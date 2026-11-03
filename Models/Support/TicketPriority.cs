using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class TicketPriority
{
    [Key]
    public int PriorityId { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<ComplaintTicket> ComplaintTickets { get; set; } = [];
}
