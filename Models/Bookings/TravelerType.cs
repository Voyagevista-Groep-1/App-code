using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class TravelerType
{
    [Key]
    public int TravelerTypeId { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Tariff> Tariffs { get; set; } = [];
    public ICollection<Traveler> Travelers { get; set; } = [];
}
