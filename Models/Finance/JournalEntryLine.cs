using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class JournalEntryLine
{
    [Key]
    public int JournalEntryLineId { get; set; }

    public int JournalEntryId { get; set; }

    public int AccountId { get; set; }

    [Precision(10, 2)]
    public decimal Debit { get; set; }

    [Precision(10, 2)]
    public decimal Credit { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }

    // Navigation properties
    public JournalEntry JournalEntry { get; set; } = null!;
    public Account Account { get; set; } = null!;
}
