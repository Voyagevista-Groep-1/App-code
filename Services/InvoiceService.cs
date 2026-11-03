using Microsoft.EntityFrameworkCore;
using tui.Data;

namespace tui.Services;

// Builds and maintains invoices and their lines, including the journal entries they produce.
public class InvoiceService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory = dbFactory;
}
