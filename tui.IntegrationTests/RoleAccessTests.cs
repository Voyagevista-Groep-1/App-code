using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using tui.Data;
using tui.Components;
using tui.Components.Layout;
using tui.Models;
using tui.Services;
using Xunit;

namespace tui.IntegrationTests;

public class RoleAccessTests
{
    [Theory]
    [InlineData("Finance", "/finance", "/sales")]
    [InlineData("Sales", "/sales", "/finance")]
    [InlineData("Finance,Sales", "/finance", "/it")]
    [InlineData("", "/", "/finance")]
    public async Task Direct_urls_enforce_department_policy(string roles, string allowed, string denied)
    {
        await using var host = await TestApplication.StartAsync(roles);
        using var client = host.App.GetTestClient();
        await host.LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(allowed)).StatusCode);
        var response = await client.GetAsync(denied);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/access-denied", response.Headers.Location!.ToString());
        Assert.Contains("Toegang geweigerd", await client.GetStringAsync("/access-denied"));
    }

    [Theory]
    [InlineData("Beheerder")]
    [InlineData("Admin")]
    [InlineData("Finance,Sales")]
    public async Task Multiple_roles_and_administrator_can_open_both_departments(string roles)
    {
        await using var host = await TestApplication.StartAsync(roles);
        using var client = host.App.GetTestClient();
        await host.LoginAsync(client);
        foreach (var path in roles == "Finance,Sales" ? new[] { "/finance", "/sales" } : new[] { "/admin", "/finance", "/sales", "/marketing", "/it", "/customerservice", "/productdevelopment" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Anonymous_users_are_redirected_to_login()
    {
        await using var host = await TestApplication.StartAsync("");
        using var client = host.App.GetTestClient();
        foreach (var path in new[] { "/", "/finance", "/sales", "/admin" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("http://localhost/auth/login", response.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task Cookie_reload_uses_current_roles_and_fails_closed()
    {
        await using var host = await TestApplication.StartAsync("Finance");
        using var client = host.App.GetTestClient();
        await host.LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/finance")).StatusCode);
        host.Session.Roles = "Sales";
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/finance")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/sales")).StatusCode);
        host.Session.Fail = true;
        var failed = await client.GetAsync("/sales");
        Assert.StartsWith("http://localhost/auth/login", failed.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("Finance", true, false)]
    [InlineData("Sales", false, true)]
    [InlineData("Finance,Sales", true, true)]
    [InlineData("Beheerder", true, true)]
    [InlineData("", false, false)]
    [InlineData("Unknown", false, false)]
    public async Task Navigation_shows_only_authorized_links(string roles, bool finance, bool sales)
    {
        await using var host = await TestApplication.StartAsync(roles);
        using var scope = host.App.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        ((IHostEnvironmentAuthenticationStateProvider)provider).SetAuthenticationState(Task.FromResult(new AuthenticationState(host.Session.Principal())));
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<Navbar>()).ToHtmlString());
        Assert.Equal(finance, html.Contains("href=\"/finance\""));
        Assert.Equal(sales, html.Contains("href=\"/sales\""));
        Assert.Contains("href=\"/\"", html);
        Assert.Contains("Uitloggen", html);
    }

    [Fact]
    public async Task Interactive_state_refresh_updates_roles_and_expires()
    {
        var session = new TestSession { Roles = "Finance" };
        using var provider = new InspectableProvider(session);
        var original = new AuthenticationState(session.Principal());
        provider.SetAuthenticationState(Task.FromResult(original));
        session.Roles = "Sales";
        Assert.True(await provider.Revalidate(original));
        var updated = await provider.GetAuthenticationStateAsync();
        Assert.True(updated.User.IsInRole("Sales"));
        Assert.False(updated.User.IsInRole("Finance"));
        session.Expired = true;
        Assert.False(await provider.Revalidate(updated));
    }

    [Fact]
    public async Task Expired_session_is_rejected_even_when_roles_are_present()
    {
        // An expired principal is rejected before opening a database connection.
        var session = new UserSession(null!);
        var principal = UserSession.CreatePrincipal(new AppUser { UserId = 1, UserRole = "Beheerder" }, DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.False((await session.RefreshAsync(principal, default)).Identity!.IsAuthenticated);
    }

    [Theory]
    [InlineData("Finance", true)]
    [InlineData("Sales", false)]
    [InlineData("Beheerder", true)]
    [InlineData("Finance,Sales", true)]
    [InlineData("", false)]
    public async Task Department_controls_are_hidden_without_required_role(string roles, bool visible)
    {
        await using var host = await TestApplication.StartAsync(roles);
        using var scope = host.App.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        ((IHostEnvironmentAuthenticationStateProvider)provider).SetAuthenticationState(Task.FromResult(new AuthenticationState(host.Session.Principal())));
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(RoleView.Policy)] = AccessPolicies.Finance,
            [nameof(RoleView.ChildContent)] = (RenderFragment)(builder => builder.AddMarkupContent(0, "<button>Finance action</button>"))
        });
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<RoleView>(parameters)).ToHtmlString());
        Assert.Equal(visible, html.Contains("Finance action"));
    }

    [Fact]
    public async Task Interactive_route_and_navigation_update_when_finance_role_is_revoked()
    {
        await using var host = await TestApplication.StartAsync("Finance");
        using var scope = host.App.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        var environmentProvider = (IHostEnvironmentAuthenticationStateProvider)provider;
        environmentProvider.SetAuthenticationState(Task.FromResult(new AuthenticationState(host.Session.Principal())));
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(AuthorizeRouteView.RouteData)] = new RouteData(typeof(tui.Components.Pages.Finance.Finance), new Dictionary<string, object?>()),
            [nameof(AuthorizeRouteView.NotAuthorized)] = (RenderFragment<AuthenticationState>)(_ => builder => builder.AddContent(0, "Toegang geweigerd"))
        });
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var route = await renderer.RenderComponentAsync<AuthorizeRouteView>(parameters);
            var navbar = await renderer.RenderComponentAsync<Navbar>();
            Assert.DoesNotContain("Toegang geweigerd", route.ToHtmlString());
            Assert.Contains("href=\"/finance\"", navbar.ToHtmlString());
            host.Session.Roles = "Sales";
            environmentProvider.SetAuthenticationState(Task.FromResult(new AuthenticationState(host.Session.Principal())));
            await Task.Yield();
            Assert.Contains("Toegang geweigerd", route.ToHtmlString());
            Assert.DoesNotContain("href=\"/finance\"", navbar.ToHtmlString());
            Assert.Contains("href=\"/sales\"", navbar.ToHtmlString());
        });
    }

    [Fact]
    public async Task Logout_clears_cookie_and_protected_pages_require_login()
    {
        await using var host = await TestApplication.StartAsync("Finance");
        using var client = host.App.GetTestClient();
        await host.LoginAsync(client);
        var home = await client.GetAsync("/");
        var html = await home.Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var cookies = client.DefaultRequestHeaders.GetValues("Cookie").Single();
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookies + "; " + home.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
        var logout = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), value => value.Contains(".AspNetCore.Cookies=;"));
        client.DefaultRequestHeaders.Remove("Cookie");
        var denied = await client.GetAsync("/finance");
        Assert.StartsWith("http://localhost/auth/login", denied.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData(" finance ; SALES,Finance,unknown", "Finance,Sales")]
    [InlineData("Admin", "Beheerder")]
    [InlineData(null, "")]
    public void Roles_are_normalized_and_unknown_roles_are_ignored(string? stored, string expected) =>
        Assert.Equal(expected, string.Join(',', AccessPolicies.ParseRoles(stored)));

    private sealed class InspectableProvider(IUserSession session)
        : RoleAuthenticationStateProvider(LoggerFactory.Create(_ => { }), session)
    {
        public Task<bool> Revalidate(AuthenticationState state) => ValidateAuthenticationStateAsync(state, default);
    }

    private sealed class TestSession : IUserSession
    {
        public string Roles { get; set; } = "";
        public bool Fail { get; set; }
        public bool Expired { get; set; }
        public ClaimsPrincipal Principal() => UserSession.CreatePrincipal(new AppUser { UserId = 1, Email = "test@example.invalid", UserRole = Roles }, DateTimeOffset.UtcNow.AddHours(1));
        public Task<ClaimsPrincipal> RefreshAsync(ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            Fail ? throw new InvalidOperationException("Role store unavailable") : Task.FromResult(Expired ? new ClaimsPrincipal(new ClaimsIdentity()) : Principal());
    }

    private sealed class TestApplication(WebApplication app, TestSession session) : IAsyncDisposable
    {
        public WebApplication App => app;
        public TestSession Session => session;
        public static async Task<TestApplication> StartAsync(string roles)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(App).Assembly.FullName, EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContextFactory<ApplicationDbContext>(options => options.UseMySql("Server=unused;Database=unused", new MySqlServerVersion(new Version(8, 0, 0))));
            builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<AuthenticationStateProvider, RoleAuthenticationStateProvider>();
            var session = new TestSession { Roles = roles };
            builder.Services.AddSingleton<IUserSession>(session);
            builder.Services.AddScoped<RoleCookieEvents>();
            builder.Services.AddAuthorization(AccessPolicies.Configure);
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
            {
                options.LoginPath = "/auth/login";
                options.AccessDeniedPath = "/access-denied";
                options.EventsType = typeof(RoleCookieEvents);
            });
            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.MapGet("/test/signin", async (HttpContext context) => await context.SignInAsync(session.Principal()));
            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
            app.MapAuthEndpoints();
            await app.StartAsync();
            return new TestApplication(app, session);
        }
        public async Task LoginAsync(HttpClient client)
        {
            var response = await client.GetAsync("/test/signin");
            client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
        }
        public ValueTask DisposeAsync() => app.DisposeAsync();
    }
}
