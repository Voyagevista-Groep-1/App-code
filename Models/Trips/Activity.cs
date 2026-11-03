using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class Activity
{
    [Key]
    public int ActivityId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<TripActivity> TripActivities { get; set; } = [];
}
