using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class Traveler
{
    [Key]
    public int TravelerId { get; set; }

    public int TravelerTypeId { get; set; }

    public int BookingId { get; set; }

    public int? ClientId { get; set; }

    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    // Navigation properties
    public TravelerType TravelerType { get; set; } = null!;
    public Booking Booking { get; set; } = null!;
    public Client? Client { get; set; }
}
