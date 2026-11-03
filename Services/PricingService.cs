using Microsoft.EntityFrameworkCore;
using tui.Data;

namespace tui.Services;

// Calculates prices from tariffs, discounts and surcharges so totals are consistent everywhere.
public class PricingService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory = dbFactory;
}
