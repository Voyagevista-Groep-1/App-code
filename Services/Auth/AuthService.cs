using System.Net.Mail;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using tui.Data;
using tui.Models;

namespace tui.Services;

public record AuthResult(bool Success, string? Error = null);

public class AuthService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IHttpContextAccessor httpContextAccessor,
    IPasswordHasher<AppUser> passwordHasher)
{
    private const string AdminEmail = "ADMIN@ADMIN.COM";

    public const string PasswordRule = "Het wachtwoord moet minimaal 8 tekens bevatten, met minstens één letter en één cijfer.";
    private static readonly Regex PasswordRegex = new(@"^(?=.*[A-Za-z])(?=.*[0-9]).{8,}$", RegexOptions.CultureInvariant);

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 200 && MailAddress.TryCreate(email.Trim(), out var a) && a.Address == email.Trim();

    public async Task<AuthResult> RegisterAsync(string firstname, string lastname, string email, string password)
    {
        firstname = firstname.Trim();
        lastname = lastname.Trim();

        if (firstname.Length is 0 or > 50 || lastname.Length is 0 or > 50)
        {
            return new AuthResult(false, "Vul je voor- en achternaam in (maximaal 50 tekens).");
        }

        if (!IsValidEmail(email))
        {
            return new AuthResult(false, "Vul een geldig e-mailadres in.");
        }

        if (!PasswordRegex.IsMatch(password ?? string.Empty))
        {
            return new AuthResult(false, PasswordRule);
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        email = email.Trim().ToUpperInvariant();

        if (await db.AppUsers.AnyAsync(u => u.Email == email))
        {
            return new AuthResult(false, "Dit e-mailadres is al in gebruik.");
        }

        var user = new AppUser { FirstName = firstname, LastName = lastname, Email = email, PasswordHash = "", CreatedAt = DateTime.UtcNow };
        user.PasswordHash = passwordHasher.HashPassword(user, password!);

        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        return new AuthResult(true);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        if (!IsValidEmail(email) || string.IsNullOrEmpty(password))
        {
            return new AuthResult(false, "Vul een geldig e-mailadres en wachtwoord in.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        email = email.Trim().ToUpperInvariant();

        var user = await db.AppUsers.SingleOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            return new AuthResult(false, "Ongeldig e-mailadres of ongeldig wachtwoord.");
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (result == PasswordVerificationResult.Failed)
        {
            return new AuthResult(false, "Ongeldig e-mailadres of ongeldig wachtwoord.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
        };

        // TODO: remove this, this is temporary until we have proper roles
        if (user.Email == AdminEmail)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        if (httpContextAccessor.HttpContext is { } httpContext)
        {
            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        return new AuthResult(true);
    }

    public async Task LogoutAsync()
    {
        if (httpContextAccessor.HttpContext is { } httpContext)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}