using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class Invoice
{
    [Key]
    public int InvoiceId { get; set; }

    public int PartyId { get; set; }

    public int? BookingId { get; set; }

    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [MaxLength(20)]
    public string InvoiceType { get; set; } = string.Empty;

    public DateOnly InvoiceDate { get; set; }

    public DateOnly? DueDate { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = string.Empty;

    [Precision(14, 4)]
    public decimal TotalAmount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    [MaxLength(30)]
    public string WorkflowStatus { get; set; } = "concept";

    public int? CreatedByUserId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    [MaxLength(1000)]
    public string? ReviewComment { get; set; }
    [ConcurrencyCheck]
    public long Version { get; set; }
    public bool IsManualRegistration { get; set; }
    public ManualInvoiceRegistration? ManualRegistration { get; set; }

    // Navigation properties
    public Party Party { get; set; } = null!;
    public Booking? Booking { get; set; }
    public ICollection<InvoiceLine> InvoiceLines { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<JournalEntry> JournalEntries { get; set; } = [];
}
