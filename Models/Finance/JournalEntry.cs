using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class JournalEntry
{
    [Key]
    public int JournalEntryId { get; set; }

    public int? InvoiceId { get; set; }

    public DateOnly EntryDate { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }

    // Navigation properties
    public Invoice? Invoice { get; set; }
    public ICollection<JournalEntryLine> JournalEntryLines { get; set; } = [];
}
