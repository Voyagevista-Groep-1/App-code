using Microsoft.EntityFrameworkCore;
using tui.Models;
using tui.Services;

namespace tui.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRoleAssignment> UserRoles => Set<UserRoleAssignment>();
    public DbSet<ClientType> ClientTypes => Set<ClientType>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<TripGuide> TripGuides => Set<TripGuide>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<Transport> Transports => Set<Transport>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripDestination> TripDestinations => Set<TripDestination>();
    public DbSet<TripActivity> TripActivities => Set<TripActivity>();
    public DbSet<TripPlanning> TripPlannings => Set<TripPlanning>();
    public DbSet<SurchargeType> SurchargeTypes => Set<SurchargeType>();
    public DbSet<Surcharge> Surcharges => Set<Surcharge>();
    public DbSet<TravelerType> TravelerTypes => Set<TravelerType>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<Traveler> Travelers => Set<Traveler>();
    public DbSet<TicketStatus> TicketStatuses => Set<TicketStatus>();
    public DbSet<TicketPriority> TicketPriorities => Set<TicketPriority>();
    public DbSet<TicketSolution> TicketSolutions => Set<TicketSolution>();
    public DbSet<SLA> SLAs => Set<SLA>();
    public DbSet<BookingSLA> BookingSLAs => Set<BookingSLA>();
    public DbSet<ComplaintTicket> ComplaintTickets => Set<ComplaintTicket>();
    public DbSet<TicketAction> TicketActions => Set<TicketAction>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<ManualInvoiceRegistration> ManualInvoiceRegistrations => Set<ManualInvoiceRegistration>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>().HasData(AccessPolicies.Roles.Select(name => new Role { Name = name }));
        modelBuilder.Entity<UserRoleAssignment>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleName });
            e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleName);
        });

        // ---- Customers ----
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Client>(e =>
        {
            e.HasOne(x => x.User).WithOne(x => x.Client).HasForeignKey<Client>(x => x.UserId);
            e.HasOne(x => x.ClientType).WithMany(x => x.Clients).HasForeignKey(x => x.ClientTypeId);
        });

        // ---- Trips ----
        modelBuilder.Entity<Trip>(e =>
        {
            e.HasOne(x => x.Discount).WithMany(x => x.Trips).HasForeignKey(x => x.DiscountId);
            e.HasOne(x => x.Transport).WithMany(x => x.Trips).HasForeignKey(x => x.TransportId);
        });

        modelBuilder.Entity<TripDestination>(e =>
        {
            e.HasOne(x => x.Trip).WithMany(x => x.TripDestinations).HasForeignKey(x => x.TripId);
            e.HasOne(x => x.Destination).WithMany(x => x.TripDestinations).HasForeignKey(x => x.DestinationId);
        });

        modelBuilder.Entity<TripActivity>(e =>
        {
            e.HasKey(x => new { x.TripId, x.ActivityId });
            e.HasOne(x => x.Trip).WithMany(x => x.TripActivities).HasForeignKey(x => x.TripId);
            e.HasOne(x => x.Activity).WithMany(x => x.TripActivities).HasForeignKey(x => x.ActivityId);
        });

        modelBuilder.Entity<TripPlanning>(e =>
        {
            e.HasOne(x => x.Trip).WithMany(x => x.TripPlannings).HasForeignKey(x => x.TripId);
            e.HasOne(x => x.TripGuide).WithMany(x => x.TripPlannings).HasForeignKey(x => x.TripGuideId);
        });

        modelBuilder.Entity<Surcharge>(e =>
        {
            e.HasOne(x => x.SurchargeType).WithMany(x => x.Surcharges).HasForeignKey(x => x.SurchargeTypeId);
            e.HasOne(x => x.Trip).WithMany(x => x.Surcharges).HasForeignKey(x => x.TripId);
        });

        // ---- Bookings ----
        modelBuilder.Entity<Tariff>(e =>
        {
            e.HasOne(x => x.Trip).WithMany(x => x.Tariffs).HasForeignKey(x => x.TripId);
            e.HasOne(x => x.TravelerType).WithMany(x => x.Tariffs).HasForeignKey(x => x.TravelerTypeId);
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.HasOne(x => x.Client).WithMany(x => x.Bookings).HasForeignKey(x => x.ClientId);
            e.HasOne(x => x.Trip).WithMany(x => x.Bookings).HasForeignKey(x => x.TripId);
        });

        modelBuilder.Entity<BookingItem>(e =>
        {
            e.HasOne(x => x.Booking).WithMany(x => x.BookingItems).HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.Tariff).WithMany(x => x.BookingItems).HasForeignKey(x => x.TariffId);
        });

        modelBuilder.Entity<Traveler>(e =>
        {
            e.HasOne(x => x.TravelerType).WithMany(x => x.Travelers).HasForeignKey(x => x.TravelerTypeId);
            e.HasOne(x => x.Booking).WithMany(x => x.Travelers).HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.Client).WithMany(x => x.Travelers).HasForeignKey(x => x.ClientId);
        });

        // ---- Support ----
        modelBuilder.Entity<BookingSLA>(e =>
        {
            e.HasOne(x => x.Booking).WithMany(x => x.BookingSLAs).HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.SLA).WithMany(x => x.BookingSLAs).HasForeignKey(x => x.SLAId);
        });

        modelBuilder.Entity<ComplaintTicket>(e =>
        {
            e.HasOne(x => x.Client).WithMany(x => x.ComplaintTickets).HasForeignKey(x => x.ClientId);
            e.HasOne(x => x.Booking).WithMany(x => x.ComplaintTickets).HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.Status).WithMany(x => x.ComplaintTickets).HasForeignKey(x => x.StatusId);
            e.HasOne(x => x.Priority).WithMany(x => x.ComplaintTickets).HasForeignKey(x => x.PriorityId);
            e.HasOne(x => x.Solution).WithMany(x => x.ComplaintTickets).HasForeignKey(x => x.SolutionId);
            e.HasOne(x => x.SLA).WithMany(x => x.ComplaintTickets).HasForeignKey(x => x.SLAId);
            e.HasOne(x => x.StaffUser).WithMany(x => x.AssignedTickets).HasForeignKey(x => x.StaffUserId);
        });

        modelBuilder.Entity<TicketAction>(e =>
        {
            e.HasOne(x => x.Ticket).WithMany(x => x.TicketActions).HasForeignKey(x => x.TicketId);
        });

        modelBuilder.Entity<Feedback>(e =>
        {
            e.HasOne(x => x.Ticket).WithMany(x => x.Feedbacks).HasForeignKey(x => x.TicketId);
        });

        // ---- Finance ----
        modelBuilder.Entity<Party>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Client).WithOne(x => x.Party).HasForeignKey<Party>(x => x.ClientId);
        });

        modelBuilder.Entity<Account>(e =>
        {
            e.HasIndex(x => x.AccountNumber).IsUnique();
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasOne(x => x.Party).WithMany(x => x.Invoices).HasForeignKey(x => x.PartyId);
            e.HasOne(x => x.Booking).WithMany(x => x.Invoices).HasForeignKey(x => x.BookingId);
            e.Property(x => x.Currency).HasDefaultValue("EUR");
            e.Property(x => x.WorkflowStatus).HasDefaultValue("concept");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedByUserId);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ReviewedByUserId);
            e.HasIndex(x => x.WorkflowStatus);
        });

        modelBuilder.Entity<AuditLogEntry>(e =>
        {
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorUserId);
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAtUtc });
        });

        modelBuilder.Entity<InvoiceLine>(e =>
        {
            e.HasOne(x => x.Invoice).WithMany(x => x.InvoiceLines).HasForeignKey(x => x.InvoiceId);
            e.HasOne(x => x.Account).WithMany(x => x.InvoiceLines).HasForeignKey(x => x.AccountId);
            e.HasOne(x => x.TaxRate).WithMany(x => x.InvoiceLines).HasForeignKey(x => x.TaxRateId);
            e.HasIndex(x => new { x.InvoiceId, x.Position });
        });

        modelBuilder.Entity<ManualInvoiceRegistration>(e =>
        {
            e.HasOne(x => x.Invoice).WithOne(x => x.ManualRegistration).HasForeignKey<ManualInvoiceRegistration>(x => x.InvoiceId);
            e.HasIndex(x => new { x.SourceIdentityKey, x.OriginalInvoiceNumber }).IsUnique();
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId);
        });

        modelBuilder.Entity<JournalEntry>(e =>
        {
            e.HasOne(x => x.Invoice).WithMany(x => x.JournalEntries).HasForeignKey(x => x.InvoiceId);
        });

        modelBuilder.Entity<JournalEntryLine>(e =>
        {
            e.HasOne(x => x.JournalEntry).WithMany(x => x.JournalEntryLines).HasForeignKey(x => x.JournalEntryId);
            e.HasOne(x => x.Account).WithMany(x => x.JournalEntryLines).HasForeignKey(x => x.AccountId);
        });

        // The ERD doesn't specify delete behaviour. Restrict everywhere avoids accidental cascades
        // (bookings, tickets and invoices should never vanish because a parent row was deleted)
        // and avoids SQL Server's "multiple cascade paths" error in this graph.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
