using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using tui.Data;
using tui.Models;

namespace tui.Services;

public class ManualInvoiceRequest
{
    [Required(ErrorMessage = "Vul het oorspronkelijke factuurnummer in."), StringLength(100)] public string? OriginalInvoiceNumber { get; set; }
    [Required(ErrorMessage = "Selecteer een bestaande ontvanger.")] public int? PartyId { get; set; }
    [Required(ErrorMessage = "Vul de factuurdatum in.")] public DateOnly? InvoiceDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public DateOnly? DeliveryPeriodStart { get; set; }
    public DateOnly? DeliveryPeriodEnd { get; set; }
    [StringLength(255)] public string? Subject { get; set; }
    [StringLength(100)] public string? PaymentReference { get; set; }
    [Range(0, 3650)] public int? PaymentTermDays { get; set; }
    public DateOnly? DueDate { get; set; }
    [Required(ErrorMessage = "Selecteer een valuta.")] public string? Currency { get; set; } = "EUR";
    [StringLength(4000)] public string? Notes { get; set; }
    [Required(ErrorMessage = "Vul de naam van de afzender in."), StringLength(150)] public string? SenderName { get; set; }
    [Required(ErrorMessage = "Vul het adres van de afzender in."), StringLength(500)] public string? SenderAddress { get; set; }
    [StringLength(30)] public string? SenderKvkNumber { get; set; }
    [StringLength(50)] public string? SenderVatNumber { get; set; }
    public decimal? SourceTotalAmount { get; set; }
    public List<ManualInvoiceLineInput>? Lines { get; set; } = [];
}

public record InvoiceRecipient(int PartyId, string Name, string? Address, string? VatNumber);
public record ManualInvoiceOptions(InvoiceRecipient[] Recipients, string[] Currencies);
public record ManualInvoiceDetail(int InvoiceId, string InternalReference, string PaymentStatus,
    DateTime? RegisteredAtUtc, ManualInvoiceRequest Data, InvoiceRecipient Recipient, ManualInvoiceCalculation Calculation, string CalculationPolicy);
public sealed class ManualInvoiceValidationException(Dictionary<string, string[]> errors, int status = 400) : Exception("Controleer de gemarkeerde invoervelden.")
{
    public Dictionary<string, string[]> Errors => errors;
    public int Status => status;
}

public class ManualInvoiceService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<ManualInvoiceOptions> OptionsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return new(await db.Parties.AsNoTracking().OrderBy(party => party.Name)
            .Select(party => new InvoiceRecipient(party.PartyId, party.Name, party.Address, party.VatNumber)).ToArrayAsync(cancellationToken),
            InvoiceCurrencies.Codes);
    }

    public async Task<ManualInvoiceDetail?> GetAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices.AsNoTracking().Include(invoice => invoice.ManualRegistration).Include(invoice => invoice.InvoiceLines)
            .SingleOrDefaultAsync(invoice => invoice.InvoiceId == id && invoice.IsManualRegistration, cancellationToken);
        return invoice is null ? null : Detail(invoice);
    }

    public async Task<ManualInvoiceDetail> RegisterAsync(ClaimsPrincipal principal, ManualInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true || !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            throw new InvoiceWorkflowException(403, "Je bent niet bevoegd om facturen te registreren.");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (!await db.UserRoles.AnyAsync(role => role.UserId == actorId && role.RoleName == AccessPolicies.InvoiceEmployee, cancellationToken))
            throw new InvoiceWorkflowException(403, "Je bent niet bevoegd om facturen te registreren.");

        var calculation = ManualInvoiceCalculator.Calculate(request.Currency, request.Lines);
        var errors = new Dictionary<string, string[]>(calculation.Errors);
        void Error(string key, string message) => errors[key] = [message];
        foreach (var property in typeof(ManualInvoiceRequest).GetProperties())
        {
            var context = new ValidationContext(request) { MemberName = property.Name };
            var messages = new List<ValidationResult>();
            if (!Validator.TryValidateProperty(property.GetValue(request), context, messages))
                Error(JsonNamingPolicy.CamelCase.ConvertName(property.Name), string.Join(" ", messages.Select(message => message.ErrorMessage)));
        }
        if (string.IsNullOrWhiteSpace(request.OriginalInvoiceNumber) || request.OriginalInvoiceNumber.Any(char.IsControl))
            Error("originalInvoiceNumber", "Neem het oorspronkelijke factuurnummer over, zonder regeleinden of besturingstekens.");
        if (request.InvoiceDate is null || request.InvoiceDate == DateOnly.MinValue) Error("invoiceDate", "Vul een geldige factuurdatum in.");
        if (string.IsNullOrWhiteSpace(request.SenderName)) Error("senderName", "Vul de naam van de afzender in.");
        if (string.IsNullOrWhiteSpace(request.SenderAddress)) Error("senderAddress", "Vul het adres van de afzender in.");
        if (request.DeliveryPeriodStart.HasValue != request.DeliveryPeriodEnd.HasValue || request.DeliveryPeriodEnd < request.DeliveryPeriodStart)
            Error("deliveryPeriodEnd", "Vul een volledige leveringsperiode in, met de einddatum op of na de begindatum.");
        if (request.DeliveryDate is not null && request.DeliveryPeriodStart is not null)
            Error("deliveryDate", "Kies een leveringsdatum óf een leveringsperiode.");
        var dueDate = request.DueDate;
        if (request.InvoiceDate is { } date && request.PaymentTermDays is >= 0 and <= 3650)
        {
            if (date.DayNumber > DateOnly.MaxValue.DayNumber - request.PaymentTermDays.Value)
                Error("paymentTermDays", "De berekende vervaldatum valt buiten het ondersteunde datumbereik.");
            else
            {
                var calculatedDueDate = date.AddDays(request.PaymentTermDays.Value);
                if (dueDate is not null && dueDate != calculatedDueDate) Error("dueDate", "De vervaldatum en betalingstermijn komen niet overeen.");
                dueDate ??= calculatedDueDate;
            }
        }
        if (dueDate < request.InvoiceDate) Error("dueDate", "De vervaldatum mag niet vóór de factuurdatum liggen.");
        var recipient = await db.Parties.AsNoTracking().SingleOrDefaultAsync(party => party.PartyId == request.PartyId, cancellationToken);
        if (recipient is null) Error("partyId", "Selecteer een bestaande ontvanger.");
        else if (string.IsNullOrWhiteSpace(recipient.Name) || string.IsNullOrWhiteSpace(recipient.Address))
            Error("partyId", "De gekozen ontvanger mist naam- of adresgegevens. Laat de bestaande stamgegevens eerst aanvullen.");
        if (request.Lines?.Any(line => line is not null && line.VatTreatment == "reverse_charge") == true)
        {
            if (string.IsNullOrWhiteSpace(request.SenderVatNumber)) Error("senderVatNumber", "Neem bij verlegging het btw-id van de afzender over.");
            if (string.IsNullOrWhiteSpace(recipient?.VatNumber)) Error("partyId", "Voor verlegging ontbreekt het btw-id bij de gekozen ontvanger.");
        }
        if (request.SourceTotalAmount is { } source && (source <= 0 || source != calculation.TotalIncludingVat))
            Error("sourceTotalAmount", "Het bronbedrag wijkt af van het berekende totaal. Controleer de invoer; bedragen worden niet automatisch gecorrigeerd.");
        if (errors.Count != 0) throw new ManualInvoiceValidationException(errors);

        // Serialize registrations while checking alternate sender identifiers as well as
        // the primary identity key. This also catches a repeated source with an omitted VAT id.
        await db.Roles.FromSqlInterpolated($"SELECT * FROM Roles WHERE Name = {AccessPolicies.Finance} FOR UPDATE").ToListAsync(cancellationToken);
        var original = request.OriginalInvoiceNumber!.Trim().ToUpperInvariant();
        var vat = request.SenderVatNumber?.Trim().ToUpperInvariant();
        var kvk = request.SenderKvkNumber?.Trim().ToUpperInvariant();
        var name = request.SenderName!.Trim().ToUpperInvariant();
        var address = request.SenderAddress!.Trim().ToUpperInvariant();
        var identityKey = SenderKey(request);
        if (await db.ManualInvoiceRegistrations.AnyAsync(registration => registration.OriginalInvoiceNumber.ToUpper() == original &&
            (registration.SourceIdentityKey == identityKey ||
             (vat != null && vat != "" && registration.SenderVatNumber != null && registration.SenderVatNumber.ToUpper() == vat) ||
             (kvk != null && kvk != "" && registration.SenderKvkNumber != null && registration.SenderKvkNumber.ToUpper() == kvk) ||
             (registration.SenderName.ToUpper() == name && registration.SenderAddress.ToUpper() == address)), cancellationToken))
            throw new ManualInvoiceValidationException(new() { ["originalInvoiceNumber"] = ["Deze factuur is al geregistreerd voor deze afzender."] }, 409);

        var registeredAt = DateTime.UtcNow;
        registeredAt = new DateTime(registeredAt.Ticks - registeredAt.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
        var invoice = new Invoice
        {
            InvoiceNumber = $"REG-{Guid.NewGuid():N}", PartyId = recipient!.PartyId, InvoiceType = "Inkoop",
            InvoiceDate = request.InvoiceDate!.Value, DueDate = dueDate, Currency = request.Currency!.Trim().ToUpperInvariant(),
            TotalAmount = calculation.TotalIncludingVat, Status = InvoiceStatuses.Open, WorkflowStatus = InvoiceWorkflowStates.Draft,
            CreatedByUserId = actorId, CreatedAtUtc = registeredAt, Version = 1, IsManualRegistration = true,
            ManualRegistration = new ManualInvoiceRegistration
            {
                OriginalInvoiceNumber = request.OriginalInvoiceNumber!.Trim(), SourceIdentityKey = SenderKey(request),
                SenderName = request.SenderName!.Trim(), SenderAddress = request.SenderAddress!.Trim(),
                SenderKvkNumber = request.SenderKvkNumber?.Trim(), SenderVatNumber = request.SenderVatNumber?.Trim(),
                RecipientName = recipient.Name, RecipientAddress = recipient.Address, RecipientVatNumber = recipient.VatNumber,
                DeliveryDate = request.DeliveryDate, DeliveryPeriodStart = request.DeliveryPeriodStart, DeliveryPeriodEnd = request.DeliveryPeriodEnd,
                Subject = request.Subject?.Trim(), PaymentReference = request.PaymentReference?.Trim(), PaymentTermDays = request.PaymentTermDays,
                Notes = request.Notes?.Trim(), SubtotalBeforeDiscount = calculation.SubtotalBeforeDiscount,
                TotalDiscount = calculation.TotalDiscount, TotalExcludingVat = calculation.TotalExcludingVat, TotalVat = calculation.TotalVat,
                SourceTotalAmount = request.SourceTotalAmount, CalculationPolicy = ManualInvoiceCalculator.Policy
            },
            InvoiceLines = calculation.Lines.Select(line => new InvoiceLine
            {
                Position = line.Position, Description = line.Description, Quantity = line.Quantity, Unit = line.Unit, Price = line.UnitPrice,
                DiscountPercentage = line.DiscountPercentage, DiscountFixedAmount = line.DiscountFixedAmount,
                VatTreatment = line.VatTreatment, VatPercentage = line.VatPercentage, VatInformation = line.VatInformation,
                BaseAmount = line.BaseAmount, DiscountAmount = line.DiscountAmount, NetAmount = line.NetAmount, VatAmount = line.VatAmount, GrossAmount = line.GrossAmount
            }).ToList()
        };
        try
        {
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(cancellationToken);
            var result = Detail(invoice);
            db.AuditLog.Add(new AuditLogEntry { EntityType = "Invoice", EntityId = invoice.InvoiceId, ActorUserId = actorId,
                Action = "register_manual", OccurredAtUtc = DateTime.UtcNow, AfterJson = JsonSerializer.Serialize(result) });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        {
            throw new ManualInvoiceValidationException(new() { ["originalInvoiceNumber"] = ["Deze factuur is al geregistreerd voor deze afzender."] }, 409);
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1451 or 1452 or 1020 or 1205 or 1213 })
        {
            throw new ManualInvoiceValidationException(new() { ["partyId"] = ["Gekoppelde gegevens zijn intussen gewijzigd. Vernieuw de ontvangers en probeer opnieuw."] }, 409);
        }
    }

    private static string SenderKey(ManualInvoiceRequest request)
    {
        static string Normalize(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        var identity = !string.IsNullOrWhiteSpace(request.SenderVatNumber) ? "VAT:" + Normalize(request.SenderVatNumber).Replace(" ", "")
            : !string.IsNullOrWhiteSpace(request.SenderKvkNumber) ? "KVK:" + Normalize(request.SenderKvkNumber).Replace(" ", "")
            : "NAME:" + Normalize(request.SenderName!) + "|" + Normalize(request.SenderAddress!);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private static ManualInvoiceDetail Detail(Invoice invoice)
    {
        var registration = invoice.ManualRegistration!;
        var lines = invoice.InvoiceLines.OrderBy(line => line.Position).ThenBy(line => line.InvoiceLineId).Select(line =>
            new CalculatedInvoiceLine(line.Position, line.Description, line.Quantity, line.Unit, line.Price, line.DiscountPercentage,
                line.DiscountFixedAmount, line.VatTreatment, line.VatPercentage, line.VatInformation, line.BaseAmount, line.DiscountAmount,
                line.NetAmount, line.VatAmount, line.GrossAmount)).ToArray();
        var groups = lines.GroupBy(line => new { line.VatTreatment, line.VatPercentage }).Select(group =>
            new InvoiceVatGroup(group.Key.VatTreatment, group.Key.VatPercentage, group.Sum(line => line.NetAmount), group.Sum(line => line.VatAmount))).ToArray();
        return new(invoice.InvoiceId, invoice.InvoiceNumber, invoice.Status, invoice.CreatedAtUtc is { } created ? DateTime.SpecifyKind(created, DateTimeKind.Utc) : null,
            new ManualInvoiceRequest
            {
                OriginalInvoiceNumber = registration.OriginalInvoiceNumber, PartyId = invoice.PartyId, InvoiceDate = invoice.InvoiceDate,
                DeliveryDate = registration.DeliveryDate, DeliveryPeriodStart = registration.DeliveryPeriodStart, DeliveryPeriodEnd = registration.DeliveryPeriodEnd,
                Subject = registration.Subject, PaymentReference = registration.PaymentReference, PaymentTermDays = registration.PaymentTermDays,
                DueDate = invoice.DueDate, Currency = invoice.Currency, Notes = registration.Notes, SenderName = registration.SenderName,
                SenderAddress = registration.SenderAddress, SenderKvkNumber = registration.SenderKvkNumber, SenderVatNumber = registration.SenderVatNumber,
                SourceTotalAmount = registration.SourceTotalAmount, Lines = lines.Select(line => new ManualInvoiceLineInput
                {
                    Description = line.Description, Quantity = line.Quantity, Unit = line.Unit, UnitPrice = line.UnitPrice,
                    DiscountPercentage = line.DiscountPercentage, DiscountAmount = line.DiscountFixedAmount,
                    VatTreatment = line.VatTreatment, VatPercentage = line.VatPercentage, VatInformation = line.VatInformation
                }).ToList()
            }, new(invoice.PartyId, registration.RecipientName, registration.RecipientAddress, registration.RecipientVatNumber),
            new(lines, groups, registration.SubtotalBeforeDiscount, registration.TotalDiscount, registration.TotalExcludingVat,
                registration.TotalVat, invoice.TotalAmount, new()), registration.CalculationPolicy);
    }
}
