using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class InvoiceLine
{
    [Key]
    public int InvoiceLineId { get; set; }

    public int InvoiceId { get; set; }

    public int AccountId { get; set; }

    public int? TaxRateId { get; set; }

    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;

    [Precision(10, 2)]
    public decimal Quantity { get; set; }

    [Precision(10, 2)]
    public decimal Price { get; set; }

    // Navigation properties
    public Invoice Invoice { get; set; } = null!;
    public Account Account { get; set; } = null!;
    public TaxRate? TaxRate { get; set; }
}
