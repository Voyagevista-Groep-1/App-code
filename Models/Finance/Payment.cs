using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class Payment
{
    [Key]
    public int PaymentId { get; set; }

    public int InvoiceId { get; set; }

    public DateOnly PaymentDate { get; set; }

    [Precision(10, 2)]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string? Method { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    // Navigation properties
    public Invoice Invoice { get; set; } = null!;
}
