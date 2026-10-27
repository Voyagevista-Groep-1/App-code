using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using tui.Components;
using tui.Data;
using tui.Models;
using tui.Services;
using Xunit;

namespace tui.IntegrationTests;

// The connection must allow creating temporary databases. The application database is never used.
public sealed class MariaDbFactAttribute : FactAttribute
{
    public MariaDbFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VOYAGEVISTA_TEST_DATABASE")))
            Skip = "Set VOYAGEVISTA_TEST_DATABASE to run against an isolated MariaDB test database.";
    }
}

public class UserManagementTests
{
    [MariaDbFact]
    public async Task Migration_preserves_legacy_roles_including_admin_alias_and_supports_roundtrip()
    {
        await using var app = await DatabaseApplication.StartAsync(legacyUsers: true);
        await using var db = app.CreateDb();
        var admin = await db.AppUsers.Include(user => user.UserRoles).SingleAsync(user => user.UserId == 1);
        Assert.Equal("Beheerder", Assert.Single(admin.UserRoles).RoleName);
        Assert.True(UserSession.CreatePrincipal(admin, DateTimeOffset.UtcNow.AddHours(1)).IsInRole("Beheerder"));
        Assert.Equal(new[] { "Finance", "Sales" }, await db.UserRoles.Where(role => role.UserId == 2).OrderBy(role => role.RoleName).Select(role => role.RoleName).ToArrayAsync());
        await db.GetService<IMigrator>().MigrateAsync("20261027220000_ExpandUserRoles");
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(3, await db.UserRoles.CountAsync());
    }

    [MariaDbFact]
    public async Task Overview_search_and_catalogue_come_from_database()
    {
        await using var app = await DatabaseApplication.StartAsync();
        using var admin = await app.LoginAsync(1);
        var users = await admin.GetFromJsonAsync<ManagedUser[]>("/api/admin/users");
        Assert.Equal(3, users!.Length);
        Assert.Contains(users, user => user.Email == "FINANCE@TEST.INVALID" && user.Roles.SequenceEqual(["Finance"]));
        Assert.Single((await admin.GetFromJsonAsync<ManagedUser[]>("/api/admin/users?search=finance%40test"))!);
        Assert.Empty((await admin.GetFromJsonAsync<ManagedUser[]>("/api/admin/users?search=not-found"))!);
        Assert.Equal(AccessPolicies.Roles.Order(), (await admin.GetFromJsonAsync<string[]>("/api/admin/roles"))!.Order());
        Assert.Contains("Gebruikers en rollen", await admin.GetStringAsync("/admin/users"));
        Assert.DoesNotContain("passwordHash", await admin.GetStringAsync("/api/admin/users"), StringComparison.OrdinalIgnoreCase);
    }

    [MariaDbFact]
    public async Task Roles_can_be_added_deduplicated_removed_and_reloaded()
    {
        await using var app = await DatabaseApplication.StartAsync();
        using var admin = await app.LoginAsync(1);
        var saved = await app.PutRolesAsync(admin, 2, ["Finance", "Sales", "Finance"]);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(new[] { "Finance", "Sales" }, (await saved.Content.ReadFromJsonAsync<ManagedUser>())!.Roles);
        await using (var db = app.CreateDb())
            Assert.Equal(2, await db.UserRoles.CountAsync(role => role.UserId == 2));
        using var reopened = await app.LoginAsync(1);
        Assert.Equal(2, (await reopened.GetFromJsonAsync<ManagedUser[]>("/api/admin/users"))!.Single(user => user.UserId == 2).Roles.Length);
        Assert.Equal(HttpStatusCode.OK, (await app.PutRolesAsync(admin, 2, [])).StatusCode);
        await using var reloaded = app.CreateDb();
        Assert.False(await reloaded.UserRoles.AnyAsync(role => role.UserId == 2));
        using var limited = await app.LoginAsync(2);
        Assert.Equal(HttpStatusCode.OK, (await limited.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.GetAsync("/api/admin/users")).StatusCode);
    }

    [MariaDbFact]
    public async Task Non_administrators_and_anonymous_requests_cannot_use_api_or_page()
    {
        await using var app = await DatabaseApplication.StartAsync();
        using var finance = await app.LoginAsync(2);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.GetAsync("/api/admin/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.PutAsJsonAsync("/api/admin/users/3/roles", new UpdateUserRoles(["Beheerder"]))).StatusCode);
        var page = await finance.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Contains("/access-denied", page.Headers.Location!.ToString());
        using var anonymous = app.App.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/admin/users/3/roles", new UpdateUserRoles(["Beheerder"]))).StatusCode);
        await using var db = app.CreateDb();
        Assert.Equal("Sales", (await db.UserRoles.SingleAsync(role => role.UserId == 3)).RoleName);
    }

    [MariaDbFact]
    public async Task Invalid_roles_and_unknown_users_do_not_change_persisted_assignments()
    {
        await using var app = await DatabaseApplication.StartAsync();
        using var admin = await app.LoginAsync(1);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PutRolesAsync(admin, 2, ["Finance", "Invented"])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PutRolesAsync(admin, 2, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.PutRolesAsync(admin, 999999, ["Finance"])).StatusCode);
        await using var db = app.CreateDb();
        Assert.Equal("Finance", (await db.UserRoles.SingleAsync(role => role.UserId == 2)).RoleName);
    }

    [MariaDbFact]
    public async Task Last_administrator_is_retained_but_can_step_down_after_appointing_another()
    {
        await using var app = await DatabaseApplication.StartAsync();
        using var admin = await app.LoginAsync(1);
        var blocked = await app.PutRolesAsync(admin, 1, ["Finance"]);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("laatste beheerdersrol", await blocked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await app.PutRolesAsync(admin, 2, ["Finance", "Beheerder"])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.PutRolesAsync(admin, 1, ["Sales"])).StatusCode);
        // Cookie validation reads the new roles on the next request.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync("/api/admin/users/3/roles", new UpdateUserRoles(["Beheerder"]))).StatusCode);
        await using var db = app.CreateDb();
        Assert.Equal(1, await db.UserRoles.CountAsync(role => role.RoleName == "Beheerder"));
    }

    [MariaDbFact]
    public async Task Concurrent_admin_removals_cannot_remove_every_administrator()
    {
        await using var app = await DatabaseApplication.StartAsync();
        using var admin = await app.LoginAsync(1);
        await app.PutRolesAsync(admin, 2, ["Beheerder"]);
        await using var firstScope = app.App.Services.CreateAsyncScope();
        await using var secondScope = app.App.Services.CreateAsyncScope();
        var results = await Task.WhenAll(
            firstScope.ServiceProvider.GetRequiredService<UserRoleService>().UpdateAsync(1, 1, [], default),
            secondScope.ServiceProvider.GetRequiredService<UserRoleService>().UpdateAsync(2, 2, [], default));
        Assert.Single(results, result => result.Status == 200);
        Assert.Single(results, result => result.Status == 409);
        await using var db = app.CreateDb();
        Assert.Equal(1, await db.UserRoles.CountAsync(role => role.RoleName == "Beheerder"));
    }

    [MariaDbFact]
    public async Task Missing_csrf_token_is_rejected_without_writing()
    {
        await using var app = await DatabaseApplication.StartAsync();
        using var admin = await app.LoginAsync(1);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/users/2/roles", new UpdateUserRoles(["Sales"]))).StatusCode);
        await using var db = app.CreateDb();
        Assert.Equal("Finance", (await db.UserRoles.SingleAsync(role => role.UserId == 2)).RoleName);
    }

    [MariaDbFact]
    public async Task Failed_write_rolls_back_previous_assignments()
    {
        await using var app = await DatabaseApplication.StartAsync();
        using var admin = await app.LoginAsync(1);
        app.Failure.Enabled = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await app.PutRolesAsync(admin, 2, ["Sales"])).StatusCode);
        app.Failure.Enabled = false;
        await using var db = app.CreateDb();
        Assert.Equal("Finance", (await db.UserRoles.SingleAsync(role => role.UserId == 2)).RoleName);
    }

    private sealed class WriteFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Enabled) throw new DbUpdateException("Simulated write failure");
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class DatabaseApplication(WebApplication app, string adminConnection, string database, string connection, WriteFailure failure) : IAsyncDisposable
    {
        public WebApplication App => app;
        public WriteFailure Failure => failure;
        public ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql(connection, new MariaDbServerVersion(new Version(11, 0, 0))).Options);

        public static async Task<DatabaseApplication> StartAsync(bool legacyUsers = false)
        {
            var administrative = Environment.GetEnvironmentVariable("VOYAGEVISTA_TEST_DATABASE")!;
            var database = "voyagevista_role_tests_" + Guid.NewGuid().ToString("N");
            await using (var server = new MySqlConnection(administrative))
            {
                await server.OpenAsync();
                await new MySqlCommand($"CREATE DATABASE `{database}`", server).ExecuteNonQueryAsync();
            }
            var connection = new MySqlConnectionStringBuilder(administrative) { Database = database }.ConnectionString;
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(App).Assembly.FullName, EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            var failure = new WriteFailure();
            builder.Services.AddDbContextFactory<ApplicationDbContext>(options => options.UseMySql(connection, new MariaDbServerVersion(new Version(11, 0, 0))).AddInterceptors(failure));
            builder.Services.AddScoped<IUserSession, UserSession>();
            builder.Services.AddScoped<UserRoleService>();
            builder.Services.AddScoped<RoleCookieEvents>();
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<AuthenticationStateProvider, RoleAuthenticationStateProvider>();
            builder.Services.AddAuthorization(AccessPolicies.Configure);
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
            {
                options.LoginPath = "/auth/login";
                options.AccessDeniedPath = "/access-denied";
                options.EventsType = typeof(RoleCookieEvents);
            });
            var app = builder.Build();
            app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
            app.MapUserManagementEndpoints();
            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
            app.MapGet("/testing/signin/{id:int}", async (int id, HttpContext context, IDbContextFactory<ApplicationDbContext> factory) =>
            {
                await using var db = await factory.CreateDbContextAsync();
                var user = await db.AppUsers.Include(user => user.UserRoles).SingleAsync(user => user.UserId == id);
                await context.SignInAsync(UserSession.CreatePrincipal(user, DateTimeOffset.UtcNow.AddHours(1)));
            });
            var instance = new DatabaseApplication(app, administrative, database, connection, failure);
            try
            {
                await using var db = instance.CreateDb();
                if (legacyUsers)
                {
                    await db.GetService<IMigrator>().MigrateAsync("20261027220000_ExpandUserRoles");
                    await db.Database.ExecuteSqlRawAsync("""
                        INSERT INTO AppUsers (UserId, FirstName, LastName, Email, PasswordHash, UserRole, CreatedAt)
                        VALUES (1, 'Admin', 'Test', 'ADMIN@TEST.INVALID', '', 'Admin', UTC_TIMESTAMP()),
                               (2, 'Finance', 'Test', 'FINANCE@TEST.INVALID', '', ' finance ;Sales,Finance,Unknown', UTC_TIMESTAMP());
                        """);
                }
                await db.Database.MigrateAsync();
                if (!legacyUsers) db.AppUsers.AddRange(
                    new AppUser { UserId = 1, Email = "ADMIN@TEST.INVALID", UserRole = "Beheerder" },
                    new AppUser { UserId = 2, Email = "FINANCE@TEST.INVALID", UserRole = "Finance" },
                    new AppUser { UserId = 3, Email = "SALES@TEST.INVALID", UserRole = "Sales" });
                await db.SaveChangesAsync();
                await app.StartAsync();
                return instance;
            }
            catch { await instance.DisposeAsync(); throw; }
        }

        public async Task<HttpClient> LoginAsync(int userId)
        {
            var client = app.GetTestClient();
            var signIn = await client.GetAsync($"/testing/signin/{userId}");
            client.DefaultRequestHeaders.Add("Cookie", signIn.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
            return client;
        }

        public async Task<HttpResponseMessage> PutRolesAsync(HttpClient client, int userId, string[]? roles)
        {
            var response = await client.GetAsync("/api/admin/antiforgery");
            if (!response.IsSuccessStatusCode) return response;
            var token = (await response.Content.ReadFromJsonAsync<Token>())!.Value;
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                var previous = client.DefaultRequestHeaders.GetValues("Cookie").Single();
                client.DefaultRequestHeaders.Remove("Cookie");
                client.DefaultRequestHeaders.Add("Cookie", previous + "; " + cookies.First().Split(';')[0]);
            }
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{userId}/roles") { Content = JsonContent.Create(new UpdateUserRoles(roles)) };
            request.Headers.Add("RequestVerificationToken", token);
            return await client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            await app.DisposeAsync();
            MySqlConnection.ClearAllPools();
            // Only the exact generated temporary schema is eligible for cleanup.
            if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^voyagevista_role_tests_[a-f0-9]{32}$")) throw new InvalidOperationException("Invalid test schema");
            await using var server = new MySqlConnection(adminConnection);
            await server.OpenAsync();
            await new MySqlCommand($"DROP DATABASE `{database}`", server).ExecuteNonQueryAsync();
        }

        private record Token([property: System.Text.Json.Serialization.JsonPropertyName("token")] string Value);
    }
}
