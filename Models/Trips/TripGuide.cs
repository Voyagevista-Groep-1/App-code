using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class TripGuide
{
    [Key]
    public int TripGuideId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? ZipCode { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    // Navigation properties
    public ICollection<TripPlanning> TripPlannings { get; set; } = [];
}
