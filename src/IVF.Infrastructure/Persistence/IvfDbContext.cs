using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

public class IvfDbContext : DbContext
{
    public IvfDbContext(DbContextOptions<IvfDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Couple> Couples => Set<Couple>();
    public DbSet<TreatmentCycle> TreatmentCycles => Set<TreatmentCycle>();
    public DbSet<QueueTicket> QueueTickets => Set<QueueTicket>();
    public DbSet<Ultrasound> Ultrasounds => Set<Ultrasound>();
    public DbSet<Embryo> Embryos => Set<Embryo>();
    public DbSet<CryoLocation> CryoLocations => Set<CryoLocation>();
    public DbSet<SemenAnalysis> SemenAnalyses => Set<SemenAnalysis>();
    public DbSet<SpermDonor> SpermDonors => Set<SpermDonor>();
    public DbSet<SpermSample> SpermSamples => Set<SpermSample>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ServiceCatalog> ServiceCatalogs => Set<ServiceCatalog>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IvfDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
