using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class Trip
{
    [Key]
    public int TripId { get; set; }

    public int? DiscountId { get; set; }

    public int? TransportId { get; set; }

    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Precision(10, 2)]
    public decimal Amount { get; set; }

    // Navigation properties
    public Discount? Discount { get; set; }
    public Transport? Transport { get; set; }
    public ICollection<TripDestination> TripDestinations { get; set; } = [];
    public ICollection<TripActivity> TripActivities { get; set; } = [];
    public ICollection<TripPlanning> TripPlannings { get; set; } = [];
    public ICollection<Surcharge> Surcharges { get; set; } = [];
    public ICollection<Tariff> Tariffs { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
}
