using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using tui.Services;

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

    public ICollection<UserRoleAssignment> UserRoles { get; set; } = [];

    // Compatibility for constructing principals; persisted assignments live in UserRoles.
    [NotMapped]
    public string UserRole
    {
        get => string.Join(',', UserRoles.Select(assignment => assignment.RoleName));
        set => UserRoles = AccessPolicies.ParseRoles(value)
            .Select(role => new UserRoleAssignment { RoleName = role }).ToList();
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Client? Client { get; set; }
    public ICollection<ComplaintTicket> AssignedTickets { get; set; } = [];
}
