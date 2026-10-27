namespace tui.Services;

public class ManualInvoiceLineInput
{
    public string? Description { get; set; } = "";
    public decimal? Quantity { get; set; } = 1;
    public string? Unit { get; set; } = "stuks";
    public decimal? UnitPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? VatTreatment { get; set; } = "regular";
    public decimal? VatPercentage { get; set; } = 21;
    public string? VatInformation { get; set; }
}

public record CalculatedInvoiceLine(int Position, string Description, decimal Quantity, string Unit, decimal UnitPrice,
    decimal? DiscountPercentage, decimal? DiscountFixedAmount, string VatTreatment, decimal VatPercentage, string? VatInformation,
    decimal BaseAmount, decimal DiscountAmount, decimal NetAmount, decimal VatAmount, decimal GrossAmount);
public record InvoiceVatGroup(string Treatment, decimal Percentage, decimal TaxableAmount, decimal VatAmount);
public record ManualInvoiceCalculation(CalculatedInvoiceLine[] Lines, InvoiceVatGroup[] VatGroups,
    decimal SubtotalBeforeDiscount, decimal TotalDiscount, decimal TotalExcludingVat, decimal TotalVat, decimal TotalIncludingVat,
    Dictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class ManualInvoiceCalculator
{
    public const int MaximumLines = 200;
    public const decimal MaximumMoney = 9999999999.9999m;
    public const string Policy = "per-line-away-from-zero-v1";
    public static ManualInvoiceCalculation Calculate(string? currency, IReadOnlyList<ManualInvoiceLineInput>? input)
    {
        var errors = new Dictionary<string, string[]>();
        var lines = new List<CalculatedInvoiceLine>();
        void Error(string key, string message) => errors[key] = [message];
        if (!InvoiceCurrencies.TryGetMinorUnits(currency?.Trim().ToUpperInvariant() ?? "", out var digits))
            Error("currency", "Selecteer een ondersteunde ISO-valuta.");
        if (input is null || input.Count == 0) Error("lines", "Voeg minimaal één geldige factuurregel toe.");
        else if (input.Count > MaximumLines) Error("lines", $"Een factuur mag maximaal {MaximumLines} regels bevatten.");
        if (errors.Count > 0) return Result();

        decimal Round(decimal amount) => decimal.Round(amount, digits, MidpointRounding.AwayFromZero);
        for (var index = 0; index < input!.Count; index++)
        {
            var line = input[index];
            var key = $"lines[{index}]";
            if (line is null) { Error(key, "Deze factuurregel ontbreekt."); continue; }
            var errorCount = errors.Count;
            if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Trim().Length > 255)
                Error(key + ".description", "Vul een omschrijving in van maximaal 255 tekens.");
            if (line.Quantity is null or <= 0 or > 999999.999999m || decimal.Round(line.Quantity.Value, 6) != line.Quantity)
                Error(key + ".quantity", "Aantal moet positief zijn, maximaal 999999,999999, met maximaal 6 decimalen.");
            if (line.UnitPrice is null or < 0 or > MaximumMoney || decimal.Round(line.UnitPrice.Value, 6) != line.UnitPrice)
                Error(key + ".unitPrice", "Eenheidsprijs moet nul of positief zijn, met maximaal 6 decimalen.");
            if (line.Unit?.Length > 30) Error(key + ".unit", "Eenheid mag maximaal 30 tekens bevatten.");
            if (line.DiscountPercentage is < 0 or > 100 || (line.DiscountPercentage is { } percent && decimal.Round(percent, 6) != percent))
                Error(key + ".discountPercentage", "Kortingspercentage moet tussen 0 en 100 liggen, met maximaal 6 decimalen.");
            if (line.DiscountAmount is < 0 or > MaximumMoney || (line.DiscountAmount is { } fixedAmount && Round(fixedAmount) != fixedAmount))
                Error(key + ".discountAmount", "Vul een geldig kortingsbedrag in met de valutaprecisie.");
            if (line.DiscountPercentage.GetValueOrDefault() != 0 && line.DiscountAmount.GetValueOrDefault() != 0)
                Error(key + ".discountAmount", "Gebruik een kortingspercentage óf een kortingsbedrag.");
            if (line.VatTreatment is not ("regular" or "exempt" or "reverse_charge"))
                Error(key + ".vatTreatment", "Selecteer een btw-behandeling.");
            if (line.VatTreatment == "regular" && line.VatPercentage is not (0 or 9 or 21))
                Error(key + ".vatPercentage", "Selecteer 0%, 9% of 21% btw.");
            if (line.VatTreatment is "exempt" or "reverse_charge")
            {
                if (line.VatPercentage is not 0) Error(key + ".vatPercentage", "Bij vrijstelling of verlegging is het berekende btw-tarief 0%.");
                if (string.IsNullOrWhiteSpace(line.VatInformation)) Error(key + ".vatInformation", "Vermeld de grond voor vrijstelling of de verleggingsinformatie van de bronfactuur.");
            }
            if (line.VatInformation?.Length > 1000) Error(key + ".vatInformation", "Btw-toelichting mag maximaal 1000 tekens bevatten.");
            if (errors.Count != errorCount) continue;

            var baseAmount = Round(line.Quantity!.Value * line.UnitPrice!.Value);
            if (baseAmount > MaximumMoney) { Error(key + ".unitPrice", "Het regelbedrag is te groot."); continue; }
            var discount = Round(line.DiscountPercentage.GetValueOrDefault() != 0
                ? baseAmount * line.DiscountPercentage!.Value / 100 : line.DiscountAmount.GetValueOrDefault());
            if (discount > baseAmount) { Error(key + ".discountAmount", "De korting mag niet hoger zijn dan het regelbedrag."); continue; }
            var net = baseAmount - discount;
            var vat = Round(net * line.VatPercentage!.Value / 100);
            if (net + vat > MaximumMoney) { Error(key + ".unitPrice", "Het regelbedrag inclusief btw is te groot."); continue; }
            lines.Add(new(index + 1, line.Description!.Trim(), line.Quantity.Value, line.Unit?.Trim() ?? "", line.UnitPrice.Value,
                line.DiscountPercentage, line.DiscountAmount, line.VatTreatment!, line.VatPercentage.Value, line.VatInformation?.Trim(),
                baseAmount, discount, net, vat, net + vat));
        }
        if (errors.Count == 0 && (lines.Sum(line => line.GrossAmount) <= 0 ||
            new[] { lines.Sum(line => line.BaseAmount), lines.Sum(line => line.DiscountAmount), lines.Sum(line => line.NetAmount),
                lines.Sum(line => line.VatAmount), lines.Sum(line => line.GrossAmount) }.Any(total => total > MaximumMoney)))
            Error("lines", "Het factuurtotaal moet positief zijn en binnen de ondersteunde bedraggrens vallen.");
        return Result();

        ManualInvoiceCalculation Result() => new(lines.ToArray(),
            lines.GroupBy(line => new { line.VatTreatment, line.VatPercentage }).Select(group =>
                new InvoiceVatGroup(group.Key.VatTreatment, group.Key.VatPercentage, group.Sum(line => line.NetAmount), group.Sum(line => line.VatAmount))).ToArray(),
            lines.Sum(line => line.BaseAmount), lines.Sum(line => line.DiscountAmount), lines.Sum(line => line.NetAmount),
            lines.Sum(line => line.VatAmount), lines.Sum(line => line.GrossAmount), errors);
    }
}
