using Microsoft.EntityFrameworkCore;
using tui.Data;

namespace tui.Services;

// Handles creating, changing and cancelling bookings along with their travelers and booking items.
public class BookingService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory = dbFactory;
}
