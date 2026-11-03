using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class Destination
{
    [Key]
    public int DestinationId { get; set; }

    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(100)]
    public string? Climate { get; set; }

    // Navigation properties
    public ICollection<TripDestination> TripDestinations { get; set; } = [];
}
