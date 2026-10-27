using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using tui.Data;
using tui.Models;

namespace tui.Services;

public interface IUserSession
{
    Task<ClaimsPrincipal> RefreshAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}

public class UserSession(IDbContextFactory<ApplicationDbContext> dbFactory) : IUserSession
{
    public const string ExpiresClaim = "voyagevista:expires";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    public static ClaimsPrincipal CreatePrincipal(AppUser user, DateTimeOffset expires)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(ExpiresClaim, expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        };
        claims.AddRange(AccessPolicies.ParseRoles(user.UserRole).Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public async Task<ClaimsPrincipal> RefreshAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !long.TryParse(principal.FindFirstValue(ExpiresClaim), out var expires) ||
            expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.AppUsers.AsNoTracking().Include(u => u.UserRoles)
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        return user is null ? new ClaimsPrincipal(new ClaimsIdentity()) : CreatePrincipal(user, DateTimeOffset.FromUnixTimeSeconds(expires));
    }
}
