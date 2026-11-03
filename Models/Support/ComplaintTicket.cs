using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class ComplaintTicket
{
    [Key]
    public int TicketId { get; set; }

    public int ClientId { get; set; }

    public int? StaffUserId { get; set; }

    public int? BookingId { get; set; }

    public int StatusId { get; set; }

    public int PriorityId { get; set; }

    public int? SolutionId { get; set; }

    public int? SLAId { get; set; }

    [MaxLength(150)]
    public string Topic { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime TicketDate { get; set; }

    // Navigation properties
    public Client Client { get; set; } = null!;
    public Booking? Booking { get; set; }
    public TicketStatus Status { get; set; } = null!;
    public TicketPriority Priority { get; set; } = null!;
    public TicketSolution? Solution { get; set; }
    public SLA? SLA { get; set; }
    public AppUser? StaffUser { get; set; }
    public ICollection<TicketAction> TicketActions { get; set; } = [];
    public ICollection<Feedback> Feedbacks { get; set; } = [];
}
