using Microsoft.EntityFrameworkCore;
using tui.Data;

namespace tui.Services;

// Owns the trip catalog: trips, destinations, activities, planning, guides and transport.
public class TripService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory = dbFactory;
}
