using tui.Services;
using Xunit;

namespace tui.IntegrationTests;

public class ManualInvoiceCalculatorTests
{
    [Fact]
    public void Mixed_vat_discounts_and_fractional_quantities_have_consistent_totals()
    {
        var result = ManualInvoiceCalculator.Calculate("EUR",
        [
            new() { Description = "Advies", Quantity = 2.5m, UnitPrice = 40, DiscountPercentage = 10, VatPercentage = 21 },
            new() { Description = "Boeken", Quantity = 2, UnitPrice = 15, DiscountAmount = 5, VatPercentage = 9 },
            new() { Description = "Nultarief", UnitPrice = 10, VatPercentage = 0 },
            new() { Description = "Vrijgesteld", UnitPrice = 20, VatTreatment = "exempt", VatPercentage = 0, VatInformation = "Vrijstelling op bronfactuur" },
            new() { Description = "Verlegd", UnitPrice = 30, VatTreatment = "reverse_charge", VatPercentage = 0, VatInformation = "Btw verlegd" }
        ]);
        Assert.True(result.IsValid);
        Assert.Equal(190, result.SubtotalBeforeDiscount);
        Assert.Equal(15, result.TotalDiscount);
        Assert.Equal(175, result.TotalExcludingVat);
        Assert.Equal(21.15m, result.TotalVat);
        Assert.Equal(196.15m, result.TotalIncludingVat);
        Assert.Equal(5, result.VatGroups.Length);
        Assert.Equal(result.TotalIncludingVat, result.Lines.Sum(line => line.GrossAmount));
        Assert.Equal(result.TotalVat, result.VatGroups.Sum(group => group.VatAmount));
    }

    [Theory]
    [InlineData("EUR", 0.03, 0.01)]
    [InlineData("KWD", 0.003, 0.001)]
    public void Rounding_happens_per_line_with_away_from_zero_midpoints(string currency, double price, double expectedVat)
    {
        var result = ManualInvoiceCalculator.Calculate(currency, [new() { Description = "Afronding", UnitPrice = (decimal)price, VatPercentage = 21 }]);
        Assert.True(result.IsValid);
        Assert.Equal((decimal)expectedVat, result.TotalVat);
        var halves = ManualInvoiceCalculator.Calculate("EUR", [new() { Description = "Halve cent", UnitPrice = .005m, VatPercentage = 0 }]);
        Assert.Equal(.01m, halves.TotalIncludingVat);
    }

    [Fact]
    public void Invalid_lines_return_field_errors_without_throwing_or_silently_rounding_input()
    {
        var invalid = new List<(ManualInvoiceLineInput Line, string Field)>
        {
            (new() { UnitPrice = 1, Description = "" }, "description"),
            (new() { UnitPrice = 1, Description = "x", Quantity = -1 }, "quantity"),
            (new() { UnitPrice = null, Description = "x" }, "unitPrice"),
            (new() { UnitPrice = -1, Description = "x" }, "unitPrice"),
            (new() { UnitPrice = 1, Description = "x", Quantity = .0000001m }, "quantity"),
            (new() { UnitPrice = .0000001m, Description = "x" }, "unitPrice"),
            (new() { UnitPrice = 1, Description = "x", DiscountPercentage = 10, DiscountAmount = .1m }, "discountAmount"),
            (new() { UnitPrice = 1, Description = "x", DiscountAmount = 2 }, "discountAmount"),
            (new() { UnitPrice = 1, Description = "x", DiscountAmount = .001m }, "discountAmount"),
            (new() { UnitPrice = 1, Description = "x", VatPercentage = 25 }, "vatPercentage"),
            (new() { UnitPrice = 1, Description = "x", VatTreatment = "exempt", VatPercentage = 0 }, "vatInformation")
        };
        foreach (var (line, field) in invalid)
            Assert.Contains("lines[0]." + field, ManualInvoiceCalculator.Calculate("EUR", [line]).Errors.Keys);
        Assert.Contains("lines", ManualInvoiceCalculator.Calculate("EUR", []).Errors.Keys);
        Assert.Contains("currency", ManualInvoiceCalculator.Calculate("FAKE", [new()]).Errors.Keys);
        Assert.Contains("lines", ManualInvoiceCalculator.Calculate("EUR", Enumerable.Range(0, 201).Select(_ => new ManualInvoiceLineInput()).ToArray()).Errors.Keys);
        Assert.Contains("lines", ManualInvoiceCalculator.Calculate("EUR", [new() { Description = "Gratis", UnitPrice = 0 }]).Errors.Keys);
        Assert.True(ManualInvoiceCalculator.Calculate("EUR", Enumerable.Range(0, 200).Select(_ => new ManualInvoiceLineInput { Description = "Regel", UnitPrice = 1 }).ToArray()).IsValid);
    }
}
