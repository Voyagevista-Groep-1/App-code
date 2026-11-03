using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class SurchargeType
{
    [Key]
    public int SurchargeTypeId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Surcharge> Surcharges { get; set; } = [];
}
