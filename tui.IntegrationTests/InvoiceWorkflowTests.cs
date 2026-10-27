using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tui.Models;
using tui.Services;
using Xunit;
using DatabaseApplication = tui.IntegrationTests.UserManagementTests.DatabaseApplication;

namespace tui.IntegrationTests;

public class InvoiceWorkflowTests
{
    [MariaDbFact]
    public async Task Employee_creates_edits_and_submits_controller_approves_with_complete_audit()
    {
        await using var app = await StartAsync();
        using var employee = await app.LoginAsync(4);
        using var controller = await app.LoginAsync(5);
        var created = await CreateAsync(employee);
        Assert.Equal("concept", created.WorkflowStatus);
        Assert.Equal(4, created.CreatedByUserId);
        Assert.Equal("EUR", created.Currency);
        Assert.Equal("Openstaand", created.PaymentStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(controller, HttpMethod.Post, $"/{created.InvoiceId}/approve", new InvoiceActionRequest(created.Version))).StatusCode);
        var edit = await SendAsync(employee, HttpMethod.Put, $"/{created.InvoiceId}", Valid() with { TotalAmount = 180.50m, Version = created.Version });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var updated = (await edit.Content.ReadFromJsonAsync<InvoiceDetail>())!;
        Assert.Equal(180.50m, updated.TotalAmount);
        var submitted = await ActionAsync(employee, created.InvoiceId, "submit", updated.Version);
        Assert.Equal("ter goedkeuring", submitted.WorkflowStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(employee, HttpMethod.Put, $"/{created.InvoiceId}", Valid() with { Version = submitted.Version })).StatusCode);
        var approved = await ActionAsync(controller, created.InvoiceId, "approve", submitted.Version);
        Assert.Equal("goedgekeurd", approved.WorkflowStatus);
        Assert.Equal(5, approved.ReviewedByUserId);
        Assert.NotNull(approved.ReviewedAtUtc);
        Assert.Equal(4, approved.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(employee, HttpMethod.Delete, $"/{created.InvoiceId}?version={approved.Version}")).StatusCode);
        var list = await employee.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?workflowStatus=goedgekeurd");
        Assert.Equal("goedgekeurd", Assert.Single(list!.Items).WorkflowStatus);
        await using var db = app.CreateDb();
        var audit = await db.AuditLog.OrderBy(entry => entry.AuditLogEntryId).ToArrayAsync();
        Assert.Equal(new[] { "create", "update", "submit", "approve" }, audit.Select(entry => entry.Action));
        Assert.Equal(new[] { 4, 4, 4, 5 }, audit.Select(entry => entry.ActorUserId));
        Assert.All(audit, entry => { Assert.Equal(created.InvoiceId, entry.EntityId); Assert.Equal("Invoice", entry.EntityType); Assert.NotEqual(default, entry.OccurredAtUtc); });
        Assert.Null(audit[0].BeforeJson);
        Assert.Equal(125.25m, JsonSerializer.Deserialize<InvoiceDetail>(audit[1].BeforeJson!)!.TotalAmount);
        Assert.Equal(180.50m, JsonSerializer.Deserialize<InvoiceDetail>(audit[1].AfterJson!)!.TotalAmount);
    }

    [MariaDbFact]
    public async Task Read_only_controller_employee_and_other_roles_have_distinct_permissions()
    {
        await using var app = await StartAsync();
        using var employee = await app.LoginAsync(4);
        var invoice = await CreateAsync(employee);
        foreach (var userId in new[] { 1, 2, 5, 6 })
        {
            using var reader = await app.LoginAsync(userId);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/finance/invoices")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/finance/invoices/{invoice.InvoiceId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(reader, HttpMethod.Post, "", Valid("NOT-ALLOWED"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(reader, HttpMethod.Put, $"/{invoice.InvoiceId}", Valid() with { Version = invoice.Version })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(reader, HttpMethod.Delete, $"/{invoice.InvoiceId}?version={invoice.Version}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(reader, HttpMethod.Post, $"/{invoice.InvoiceId}/submit", new InvoiceActionRequest(invoice.Version))).StatusCode);
            if (userId != 5)
                foreach (var action in new[] { "approve", "reject" })
                    Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(reader, HttpMethod.Post, $"/{invoice.InvoiceId}/{action}", new InvoiceActionRequest(invoice.Version))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(employee, HttpMethod.Post, $"/{invoice.InvoiceId}/approve", new InvoiceActionRequest(invoice.Version))).StatusCode);
        using var sales = await app.LoginAsync(3);
        Assert.Equal(HttpStatusCode.Forbidden, (await sales.GetAsync($"/api/finance/invoices/{invoice.InvoiceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(sales, HttpMethod.Post, "", Valid())).StatusCode);
    }

    [MariaDbFact]
    public async Task Controllers_cannot_approve_or_reject_their_own_invoice_even_with_both_roles()
    {
        await using var app = await StartAsync();
        using var both = await app.LoginAsync(7);
        using var controller = await app.LoginAsync(5);
        var invoice = await CreateAsync(both);
        var submitted = await ActionAsync(both, invoice.InvoiceId, "submit", invoice.Version);
        foreach (var action in new[] { "approve", "reject" })
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(both, HttpMethod.Post, $"/{invoice.InvoiceId}/{action}", new InvoiceActionRequest(submitted.Version))).StatusCode);
        var rejected = await ActionAsync(controller, invoice.InvoiceId, "reject", submitted.Version, "Onjuiste leveranciersnota");
        Assert.Equal("afgewezen", rejected.WorkflowStatus);
        Assert.Equal("Onjuiste leveranciersnota", rejected.ReviewComment);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(both, HttpMethod.Put, $"/{invoice.InvoiceId}", Valid() with { Version = rejected.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(controller, HttpMethod.Post, $"/{invoice.InvoiceId}/approve", new InvoiceActionRequest(rejected.Version))).StatusCode);
        await using var db = app.CreateDb();
        Assert.Equal(3, await db.AuditLog.CountAsync());
    }

    [MariaDbFact]
    public async Task Validation_duplicate_references_and_stale_versions_leave_previous_values_unchanged()
    {
        await using var app = await StartAsync();
        using var employee = await app.LoginAsync(4);
        var original = await CreateAsync(employee);
        var invalid = new[]
        {
            Valid() with { Reference = " " }, Valid() with { Reference = new string('x', 51) },
            Valid() with { PartyId = null }, Valid() with { PartyId = 99999 },
            Valid() with { InvoiceType = null }, Valid() with { InvoiceDate = null },
            Valid() with { InvoiceDate = DateOnly.MinValue }, Valid() with { BookingId = 99999 },
            Valid() with { TotalAmount = null }, Valid() with { TotalAmount = 0 }, Valid() with { TotalAmount = -1 },
            Valid() with { TotalAmount = 10000000000m }, Valid() with { Currency = "FAKE" },
            Valid() with { Currency = null }, Valid() with { Currency = "XXX" }, Valid() with { Currency = "XTS" },
            Valid() with { TotalAmount = 1.001m }, Valid() with { Currency = "JPY", TotalAmount = 1.5m },
            Valid() with { DueDate = new DateOnly(2026, 1, 1) }
        };
        foreach (var request in invalid)
        {
            var response = await SendAsync(employee, HttpMethod.Put, $"/{original.InvoiceId}", request with { Version = original.Version });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(employee, HttpMethod.Post, "", Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(employee, HttpMethod.Put, $"/{original.InvoiceId}", Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(employee, HttpMethod.Put, $"/{original.InvoiceId}", Valid() with { Version = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(employee, HttpMethod.Put, "/99999", Valid() with { Version = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync("/api/finance/invoices/99999")).StatusCode);
        var stored = await employee.GetFromJsonAsync<InvoiceDetail>($"/api/finance/invoices/{original.InvoiceId}");
        Assert.Equal(original.TotalAmount, stored!.TotalAmount);
        Assert.Equal(original.Version, stored.Version);
        await using var db = app.CreateDb();
        Assert.Equal(1, await db.AuditLog.CountAsync());
    }

    [MariaDbFact]
    public async Task Currency_precision_is_preserved_and_clients_cannot_set_owner_or_status()
    {
        await using var app = await StartAsync();
        using var employee = await app.LoginAsync(4);
        var response = await SendAsync(employee, HttpMethod.Post, "", new
        {
            Reference = "KWD-1", PartyId = 1, InvoiceType = "Inkoop", InvoiceDate = "2026-10-27", TotalAmount = 1.234m, Currency = "kwd",
            CreatedByUserId = 5, WorkflowStatus = "goedgekeurd", Status = "Betaald", ReviewedByUserId = 5, Version = 999
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invoice = (await response.Content.ReadFromJsonAsync<InvoiceDetail>())!;
        Assert.Equal("KWD", invoice.Currency);
        Assert.Equal(1.234m, invoice.TotalAmount);
        Assert.Equal(4, invoice.CreatedByUserId);
        Assert.Equal("concept", invoice.WorkflowStatus);
        Assert.Equal("Openstaand", invoice.PaymentStatus);
        Assert.Null(invoice.ReviewedByUserId);
        Assert.Equal(1, invoice.Version);
        var fourDecimals = await SendAsync(employee, HttpMethod.Post, "", Valid("CLF-1") with { Currency = "CLF", TotalAmount = 1.2345m });
        Assert.Equal(HttpStatusCode.Created, fourDecimals.StatusCode);
        await using var db = app.CreateDb();
        Assert.Equal(1.2345m, (await db.Invoices.SingleAsync(stored => stored.InvoiceNumber == "CLF-1")).TotalAmount);
    }

    [MariaDbFact]
    public async Task Deleting_drafts_retains_audit_but_cannot_delete_submitted_or_financially_linked_records()
    {
        await using var app = await StartAsync();
        using var employee = await app.LoginAsync(4);
        var draft = await CreateAsync(employee);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(employee, HttpMethod.Delete, $"/{draft.InvoiceId}?version={draft.Version}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync($"/api/finance/invoices/{draft.InvoiceId}")).StatusCode);
        var pending = await CreateAsync(employee, "PENDING-1");
        pending = await ActionAsync(employee, pending.InvoiceId, "submit", pending.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(employee, HttpMethod.Delete, $"/{pending.InvoiceId}?version={pending.Version}")).StatusCode);
        var linked = await CreateAsync(employee, "LINKED-1");
        await using (var db = app.CreateDb())
        {
            db.Payments.Add(new Payment { InvoiceId = linked.InvoiceId, Amount = 1, PaymentDate = new DateOnly(2026, 10, 27) });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(employee, HttpMethod.Delete, $"/{linked.InvoiceId}?version={linked.Version}")).StatusCode);
        await using var reloaded = app.CreateDb();
        var deleted = await reloaded.AuditLog.SingleAsync(entry => entry.Action == "delete");
        Assert.Equal(draft.InvoiceId, deleted.EntityId);
        Assert.NotNull(deleted.BeforeJson);
        Assert.Null(deleted.AfterJson);
    }

    [MariaDbFact]
    public async Task Audit_failure_rolls_back_create_update_and_delete_and_missing_csrf_is_rejected()
    {
        await using var app = await StartAsync();
        using var employee = await app.LoginAsync(4);
        var invoice = await CreateAsync(employee);
        Assert.Equal(HttpStatusCode.BadRequest, (await employee.PostAsJsonAsync("/api/finance/invoices", Valid("NO-CSRF"))).StatusCode);
        app.ReadFailure.FailAuditWrites = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SendAsync(employee, HttpMethod.Post, "", Valid("AUDIT-FAIL"))).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SendAsync(employee, HttpMethod.Put, $"/{invoice.InvoiceId}", Valid() with { Version = invoice.Version, TotalAmount = 900 })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SendAsync(employee, HttpMethod.Delete, $"/{invoice.InvoiceId}?version={invoice.Version}")).StatusCode);
        app.ReadFailure.FailAuditWrites = false;
        await using var db = app.CreateDb();
        var stored = Assert.Single(await db.Invoices.ToArrayAsync());
        Assert.Equal(invoice.TotalAmount, stored.TotalAmount);
        Assert.Equal(invoice.Version, stored.Version);
        Assert.Equal(1, await db.AuditLog.CountAsync());
    }

    [MariaDbFact]
    public async Task Concurrent_reviews_produce_one_decision_and_one_audit_entry()
    {
        await using var app = await StartAsync();
        using var employee = await app.LoginAsync(4);
        using var first = await app.LoginAsync(5);
        using var second = await app.LoginAsync(8);
        var invoice = await CreateAsync(employee);
        invoice = await ActionAsync(employee, invoice.InvoiceId, "submit", invoice.Version);
        var results = await Task.WhenAll(
            SendAsync(first, HttpMethod.Post, $"/{invoice.InvoiceId}/approve", new InvoiceActionRequest(invoice.Version)),
            SendAsync(second, HttpMethod.Post, $"/{invoice.InvoiceId}/reject", new InvoiceActionRequest(invoice.Version)));
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var db = app.CreateDb();
        Assert.Equal(3, await db.AuditLog.CountAsync());
        Assert.Contains((await db.Invoices.SingleAsync()).WorkflowStatus, new[] { "goedgekeurd", "afgewezen" });
    }

    private static SaveInvoiceRequest Valid(string reference = "INV-NEW-1") =>
        new(reference, 1, null, "Inkoop", new DateOnly(2026, 10, 27), null, 125.25m, "EUR");

    private static async Task<DatabaseApplication> StartAsync()
    {
        var app = await DatabaseApplication.StartAsync();
        await using var db = app.CreateDb();
        db.AppUsers.AddRange(
            new AppUser { UserId = 4, Email = "EMPLOYEE@TEST.INVALID", UserRole = "medewerker" },
            new AppUser { UserId = 5, Email = "CONTROLLER@TEST.INVALID", UserRole = "controller" },
            new AppUser { UserId = 6, Email = "READER@TEST.INVALID", UserRole = "read_only" },
            new AppUser { UserId = 7, Email = "BOTH@TEST.INVALID", UserRole = "medewerker,controller" },
            new AppUser { UserId = 8, Email = "CONTROLLER2@TEST.INVALID", UserRole = "controller" });
        db.Parties.Add(new Party { PartyId = 1, Name = "Supplier", PartyType = "Leverancier" });
        await db.SaveChangesAsync();
        return app;
    }

    private static async Task<InvoiceDetail> CreateAsync(HttpClient client, string reference = "INV-NEW-1")
    {
        var response = await SendAsync(client, HttpMethod.Post, "", Valid(reference));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<InvoiceDetail>())!;
    }
    private static async Task<InvoiceDetail> ActionAsync(HttpClient client, int id, string action, long version, string? comment = null)
    {
        var response = await SendAsync(client, HttpMethod.Post, $"/{id}/{action}", new InvoiceActionRequest(version, comment));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InvoiceDetail>())!;
    }
    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string suffix, object? body = null)
    {
        var tokenResponse = await client.GetAsync("/api/finance/invoices/antiforgery");
        if (!tokenResponse.IsSuccessStatusCode) return tokenResponse;
        var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        if (tokenResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var previous = client.DefaultRequestHeaders.GetValues("Cookie").Single();
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", previous + "; " + cookies.First().Split(';')[0]);
        }
        using var request = new HttpRequestMessage(method, "/api/finance/invoices" + suffix);
        request.Headers.Add("RequestVerificationToken", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
