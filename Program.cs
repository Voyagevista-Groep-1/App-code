using tui.Components;
using tui.Data;
using tui.Models;
using tui.Services;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

var dbHost = builder.Configuration["Db:host"];
var dbPort = builder.Configuration["Db:port"];
var dbName = builder.Configuration["Db:name"];
var dbUser = builder.Configuration["Db:user"];
var dbPassword = builder.Configuration["Db:password"];

var connectionString = $"Server={dbHost};Port={dbPort};Database={dbName};User={dbUser};Password={dbPassword};";

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Domain services.
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<TripService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<InvoiceWorkflowService>();
builder.Services.AddScoped<ManualInvoiceService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<SlaService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 4 * 1024 * 1024);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserRoleService>();
builder.Services.AddScoped<IUserSession, UserSession>();
builder.Services.AddScoped<RoleCookieEvents>();
builder.Services.AddScoped<AuthenticationStateProvider, RoleAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.AccessDeniedPath = "/access-denied";
        options.EventsType = typeof(RoleCookieEvents);
        options.ExpireTimeSpan = UserSession.Lifetime;
        options.SlidingExpiration = false;
    });

builder.Services.AddAuthorization(AccessPolicies.Configure);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync(); // Run migrations on startup
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAuthEndpoints();
app.MapUserManagementEndpoints();
app.MapInvoiceEndpoints();

app.Run();
