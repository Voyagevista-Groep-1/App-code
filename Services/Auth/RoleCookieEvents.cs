using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace tui.Services;

public sealed class RoleCookieEvents(IUserSession session, ILogger<RoleCookieEvents> logger) : CookieAuthenticationEvents
{
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = StatusCodes.Status403Forbidden;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        try
        {
            var principal = await session.RefreshAsync(context.Principal!, context.HttpContext.RequestAborted);
            if (principal.Identity?.IsAuthenticated == true)
            {
                context.ReplacePrincipal(principal);
                return;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Gebruikersrollen konden niet worden geladen; sessie geweigerd.");
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
