using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class Transport
{
    [Key]
    public int TransportId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Trip> Trips { get; set; } = [];
}
