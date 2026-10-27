using Microsoft.AspNetCore.Authorization;

namespace tui.Services;

public static class AccessPolicies
{
    public const string Administrator = "Beheerder";
    public const string Finance = "Finance";
    public const string Sales = "Sales";
    public const string Marketing = "Marketing";
    public const string CustomerService = "Customerservice";
    public const string ProductDevelopment = "Productdevelopment";
    public const string IT = "IT";
    public const string InvoiceEmployee = "medewerker";
    public const string InvoiceController = "controller";
    public const string InvoiceReadOnly = "read_only";
    public const string InvoiceWrite = "Invoices.Write";
    public const string InvoiceReview = "Invoices.Review";

    public static readonly string[] Roles =
        [Administrator, Finance, Sales, Marketing, CustomerService, ProductDevelopment, IT,
            InvoiceEmployee, InvoiceController, InvoiceReadOnly];

    public static IEnumerable<string> ParseRoles(string? value) =>
        (value ?? "").Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(role => role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? Administrator : role)
            .Select(role => Roles.FirstOrDefault(known => known.Equals(role, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);

    public static void Configure(AuthorizationOptions options)
    {
        foreach (var role in Roles)
        {
            options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireRole(role, Administrator));
        }
        options.AddPolicy(Finance, policy => policy.RequireAuthenticatedUser()
            .RequireRole(Finance, Administrator, InvoiceEmployee, InvoiceController, InvoiceReadOnly));
        // Segregation of duties: administrators need the explicit operational role too.
        options.AddPolicy(InvoiceWrite, policy => policy.RequireAuthenticatedUser().RequireRole(InvoiceEmployee));
        options.AddPolicy(InvoiceReview, policy => policy.RequireAuthenticatedUser().RequireRole(InvoiceController));
    }
}
