using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;

namespace tui.Services;

public static class UserManagementEndpoints
{
    public static IEndpointRouteBuilder MapUserManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization(AccessPolicies.Administrator);
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                context.HttpContext.RequestServices.GetRequiredService<ILogger<UserRoleService>>()
                    .LogError(exception, "Gebruikersbeheer kon de aanvraag niet verwerken.");
                return Results.Json(new { error = "De wijziging kon niet worden verwerkt. Probeer het opnieuw of vernieuw het overzicht." }, statusCode: 503);
            }
        });
        group.MapGet("/antiforgery", (IAntiforgery antiforgery, HttpContext context) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        });
        group.MapGet("/users", async (string? search, UserRoleService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetUsersAsync(search, cancellationToken)));
        group.MapGet("/roles", async (UserRoleService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetRolesAsync(cancellationToken)));
        group.MapPut("/users/{userId:int}/roles", async (int userId, UpdateUserRoles request, UserRoleService service,
            IAntiforgery antiforgery, HttpContext context, CancellationToken cancellationToken) =>
        {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.Json(new { error = "De beveiligingscontrole is verlopen. Vernieuw de pagina en probeer opnieuw." }, statusCode: 400); }
            if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
                return Results.Forbid();
            var result = await service.UpdateAsync(actorId, userId, request.Roles, cancellationToken);
            return result.Status == 200 ? Results.Ok(result.User) : Results.Json(new { error = result.Error }, statusCode: result.Status);
        });
        return app;
    }
}
