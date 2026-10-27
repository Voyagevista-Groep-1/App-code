using Microsoft.EntityFrameworkCore;
using tui.Data;

namespace tui.Services;

public record InvoiceListItem(int InvoiceId, string Reference, string Recipient, DateOnly InvoiceDate, decimal TotalAmount, string Status, string Currency = "EUR", string WorkflowStatus = "concept", bool IsManualRegistration = false, string? OriginalInvoiceNumber = null);
public record InvoicePage(InvoiceListItem[] Items, int Page, int PageSize, int TotalCount, int TotalInvoices)
{
    public int TotalPages => TotalCount == 0 ? 0 : (TotalCount - 1) / PageSize + 1;
}

public static class InvoiceStatuses
{
    public const string All = "Alle";
    public const string Open = "Openstaand";
    public const string Paid = "Betaald";
}

public class InvoiceService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public const int PageSize = 25;

    public async Task<InvoicePage> GetOverviewAsync(string? search, string? status, int page, CancellationToken cancellationToken, string? workflowStatus = null)
    {
        if (page < 1) throw new ArgumentException("Het paginanummer moet minimaal 1 zijn.");
        if (status is not null && status != InvoiceStatuses.All && status != InvoiceStatuses.Open && status != InvoiceStatuses.Paid)
            throw new ArgumentException("Kies Alle, Openstaand of Betaald als betaalstatus.");
        if (search?.Length > 200) throw new ArgumentException("De zoekopdracht mag maximaal 200 tekens bevatten.");
        if (workflowStatus is not null && !InvoiceWorkflowStates.All.Contains(workflowStatus))
            throw new ArgumentException("Ongeldige verwerkingsstatus.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Invoices.AsNoTracking();
        var totalInvoices = await query.CountAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(invoice => invoice.InvoiceNumber.ToUpper().Contains(term) ||
                (invoice.ManualRegistration != null ? invoice.ManualRegistration.RecipientName : invoice.Party.Name).ToUpper().Contains(term) ||
                (invoice.ManualRegistration != null && invoice.ManualRegistration.OriginalInvoiceNumber.ToUpper().Contains(term)));
        }
        if (status is not null && status != InvoiceStatuses.All)
        {
            var selected = status.ToUpperInvariant();
            query = query.Where(invoice => invoice.Status.ToUpper() == selected);
        }
        if (workflowStatus is not null) query = query.Where(invoice => invoice.WorkflowStatus == workflowStatus);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 1 : (totalCount - 1) / PageSize + 1;
        page = Math.Min(page, totalPages);
        var items = await query.OrderByDescending(invoice => invoice.InvoiceDate).ThenByDescending(invoice => invoice.InvoiceId)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(invoice => new InvoiceListItem(invoice.InvoiceId, invoice.InvoiceNumber, invoice.ManualRegistration != null ? invoice.ManualRegistration.RecipientName : invoice.Party.Name,
                invoice.InvoiceDate, invoice.TotalAmount, invoice.Status, invoice.Currency, invoice.WorkflowStatus, invoice.IsManualRegistration,
                invoice.ManualRegistration != null ? invoice.ManualRegistration.OriginalInvoiceNumber : null))
            .ToArrayAsync(cancellationToken);
        return new(items, page, PageSize, totalCount, totalInvoices);
    }
}
