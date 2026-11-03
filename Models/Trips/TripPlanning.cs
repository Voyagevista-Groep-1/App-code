using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class TripPlanning
{
    [Key]
    public int TripPlanningId { get; set; }

    public int TripId { get; set; }

    public int? TripGuideId { get; set; }

    public DateTime PlanningDate { get; set; }

    // Navigation properties
    public Trip Trip { get; set; } = null!;
    public TripGuide? TripGuide { get; set; }
}
