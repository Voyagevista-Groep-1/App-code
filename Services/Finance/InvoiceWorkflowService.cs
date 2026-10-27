using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using tui.Data;
using tui.Models;

namespace tui.Services;

public static class InvoiceWorkflowStates
{
    public const string Draft = "concept";
    public const string Pending = "ter goedkeuring";
    public const string Approved = "goedgekeurd";
    public const string Rejected = "afgewezen";
    public static readonly string[] All = [Draft, Pending, Approved, Rejected];
}

public record SaveInvoiceRequest(string? Reference, int? PartyId, int? BookingId, string? InvoiceType,
    DateOnly? InvoiceDate, DateOnly? DueDate, decimal? TotalAmount, string? Currency, long? Version = null);
public record InvoiceActionRequest(long? Version, string? Comment = null);
public record InvoiceDetail(int InvoiceId, string Reference, int PartyId, string Recipient, int? BookingId,
    string InvoiceType, DateOnly InvoiceDate, DateOnly? DueDate, decimal TotalAmount, string Currency,
    string PaymentStatus, string WorkflowStatus, int? CreatedByUserId, int? ReviewedByUserId,
    DateTime? CreatedAtUtc, DateTime? UpdatedAtUtc, DateTime? ReviewedAtUtc, string? ReviewComment, long Version);

public sealed class InvoiceWorkflowException(int status, string message) : Exception(message)
{
    public int Status => status;
}

public class InvoiceWorkflowService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<InvoiceDetail?> GetAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices.AsNoTracking().Include(invoice => invoice.Party)
            .SingleOrDefaultAsync(invoice => invoice.InvoiceId == id, cancellationToken);
        return invoice is null ? null : Detail(invoice);
    }

    public async Task<InvoiceDetail?> ExecuteAsync(ClaimsPrincipal principal, string action, int? id,
        SaveInvoiceRequest? request, InvoiceActionRequest? decision, CancellationToken cancellationToken)
    {
        var requiredRole = action is "approve" or "reject" ? AccessPolicies.InvoiceController : AccessPolicies.InvoiceEmployee;
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            throw new InvoiceWorkflowException(403, "Je bent niet bevoegd voor deze factuuractie.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await db.UserRoles.AnyAsync(role => role.UserId == actorId && role.RoleName == requiredRole, cancellationToken))
            throw new InvoiceWorkflowException(403, "Je bent niet bevoegd voor deze factuuractie.");

        try
        {
            Invoice invoice;
            InvoiceDetail? before = null;
            if (action == "create")
            {
                await ValidateAsync(db, request, cancellationToken);
                invoice = new Invoice
                {
                    CreatedByUserId = actorId, CreatedAtUtc = DateTime.UtcNow,
                    Status = InvoiceStatuses.Open, WorkflowStatus = InvoiceWorkflowStates.Draft, Version = 1
                };
                Apply(invoice, request!);
                db.Invoices.Add(invoice);
                await db.SaveChangesAsync(cancellationToken);
                invoice.Party = await db.Parties.SingleAsync(party => party.PartyId == invoice.PartyId, cancellationToken);
            }
            else
            {
                invoice = await db.Invoices.Include(invoice => invoice.Party).SingleOrDefaultAsync(invoice => invoice.InvoiceId == id, cancellationToken)
                    ?? throw new InvoiceWorkflowException(404, "De factuur bestaat niet.");
                before = Detail(invoice);
                if (invoice.IsManualRegistration)
                    throw new InvoiceWorkflowException(409, "Handmatig geregistreerde bronfacturen zijn na opslag alleen-lezen.");
                var expectedVersion = action == "update" ? request?.Version : decision?.Version;
                if (expectedVersion is null) throw new InvoiceWorkflowException(400, "Geef de versie van de opgehaalde factuur mee.");
                if (expectedVersion != invoice.Version) throw new InvoiceWorkflowException(409, "De factuur is intussen gewijzigd. Haal deze opnieuw op.");

                if (action is "update" or "delete" or "submit")
                {
                    if (invoice.WorkflowStatus != InvoiceWorkflowStates.Draft)
                        throw new InvoiceWorkflowException(409, "Alleen conceptfacturen kunnen worden bewerkt, ingediend of verwijderd.");
                }
                else
                {
                    if (invoice.CreatedByUserId is null)
                        throw new InvoiceWorkflowException(409, "De oorspronkelijke maker is onbekend; deze factuur kan niet worden beoordeeld.");
                    if (invoice.CreatedByUserId == actorId)
                        throw new InvoiceWorkflowException(403, "Je mag je eigen factuur niet goedkeuren of afwijzen.");
                    if (invoice.WorkflowStatus != InvoiceWorkflowStates.Pending)
                        throw new InvoiceWorkflowException(409, "Alleen facturen ter goedkeuring kunnen worden beoordeeld.");
                }

                switch (action)
                {
                    case "update":
                        await ValidateAsync(db, request, cancellationToken);
                        Apply(invoice, request!);
                        invoice.Party = await db.Parties.SingleAsync(party => party.PartyId == invoice.PartyId, cancellationToken);
                        break;
                    case "submit":
                        if (invoice.CreatedByUserId is null)
                            throw new InvoiceWorkflowException(409, "De oorspronkelijke maker is onbekend; deze factuur kan niet worden ingediend.");
                        // Validate legacy/imported records too, before a controller sees them.
                        await ValidateAsync(db, new(invoice.InvoiceNumber, invoice.PartyId, invoice.BookingId, invoice.InvoiceType,
                            invoice.InvoiceDate, invoice.DueDate, invoice.TotalAmount, invoice.Currency), cancellationToken);
                        invoice.WorkflowStatus = InvoiceWorkflowStates.Pending;
                        break;
                    case "approve":
                    case "reject":
                        if (decision?.Comment?.Length > 1000) throw new InvoiceWorkflowException(400, "De toelichting mag maximaal 1000 tekens bevatten.");
                        invoice.WorkflowStatus = action == "approve" ? InvoiceWorkflowStates.Approved : InvoiceWorkflowStates.Rejected;
                        invoice.ReviewedByUserId = actorId;
                        invoice.ReviewedAtUtc = DateTime.UtcNow;
                        invoice.ReviewComment = decision?.Comment?.Trim();
                        break;
                    case "delete":
                        if (await db.Payments.AnyAsync(payment => payment.InvoiceId == id, cancellationToken) ||
                            await db.JournalEntries.AnyAsync(entry => entry.InvoiceId == id, cancellationToken))
                            throw new InvoiceWorkflowException(409, "Een factuur met betalingen of journaalposten kan niet worden verwijderd.");
                        db.InvoiceLines.RemoveRange(await db.InvoiceLines.Where(line => line.InvoiceId == id).ToArrayAsync(cancellationToken));
                        db.Invoices.Remove(invoice);
                        break;
                    default: throw new InvoiceWorkflowException(400, "Onbekende factuuractie.");
                }
                invoice.UpdatedAtUtc = DateTime.UtcNow;
                if (action != "delete") invoice.Version++;
            }

            var after = action == "delete" ? null : Detail(invoice);
            db.AuditLog.Add(new AuditLogEntry
            {
                EntityType = "Invoice", EntityId = invoice.InvoiceId, Action = action, ActorUserId = actorId,
                OccurredAtUtc = DateTime.UtcNow,
                BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
                AfterJson = after is null ? null : JsonSerializer.Serialize(after)
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return after;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvoiceWorkflowException(409, "De factuur is intussen gewijzigd. Haal deze opnieuw op.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1020 or 1205 or 1213 })
        {
            // MariaDB may report a changed snapshot (1020) instead of zero affected rows.
            throw new InvoiceWorkflowException(409, "De factuur is gelijktijdig gewijzigd of vergrendeld. Haal deze opnieuw op en probeer opnieuw.");
        }
        catch (MySqlException exception) when (exception.Number is 1020 or 1205 or 1213)
        {
            throw new InvoiceWorkflowException(409, "De factuur is gelijktijdig gewijzigd of vergrendeld. Haal deze opnieuw op en probeer opnieuw.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        {
            throw new InvoiceWorkflowException(409, "Deze factuurreferentie bestaat al.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1451 or 1452 })
        {
            throw new InvoiceWorkflowException(409, "Gekoppelde gegevens zijn gewijzigd of verhinderen deze actie. Haal de factuur opnieuw op.");
        }
    }

    private static async Task ValidateAsync(ApplicationDbContext db, SaveInvoiceRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Reference) || request.Reference.Trim().Length > 50 ||
            string.IsNullOrWhiteSpace(request.InvoiceType) || request.InvoiceType.Trim().Length > 20 ||
            request.PartyId is null or <= 0 || request.InvoiceDate is null || request.InvoiceDate == default(DateOnly))
            throw new InvoiceWorkflowException(400, "Factuurreferentie (maximaal 50 tekens), ontvanger, type (maximaal 20 tekens) en factuurdatum zijn verplicht.");
        if (request.TotalAmount is null or <= 0 or > 9999999999.9999m)
            throw new InvoiceWorkflowException(400, "Het bedrag moet groter dan 0 en maximaal 9.999.999.999,9999 zijn.");
        var currency = request.Currency?.Trim().ToUpperInvariant();
        if (currency is null || !InvoiceCurrencies.TryGetMinorUnits(currency, out var decimals))
            throw new InvoiceWorkflowException(400, "Geef een geldige ISO 4217-valuta op, bijvoorbeeld EUR.");
        if (decimal.Round(request.TotalAmount.Value, decimals) != request.TotalAmount)
            throw new InvoiceWorkflowException(400, $"Deze valuta ondersteunt maximaal {decimals} decimalen.");
        if (request.DueDate is not null && request.DueDate < request.InvoiceDate)
            throw new InvoiceWorkflowException(400, "De vervaldatum mag niet vóór de factuurdatum liggen.");
        if (!await db.Parties.AnyAsync(party => party.PartyId == request.PartyId, cancellationToken))
            throw new InvoiceWorkflowException(400, "De ontvanger bestaat niet.");
        if (request.BookingId is not null && !await db.Bookings.AnyAsync(booking => booking.BookingId == request.BookingId, cancellationToken))
            throw new InvoiceWorkflowException(400, "De boeking bestaat niet.");
    }

    private static void Apply(Invoice invoice, SaveInvoiceRequest request)
    {
        invoice.InvoiceNumber = request.Reference!.Trim(); invoice.PartyId = request.PartyId!.Value;
        invoice.BookingId = request.BookingId; invoice.InvoiceType = request.InvoiceType!.Trim();
        invoice.InvoiceDate = request.InvoiceDate!.Value; invoice.DueDate = request.DueDate;
        invoice.TotalAmount = request.TotalAmount!.Value; invoice.Currency = request.Currency!.Trim().ToUpperInvariant();
    }

    private static InvoiceDetail Detail(Invoice invoice) => new(invoice.InvoiceId, invoice.InvoiceNumber, invoice.PartyId,
        invoice.Party.Name, invoice.BookingId, invoice.InvoiceType, invoice.InvoiceDate, invoice.DueDate,
        invoice.TotalAmount, invoice.Currency, invoice.Status, invoice.WorkflowStatus, invoice.CreatedByUserId, invoice.ReviewedByUserId,
        invoice.CreatedAtUtc, invoice.UpdatedAtUtc, invoice.ReviewedAtUtc, invoice.ReviewComment, invoice.Version);
}
