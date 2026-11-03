using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class Discount
{
    [Key]
    public int DiscountId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Precision(5, 2)]
    public decimal Percentage { get; set; }

    // Navigation properties
    public ICollection<Trip> Trips { get; set; } = [];
}
