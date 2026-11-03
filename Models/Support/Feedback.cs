using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class Feedback
{
    [Key]
    public int FeedbackId { get; set; }

    public int TicketId { get; set; }

    // 1-5 stars (integer(1,5) in the first ERD version)
    [Range(1, 5)]
    public byte StarRating { get; set; }

    public string? Description { get; set; }

    // Navigation properties
    public ComplaintTicket Ticket { get; set; } = null!;
}
