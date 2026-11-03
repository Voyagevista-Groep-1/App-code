using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class TicketSolution
{
    [Key]
    public int SolutionId { get; set; }

    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<ComplaintTicket> ComplaintTickets { get; set; } = [];
}
