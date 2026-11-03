using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class BookingItem
{
    [Key]
    public int BookingItemId { get; set; }

    public int BookingId { get; set; }

    public int TariffId { get; set; }

    public int Quantity { get; set; }

    [Precision(10, 2)]
    public decimal Price { get; set; }

    // Navigation properties
    public Booking Booking { get; set; } = null!;
    public Tariff Tariff { get; set; } = null!;
}
