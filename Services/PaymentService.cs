using Microsoft.EntityFrameworkCore;
using tui.Data;

namespace tui.Services;

// Records payments against invoices and keeps the outstanding balance up to date.
public class PaymentService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory = dbFactory;
}
