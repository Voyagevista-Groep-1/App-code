using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class InvoiceLine
{
    [Key]
    public int InvoiceLineId { get; set; }

    public int InvoiceId { get; set; }

    public int? AccountId { get; set; }

    public int? TaxRateId { get; set; }

    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;

    [Precision(18, 6)]
    public decimal Quantity { get; set; }

    [Precision(18, 6)]
    public decimal Price { get; set; }

    public int Position { get; set; }
    [MaxLength(30)] public string Unit { get; set; } = "";
    [Precision(9, 6)] public decimal? DiscountPercentage { get; set; }
    [Precision(14, 4)] public decimal? DiscountFixedAmount { get; set; }
    [MaxLength(20)] public string VatTreatment { get; set; } = "regular";
    [Precision(5, 2)] public decimal VatPercentage { get; set; }
    [MaxLength(1000)] public string? VatInformation { get; set; }
    [Precision(14, 4)] public decimal BaseAmount { get; set; }
    [Precision(14, 4)] public decimal DiscountAmount { get; set; }
    [Precision(14, 4)] public decimal NetAmount { get; set; }
    [Precision(14, 4)] public decimal VatAmount { get; set; }
    [Precision(14, 4)] public decimal GrossAmount { get; set; }

    // Navigation properties
    public Invoice Invoice { get; set; } = null!;
    public Account? Account { get; set; }
    public TaxRate? TaxRate { get; set; }
}
