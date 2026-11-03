using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class TaxRate
{
    [Key]
    public int TaxRateId { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Precision(5, 2)]
    public decimal Percentage { get; set; }

    // Navigation properties
    public ICollection<InvoiceLine> InvoiceLines { get; set; } = [];
}
