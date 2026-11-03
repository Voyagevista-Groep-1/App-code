using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class SLA
{
    [Key]
    public int SLAId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public int? WarningMinutes { get; set; }

    public bool Active { get; set; }

    // Navigation properties
    public ICollection<ComplaintTicket> ComplaintTickets { get; set; } = [];
    public ICollection<BookingSLA> BookingSLAs { get; set; } = [];
}
