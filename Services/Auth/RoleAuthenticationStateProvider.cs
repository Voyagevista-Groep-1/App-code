using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace tui.Services;

public class RoleAuthenticationStateProvider(ILoggerFactory loggerFactory, IUserSession session)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // The base provider signs out on a failed check, including database errors.
        var refreshed = await session.RefreshAsync(authenticationState.User, cancellationToken);
        if (refreshed.Identity?.IsAuthenticated != true)
            return false;

        var previous = authenticationState.User.Claims.Select(c => (c.Type, c.Value));
        if (!previous.SequenceEqual(refreshed.Claims.Select(c => (c.Type, c.Value))))
            SetAuthenticationState(Task.FromResult(new AuthenticationState(refreshed)));

        return true;
    }
}
