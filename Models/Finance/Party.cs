using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class Party
{
    [Key]
    public int PartyId { get; set; }

    public int? ClientId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string PartyType { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? VatNumber { get; set; }

    // Navigation properties
    public Client? Client { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = [];
}
