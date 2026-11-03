using Microsoft.EntityFrameworkCore;
using tui.Data;

namespace tui.Services;

// Applies SLA rules to bookings and tickets, tracking deadlines and warning times for escalation.
public class SlaService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory = dbFactory;
}
