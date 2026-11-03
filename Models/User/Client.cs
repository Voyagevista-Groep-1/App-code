using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class Client
{
    [Key]
    public int ClientId { get; set; }

    public int? UserId { get; set; }

    public int? ClientTypeId { get; set; }

    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    // Navigation properties
    public AppUser? User { get; set; }
    public ClientType? ClientType { get; set; }
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<Traveler> Travelers { get; set; } = [];
    public ICollection<ComplaintTicket> ComplaintTickets { get; set; } = [];
    public Party? Party { get; set; }
}
