using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using tui.Models;
using tui.Services;
using Xunit;
using DatabaseApplication = tui.IntegrationTests.UserManagementTests.DatabaseApplication;

namespace tui.IntegrationTests;

public class InvoiceOverviewTests
{
    [MariaDbFact]
    public async Task Overview_matches_database_and_has_stable_pagination_newest_first()
    {
        await using var app = await DatabaseApplication.StartAsync();
        var invoices = await SeedAsync(app);
        using var finance = await app.LoginAsync(2);
        var first = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices");
        var second = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?page=2");
        var last = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?page=3");
        Assert.Equal(25, first!.PageSize);
        Assert.Equal(56, first.TotalCount);
        Assert.Equal(56, first.TotalInvoices);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(25, first.Items.Length);
        Assert.Equal(25, second!.Items.Length);
        Assert.Equal(6, last!.Items.Length);
        var all = first.Items.Concat(second.Items).Concat(last.Items).ToArray();
        var expected = invoices.OrderByDescending(invoice => invoice.InvoiceDate).ThenByDescending(invoice => invoice.InvoiceId).ToArray();
        Assert.Equal(expected.Select(invoice => invoice.InvoiceId), all.Select(invoice => invoice.InvoiceId));
        Assert.Equal(56, all.Select(invoice => invoice.InvoiceId).Distinct().Count());
        foreach (var item in all)
        {
            var stored = invoices.Single(invoice => invoice.InvoiceId == item.InvoiceId);
            Assert.Equal(stored.InvoiceNumber, item.Reference);
            Assert.Equal(stored.Party.Name, item.Recipient);
            Assert.Equal(stored.InvoiceDate, item.InvoiceDate);
            Assert.Equal(stored.TotalAmount, item.TotalAmount);
            Assert.Equal(stored.Status, item.Status);
        }
        Assert.Equal(first.Items.Select(item => item.InvoiceId), (await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices"))!.Items.Select(item => item.InvoiceId));
        Assert.Equal(3, (await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?page=2147483647"))!.Page);
    }

    [MariaDbFact]
    public async Task Searching_is_case_insensitive_partial_and_combines_with_status_and_pagination()
    {
        await using var app = await DatabaseApplication.StartAsync();
        var invoices = await SeedAsync(app);
        using var finance = await app.LoginAsync(2);
        var exact = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?search=inv-alpha-001");
        Assert.Equal("INV-ALPHA-001", Assert.Single(exact!.Items).Reference);
        Assert.Single((await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?search=%20-alpha-%20"))!.Items);
        var recipients = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?search=oRbIt");
        Assert.Equal(28, recipients!.TotalCount);
        Assert.All(recipients.Items, item => Assert.Equal("Orbit Tours", item.Recipient));
        var next = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?search=oRbIt&status=Alle&page=2");
        Assert.Equal(3, next!.Items.Length);
        Assert.Empty(recipients.Items.Select(item => item.InvoiceId).Intersect(next.Items.Select(item => item.InvoiceId)));
        var filtered = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?search=oRbIt&status=Openstaand");
        Assert.Equal(invoices.Count(invoice => invoice.Party.Name == "Orbit Tours" && invoice.Status == "Openstaand"), filtered!.TotalCount);
        Assert.All(filtered.Items, item => { Assert.Equal("Orbit Tours", item.Recipient); Assert.Equal("Openstaand", item.Status); });
        var cleared = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?search=&status=Openstaand");
        Assert.Equal(invoices.Count(invoice => invoice.Status == "Openstaand"), cleared!.TotalCount);
        Assert.All(cleared.Items, item => Assert.Equal("Openstaand", item.Status));
        var paid = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?status=Betaald");
        Assert.Equal(invoices.Count(invoice => invoice.Status == "Betaald"), paid!.TotalCount);
        Assert.All(paid.Items, item => Assert.Equal("Betaald", item.Status));
        Assert.Equal(0, (await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?search=%25"))!.TotalCount);
    }

    [MariaDbFact]
    public async Task Empty_database_and_no_matches_are_distinguishable()
    {
        await using var app = await DatabaseApplication.StartAsync();
        using var finance = await app.LoginAsync(2);
        var empty = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices");
        Assert.Equal(0, empty!.TotalInvoices);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.TotalPages);
        Assert.Empty(empty.Items);
        await SeedAsync(app);
        var noMatches = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices?search=no-such-invoice&status=Betaald&page=3");
        Assert.Equal(56, noMatches!.TotalInvoices);
        Assert.Equal(0, noMatches.TotalCount);
        Assert.Empty(noMatches.Items);
        Assert.Equal(1, noMatches.Page);
    }

    [MariaDbFact]
    public async Task Finance_and_admin_are_allowed_but_sales_and_anonymous_are_blocked()
    {
        await using var app = await DatabaseApplication.StartAsync();
        foreach (var userId in new[] { 1, 2 })
        {
            using var allowed = await app.LoginAsync(userId);
            Assert.Equal(HttpStatusCode.OK, (await allowed.GetAsync("/api/finance/invoices")).StatusCode);
            Assert.Contains("Facturen", await allowed.GetStringAsync("/finance/invoices"));
        }
        using var sales = await app.LoginAsync(3);
        Assert.Equal(HttpStatusCode.Forbidden, (await sales.GetAsync("/api/finance/invoices")).StatusCode);
        var deniedPage = await sales.GetAsync("/finance/invoices");
        Assert.Equal(HttpStatusCode.Redirect, deniedPage.StatusCode);
        Assert.Contains("/access-denied", deniedPage.Headers.Location!.ToString());
        using var anonymous = app.App.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/finance/invoices")).StatusCode);
        var login = await anonymous.GetAsync("/finance/invoices");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("/auth/login", login.Headers.Location!.ToString());
    }

    [MariaDbFact]
    public async Task Status_is_read_from_invoice_without_deriving_it_from_payments()
    {
        await using var app = await DatabaseApplication.StartAsync();
        var invoices = await SeedAsync(app, 3);
        await using (var db = app.CreateDb())
        {
            db.Payments.Add(new Payment { InvoiceId = invoices[0].InvoiceId, Amount = invoices[0].TotalAmount, PaymentDate = new DateOnly(2026, 10, 27) });
            await db.SaveChangesAsync();
        }
        using var finance = await app.LoginAsync(2);
        var result = await finance.GetFromJsonAsync<InvoicePage>("/api/finance/invoices");
        Assert.Equal("Openstaand", result!.Items.Single(item => item.InvoiceId == invoices[0].InvoiceId).Status);
        Assert.Equal("Betaald", result.Items.Single(item => item.InvoiceId == invoices[2].InvoiceId).Status);
        await using var reloaded = app.CreateDb();
        Assert.Equal(3, await reloaded.Invoices.CountAsync());
        Assert.Single(await reloaded.Payments.ToArrayAsync());
    }

    [MariaDbFact]
    public async Task Invalid_query_is_rejected_and_database_failures_allow_retry()
    {
        await using var app = await DatabaseApplication.StartAsync();
        using var finance = await app.LoginAsync(2);
        foreach (var path in new[] { "?status=Unknown", "?page=0", "?page=-1", "?page=not-a-number", "?search=" + new string('a', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await finance.GetAsync("/api/finance/invoices" + path)).StatusCode);
        app.ReadFailure.Enabled = true;
        var failed = await finance.GetAsync("/api/finance/invoices");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Contains("Probeer het opnieuw", await failed.Content.ReadAsStringAsync());
        app.ReadFailure.Enabled = false;
        Assert.Equal(HttpStatusCode.OK, (await finance.GetAsync("/api/finance/invoices")).StatusCode);
    }

    private static async Task<Invoice[]> SeedAsync(DatabaseApplication app, int count = 56)
    {
        await using var db = app.CreateDb();
        var acme = new Party { Name = "Acme Travel", PartyType = "Klant" };
        var orbit = new Party { Name = "Orbit Tours", PartyType = "Klant" };
        var invoices = Enumerable.Range(1, count).Select(index => new Invoice
        {
            Party = index % 2 == 0 ? orbit : acme,
            InvoiceNumber = index == 1 ? "INV-ALPHA-001" : $"INV-{index:000}",
            InvoiceType = "Verkoop",
            InvoiceDate = new DateOnly(2026, 10, 1).AddDays(index / 2),
            TotalAmount = 1234.56m + index,
            Status = index % 3 == 0 ? InvoiceStatuses.Paid : InvoiceStatuses.Open
        }).ToArray();
        db.Invoices.AddRange(invoices);
        await db.SaveChangesAsync();
        return invoices;
    }
}
