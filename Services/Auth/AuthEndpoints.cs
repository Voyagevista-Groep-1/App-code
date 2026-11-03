using Microsoft.AspNetCore.Mvc;

namespace tui.Services;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async (AuthService auth, [FromForm] string? email, [FromForm] string? password, [FromForm] string? returnUrl) =>
        {
            var target = IsLocalPath(returnUrl) ? returnUrl! : "/";
            var result = await auth.LoginAsync(email ?? "", password ?? "");

            return result.Success
                ? Results.LocalRedirect(target)
                : Results.LocalRedirect($"/auth/login?error={Uri.EscapeDataString(result.Error ?? "Inloggen mislukt.")}&returnUrl={Uri.EscapeDataString(target)}");
        });

        group.MapPost("/logout", async (AuthService auth) =>
        {
            await auth.LogoutAsync();
            return Results.LocalRedirect("/auth/login");
        });

        return app;
    }

    private static bool IsLocalPath(string? url) =>
        !string.IsNullOrEmpty(url) && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
}