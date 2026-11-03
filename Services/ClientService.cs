using Microsoft.EntityFrameworkCore;
using tui.Data;

namespace tui.Services;

// Manages customer records: client profiles, client types and their linked party details.
public class ClientService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory = dbFactory;
}
