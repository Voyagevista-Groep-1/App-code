using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class ClientType
{
    [Key]
    public int ClientTypeId { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Client> Clients { get; set; } = [];
}
