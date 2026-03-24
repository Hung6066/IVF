using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Seeding;

public static class TenantSeeder
{
    private const string RootTenantSlug = "default";

    /// <summary>Returns the root tenant Id by resolving from the DB (slug = "default").</summary>
    public static async Task<Guid> GetRootTenantIdAsync(IvfDbContext context)
    {
        var id = await context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug == RootTenantSlug)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync();
        return id ?? Guid.Empty;
    }

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IvfDbContext>>();

        // Skip if default tenant already exists (look up by slug, not hardcoded Id)
        if (await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == RootTenantSlug))
            return;

        logger.LogInformation("Seeding default tenant...");

        // Create the default/platform tenant — Id is auto-generated (Guid.NewGuid())
        var tenant = Tenant.Create(
            "IVF Platform Default",
            RootTenantSlug,
            "admin@ivf-platform.vn",
            "0123456789",
            "Hà Nội, Việt Nam");
        tenant.Activate();
        tenant.SetResourceLimits(999, 99999, 1_048_576, true, true, true, true);
        tenant.SetRootTenant(true);
        context.Tenants.Add(tenant);

        // Create platform subscription
        var subscription = TenantSubscription.Create(
            tenant.Id, SubscriptionPlan.Enterprise, BillingCycle.Annually, 0);
        context.TenantSubscriptions.Add(subscription);

        // Create initial usage record
        var now = DateTime.UtcNow;
        var usage = TenantUsageRecord.Create(tenant.Id, now.Year, now.Month);
        context.TenantUsageRecords.Add(usage);

        await context.SaveChangesAsync();

        logger.LogInformation("Default tenant seeded with Id={TenantId}", tenant.Id);

        // Migrate existing records: set TenantId = tenant.Id where TenantId is empty
        await MigrateExistingDataAsync(context, tenant.Id, logger);

        logger.LogInformation("Default tenant seeded and existing data migrated");
    }

    private static async Task MigrateExistingDataAsync(IvfDbContext context, Guid defaultId, ILogger logger)
    {

        // Use raw SQL for efficient bulk update of all tenant entities with empty TenantId
        var tables = new[]
        {
            ("users", "\"TenantId\""),
            ("patients", "\"TenantId\""),
            ("doctors", "\"TenantId\""),
            ("couples", "\"TenantId\""),
            ("treatment_cycles", "\"TenantId\""),
            ("queue_tickets", "\"TenantId\""),
            ("invoices", "\"TenantId\""),
            ("appointments", "\"TenantId\""),
            ("notifications", "\"TenantId\""),
            ("form_templates", "\"TenantId\""),
            ("form_responses", "\"TenantId\""),
            ("service_catalog", "\"TenantId\"")
        };

        foreach (var (table, column) in tables)
        {
            try
            {
                var sql = $"UPDATE \"{table}\" SET {column} = {{0}} WHERE {column} = {{1}} OR {column} IS NULL";
                var affected = await context.Database.ExecuteSqlRawAsync(sql, defaultId, Guid.Empty);
                if (affected > 0)
                    logger.LogInformation("Migrated {Count} records in {Table} to default tenant", affected, table);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not migrate table {Table} — it may not have TenantId column yet", table);
            }
        }

        // Mark the admin user as platform admin
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE \"users\" SET \"IsPlatformAdmin\" = true WHERE \"Username\" = {0}", "admin");
    }
}
