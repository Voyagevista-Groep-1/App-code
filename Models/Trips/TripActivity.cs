
namespace tui.Models;

public class TripActivity
{
    public int TripId { get; set; }

    public int ActivityId { get; set; }

    // Navigation properties
    public Trip Trip { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
}
