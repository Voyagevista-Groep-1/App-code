using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class TicketAction
{
    [Key]
    public int TicketActionId { get; set; }

    public int TicketId { get; set; }

    [MaxLength(255)]
    public string Action { get; set; } = string.Empty;

    public DateTime ActionDate { get; set; }

    // Navigation properties
    public ComplaintTicket Ticket { get; set; } = null!;
}
