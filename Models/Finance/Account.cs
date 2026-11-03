using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class Account
{
    [Key]
    public int AccountId { get; set; }

    [MaxLength(20)]
    public string AccountNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string AccountType { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<InvoiceLine> InvoiceLines { get; set; } = [];
    public ICollection<JournalEntryLine> JournalEntryLines { get; set; } = [];
}
