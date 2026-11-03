using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class Tariff
{
    [Key]
    public int TariffId { get; set; }

    public int TripId { get; set; }

    public int TravelerTypeId { get; set; }

    [Precision(10, 2)]
    public decimal Amount { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidUntil { get; set; }

    // Navigation properties
    public Trip Trip { get; set; } = null!;
    public TravelerType TravelerType { get; set; } = null!;
    public ICollection<BookingItem> BookingItems { get; set; } = [];
}
