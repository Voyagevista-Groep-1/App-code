using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class Surcharge
{
    [Key]
    public int SurchargeId { get; set; }

    public int SurchargeTypeId { get; set; }

    public int TripId { get; set; }

    [Precision(10, 2)]
    public decimal Amount { get; set; }

    // Navigation properties
    public SurchargeType SurchargeType { get; set; } = null!;
    public Trip Trip { get; set; } = null!;
}
