using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class BookingSLA
{
    [Key]
    public int BookingSLAId { get; set; }

    public int BookingId { get; set; }

    public int SLAId { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime DeadlineAt { get; set; }

    [MaxLength(30)]
    public string? Status { get; set; }

    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public Booking Booking { get; set; } = null!;
    public SLA SLA { get; set; } = null!;
}
