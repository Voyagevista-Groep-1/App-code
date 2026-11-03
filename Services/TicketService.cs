using Microsoft.EntityFrameworkCore;
using tui.Data;

namespace tui.Services;

// Handles complaint tickets, their status, priority, solution and action history.
public class TicketService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory = dbFactory;
}
