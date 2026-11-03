using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class TicketStatus
{
    [Key]
    public int StatusId { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<ComplaintTicket> ComplaintTickets { get; set; } = [];
}
