using System.Data;
using Microsoft.EntityFrameworkCore;
using tui.Data;
using tui.Models;

namespace tui.Services;

public record ManagedUser(int UserId, string Email, string[] Roles);
public record UpdateUserRoles(string[]? Roles);
public record RoleUpdateResult(int Status, ManagedUser? User = null, string? Error = null);

public class UserRoleService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<ManagedUser[]> GetUsersAsync(string? search, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.AppUsers.AsNoTracking().Include(user => user.UserRoles).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var email = search.Trim().ToUpperInvariant();
            query = query.Where(user => user.Email.ToUpper().Contains(email));
        }
        var users = await query.OrderBy(user => user.Email).ToArrayAsync(cancellationToken);
        return users.Select(ToManagedUser).ToArray();
    }

    public async Task<string[]> GetRolesAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Roles.AsNoTracking().OrderBy(role => role.Name).Select(role => role.Name).ToArrayAsync(cancellationToken);
    }

    public async Task<RoleUpdateResult> UpdateAsync(int actorId, int userId, string[]? requested, CancellationToken cancellationToken)
    {
        if (requested is null || requested.Length > AccessPolicies.Roles.Length ||
            requested.Any(role => role is null || !AccessPolicies.Roles.Contains(role, StringComparer.Ordinal)))
            return new(400, Error: "Selecteer uitsluitend bestaande rollen.");

        var roles = requested.Distinct(StringComparer.Ordinal).ToArray();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // All role changes lock this one catalogue row first. Concurrent requests cannot
        // both observe another administrator and then remove the last two administrators.
        await db.Roles.FromSqlInterpolated($"SELECT * FROM Roles WHERE Name = {AccessPolicies.Administrator} FOR UPDATE")
            .ToListAsync(cancellationToken);

        if (!await db.UserRoles.AnyAsync(r => r.UserId == actorId && r.RoleName == AccessPolicies.Administrator, cancellationToken))
            return new(403, Error: "Je bent niet bevoegd om gebruikersrollen te wijzigen.");

        var user = await db.AppUsers.Include(u => u.UserRoles).SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null)
            return new(404, Error: "Deze gebruiker bestaat niet meer. Vernieuw het overzicht.");

        if (await db.Roles.CountAsync(role => roles.Contains(role.Name), cancellationToken) != roles.Length)
            return new(400, Error: "Een geselecteerde rol bestaat niet meer. Vernieuw de pagina.");

        if (user.UserRoles.Any(r => r.RoleName == AccessPolicies.Administrator) && !roles.Contains(AccessPolicies.Administrator) &&
            !await db.UserRoles.AnyAsync(r => r.UserId != userId && r.RoleName == AccessPolicies.Administrator, cancellationToken))
            return new(409, Error: "De laatste beheerdersrol kan niet worden verwijderd. Wijs eerst een andere beheerder toe.");

        var removed = user.UserRoles.Where(r => !roles.Contains(r.RoleName)).ToArray();
        db.UserRoles.RemoveRange(removed);
        foreach (var assignment in removed) user.UserRoles.Remove(assignment);
        foreach (var role in roles.Where(role => !user.UserRoles.Any(r => r.RoleName == role)))
            user.UserRoles.Add(new UserRoleAssignment { UserId = userId, RoleName = role });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(200, ToManagedUser(user));
    }

    private static ManagedUser ToManagedUser(AppUser user) =>
        new(user.UserId, user.Email, user.UserRoles.Select(role => role.RoleName).Order().ToArray());
}
