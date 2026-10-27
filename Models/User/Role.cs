using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class Role
{
    [Key, MaxLength(32)]
    public string Name { get; set; } = string.Empty;
    public ICollection<UserRoleAssignment> UserRoles { get; set; } = [];
}

public class UserRoleAssignment
{
    public int UserId { get; set; }
    [MaxLength(32)]
    public string RoleName { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
