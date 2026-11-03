using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class Booking
{
    [Key]
    public int BookingId { get; set; }

    public int ClientId { get; set; }

    public int TripId { get; set; }

    public DateTime BookingDate { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = string.Empty;

    // Navigation properties
    public Client Client { get; set; } = null!;
    public Trip Trip { get; set; } = null!;
    public ICollection<BookingItem> BookingItems { get; set; } = [];
    public ICollection<Traveler> Travelers { get; set; } = [];
    public ICollection<ComplaintTicket> ComplaintTickets { get; set; } = [];
    public ICollection<BookingSLA> BookingSLAs { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
}
