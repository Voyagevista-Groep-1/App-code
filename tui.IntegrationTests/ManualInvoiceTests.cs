using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using tui.Models;
using tui.Services;
using Xunit;
using DatabaseApplication = tui.IntegrationTests.UserManagementTests.DatabaseApplication;

namespace tui.IntegrationTests;

public class ManualInvoiceTests
{
    [MariaDbFact]
    public async Task Registration_saves_all_fields_lines_order_totals_and_audit_and_reopens_unchanged()
    {
        await using var app = await StartAsync();
        using var employee = await app.LoginAsync(4);
        var input = Valid();
        var response = await RegisterAsync(employee, input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ManualInvoiceDetail>())!;
        Assert.StartsWith("REG-", created.InternalReference);
        Assert.Equal("Openstaand", created.PaymentStatus);
        Assert.Equal(input.OriginalInvoiceNumber, created.Data.OriginalInvoiceNumber);
        Assert.Equal(input.Subject, created.Data.Subject);
        Assert.Equal(input.Notes, created.Data.Notes);
        Assert.Equal(input.PaymentReference, created.Data.PaymentReference);
        Assert.Equal(new DateOnly(2026, 11, 27), created.Data.DueDate);
        Assert.Equal(157.95m, created.Calculation.TotalIncludingVat);
        Assert.Equal(new[] { "Advies", "Boeken" }, created.Calculation.Lines.Select(line => line.Description));
        Assert.Equal(new[] { 1, 2 }, created.Calculation.Lines.Select(line => line.Position));
        Assert.Equal(157.95m, created.Calculation.Lines.Sum(line => line.GrossAmount));
        await using (var db = app.CreateDb())
        {
            var party = await db.Parties.SingleAsync(); party.Name = "Gewijzigde naam"; party.Address = "Ander adres"; party.VatNumber = "ANDERS";
            await db.SaveChangesAsync();
        }
        var reopened = (await employee.GetFromJsonAsync<ManualInvoiceDetail>(response.Headers.Location))!;
        Assert.Equal("VoyageVista", reopened.Recipient.Name);
        Assert.Equal("Voorbeeldstraat 1, Amsterdam", reopened.Recipient.Address);
        Assert.Equal("NL123456789B01", reopened.Recipient.VatNumber);
        Assert.Equivalent(created, reopened);
        var list = await employee.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?search=" + created.InternalReference);
        Assert.Equal(created.InternalReference, Assert.Single(list!.Items).Reference);
        Assert.True(list.Items[0].IsManualRegistration);
        Assert.Equal(input.OriginalInvoiceNumber, list.Items[0].OriginalInvoiceNumber);
        Assert.Single((await employee.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?search=bron-001"))!.Items);
        await using var stored = app.CreateDb();
        Assert.Equal(2, await stored.InvoiceLines.CountAsync());
        Assert.All(await stored.InvoiceLines.ToArrayAsync(), line => Assert.Equal(created.InvoiceId, line.InvoiceId));
        var audit = await stored.AuditLog.SingleAsync();
        Assert.Equal("register_manual", audit.Action); Assert.Equal(4, audit.ActorUserId);
        Assert.Equal(created.InvoiceId, audit.EntityId); Assert.Contains("Boeken", audit.AfterJson!);
        Assert.Contains("Factuur bekijken", await employee.GetStringAsync($"/finance/invoices/{created.InvoiceId}"));
    }

    [MariaDbFact]
    public async Task Duplicate_original_number_is_blocked_for_same_sender_but_allowed_for_different_sender()
    {
        await using var app = await StartAsync();
        using var employee = await app.LoginAsync(4);
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(employee, Valid())).StatusCode);
        var duplicate = Valid(); duplicate.SenderName = "Andere handelsnaam"; duplicate.SenderVatNumber = " nl999999999b01 ";
        var response = await RegisterAsync(employee, duplicate);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("originalInvoiceNumber", await response.Content.ReadAsStringAsync());
        var other = Valid(); other.SenderVatNumber = "NL111111111B01"; other.SenderName = "Andere leverancier"; other.SenderKvkNumber = "87654321"; other.SenderAddress = "Ander adres 5, Rotterdam";
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(employee, other)).StatusCode);
        using var secondClient = await app.LoginAsync(4);
        var firstConcurrent = Valid(); firstConcurrent.OriginalInvoiceNumber = "CONCURRENT-1";
        var secondConcurrent = Valid(); secondConcurrent.OriginalInvoiceNumber = "CONCURRENT-1"; secondConcurrent.SenderVatNumber = null;
        var concurrent = await Task.WhenAll(RegisterAsync(employee, firstConcurrent), RegisterAsync(secondClient, secondConcurrent));
        Assert.Single(concurrent, result => result.StatusCode == HttpStatusCode.Created);
        Assert.Single(concurrent, result => result.StatusCode == HttpStatusCode.Conflict);
        await using var db = app.CreateDb();
        Assert.Equal(3, await db.Invoices.CountAsync());
        Assert.Equal(3, await db.Invoices.Select(invoice => invoice.InvoiceNumber).Distinct().CountAsync());
    }

    [MariaDbFact]
    public async Task Field_and_line_errors_do_not_save_partial_records()
    {
        await using var app = await StartAsync(); using var employee = await app.LoginAsync(4);
        var input = Valid(); input.SenderAddress = ""; input.PartyId = 99999; input.Lines![1].Description = "";
        var response = await RegisterAsync(employee, input);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("senderAddress", body); Assert.Contains("partyId", body); Assert.Contains("lines[1].description", body);
        var mismatch = Valid(); mismatch.SourceTotalAmount = 1;
        Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync(employee, mismatch)).StatusCode);
        var dates = Valid(); dates.DueDate = new DateOnly(2026, 10, 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync(employee, dates)).StatusCode);
        var empty = Valid(); empty.Lines = [];
        Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync(employee, empty)).StatusCode);
        await using var db = app.CreateDb();
        Assert.Empty(await db.Invoices.ToArrayAsync()); Assert.Empty(await db.InvoiceLines.ToArrayAsync()); Assert.Empty(await db.ManualInvoiceRegistrations.ToArrayAsync()); Assert.Empty(await db.AuditLog.ToArrayAsync());
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(employee, Valid())).StatusCode);
    }

    [MariaDbFact]
    public async Task Audit_failure_rolls_back_header_registration_and_every_line()
    {
        await using var app = await StartAsync(); using var employee = await app.LoginAsync(4);
        app.ReadFailure.FailAuditWrites = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await RegisterAsync(employee, Valid())).StatusCode);
        app.ReadFailure.FailAuditWrites = false;
        await using var db = app.CreateDb();
        Assert.Empty(await db.Invoices.ToArrayAsync()); Assert.Empty(await db.InvoiceLines.ToArrayAsync()); Assert.Empty(await db.ManualInvoiceRegistrations.ToArrayAsync()); Assert.Empty(await db.AuditLog.ToArrayAsync());
    }

    [MariaDbFact]
    public async Task Only_employee_registers_readers_can_reopen_and_saved_manual_invoices_cannot_be_mutated()
    {
        await using var app = await StartAsync(); using var employee = await app.LoginAsync(4);
        Assert.Contains("Factuur handmatig registreren", await employee.GetStringAsync("/finance/invoices/new"));
        var response = await RegisterAsync(employee, Valid());
        var invoice = (await response.Content.ReadFromJsonAsync<ManualInvoiceDetail>())!;
        using var finance = await app.LoginAsync(2);
        Assert.Equal(HttpStatusCode.OK, (await finance.GetAsync(response.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.PostAsJsonAsync("/api/finance/invoices/manual", Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.GetAsync("/api/finance/invoices/manual/options")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await finance.GetAsync("/finance/invoices/new")).StatusCode);
        using var sales = await app.LoginAsync(3);
        Assert.Equal(HttpStatusCode.Forbidden, (await sales.GetAsync(response.Headers.Location)).StatusCode);
        using var anonymous = app.App.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/finance/invoices/manual", Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await employee.PostAsJsonAsync("/api/finance/invoices/manual", Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(employee, HttpMethod.Delete, $"/{invoice.InvoiceId}?version=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(employee, HttpMethod.Post, $"/{invoice.InvoiceId}/submit", new InvoiceActionRequest(1))).StatusCode);
        var headerOnly = new SaveInvoiceRequest("CHANGED", 1, null, "Inkoop", new DateOnly(2026, 10, 28), null, 1, "EUR", 1);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(employee, HttpMethod.Put, $"/{invoice.InvoiceId}", headerOnly)).StatusCode);
        await using var db = app.CreateDb(); Assert.Equal(157.95m, (await db.Invoices.SingleAsync()).TotalAmount);
    }

    [MariaDbFact]
    public async Task Reverse_charge_requires_information_and_recipient_vat_number_and_saves_separate_groups()
    {
        await using var app = await StartAsync(); using var employee = await app.LoginAsync(4);
        var input = Valid(); input.Lines = [new() { Description = "Verlegd", UnitPrice = 100, VatTreatment = "reverse_charge", VatPercentage = 0 }];
        Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync(employee, input)).StatusCode);
        input.Lines[0].VatInformation = "Btw verlegd volgens bronfactuur";
        await using (var db = app.CreateDb()) { (await db.Parties.SingleAsync()).VatNumber = null; await db.SaveChangesAsync(); }
        var response = await RegisterAsync(employee, input);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Contains("partyId", await response.Content.ReadAsStringAsync());
        await using (var db = app.CreateDb()) { (await db.Parties.SingleAsync()).VatNumber = "NL123456789B01"; await db.SaveChangesAsync(); }
        input.Lines.Add(new() { Description = "Vrijgesteld", UnitPrice = 50, VatTreatment = "exempt", VatPercentage = 0, VatInformation = "Vrijstellingsgrond bronfactuur" });
        var success = await RegisterAsync(employee, input); Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        var result = (await success.Content.ReadFromJsonAsync<ManualInvoiceDetail>())!;
        Assert.Equal(2, result.Calculation.VatGroups.Length); Assert.Equal(0, result.Calculation.TotalVat); Assert.Equal(150, result.Calculation.TotalIncludingVat);
    }

    private static ManualInvoiceRequest Valid() => new()
    {
        OriginalInvoiceNumber = "BRON-001", PartyId = 1, InvoiceDate = new DateOnly(2026, 10, 28), PaymentTermDays = 30,
        SenderName = "Leverancier BV", SenderAddress = "Leveranciersstraat 2, Utrecht", SenderVatNumber = "NL999999999B01", SenderKvkNumber = "12345678",
        Subject = "Diensten en boeken", PaymentReference = "PAY-001", Notes = "Overgenomen uit bron", DeliveryPeriodStart = new DateOnly(2026, 10, 1), DeliveryPeriodEnd = new DateOnly(2026, 10, 27),
        Lines = [new() { Description = "Advies", Quantity = 2, Unit = "uren", UnitPrice = 50, DiscountPercentage = 10, VatPercentage = 21 },
                 new() { Description = "Boeken", Quantity = 5, UnitPrice = 10, DiscountAmount = 5, VatPercentage = 9 }]
    };
    private static async Task<DatabaseApplication> StartAsync()
    {
        var app = await DatabaseApplication.StartAsync(); await using var db = app.CreateDb();
        db.AppUsers.Add(new AppUser { UserId = 4, Email = "MANUAL@TEST.INVALID", UserRole = "medewerker" });
        db.Parties.Add(new Party { PartyId = 1, Name = "VoyageVista", Address = "Voorbeeldstraat 1, Amsterdam", VatNumber = "NL123456789B01", PartyType = "Bedrijf" });
        await db.SaveChangesAsync(); return app;
    }
    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, ManualInvoiceRequest input) => SendAsync(client, HttpMethod.Post, "/manual", input);
    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string suffix, object? body = null)
    {
        var tokenResponse = await client.GetAsync("/api/finance/invoices/antiforgery");
        var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        if (tokenResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var previous = client.DefaultRequestHeaders.GetValues("Cookie").Single(); client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", previous + "; " + cookies.First().Split(';')[0]);
        }
        using var request = new HttpRequestMessage(method, "/api/finance/invoices" + suffix); request.Headers.Add("RequestVerificationToken", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
