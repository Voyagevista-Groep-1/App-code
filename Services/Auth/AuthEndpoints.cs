using Microsoft.AspNetCore.Mvc;

namespace tui.Services;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async (AuthService auth, [FromForm] string email, [FromForm] string password) =>
        {
            var result = await auth.LoginAsync(email, password);

            return result.Success
                ? Results.LocalRedirect("/")
                : Results.LocalRedirect($"/auth/login?error={Uri.EscapeDataString(result.Error ?? "Inloggen mislukt.")}");
        });

        group.MapPost("/logout", async (AuthService auth) =>
        {
            await auth.LogoutAsync();
            return Results.LocalRedirect("/auth/login");
        });

        return app;
    }
}