using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class TripDestination
{
    [Key]
    public int TripDestinationId { get; set; }

    public int TripId { get; set; }

    public int DestinationId { get; set; }

    public int? StopOrder { get; set; }

    // Navigation properties
    public Trip Trip { get; set; } = null!;
    public Destination Destination { get; set; } = null!;
}
