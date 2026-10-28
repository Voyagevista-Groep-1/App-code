using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class AppUser
{
    [Key]
    public int UserId { get; set; }

    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserRole { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Client? Client { get; set; }
    public ICollection<ComplaintTicket> AssignedTickets { get; set; } = [];
}
