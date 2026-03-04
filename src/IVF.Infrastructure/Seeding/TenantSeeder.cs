using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Seeding;

public static class TenantSeeder
{
    public static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IvfDbContext>>();

        // Skip if default tenant already exists
        if (await context.Tenants.AnyAsync(t => t.Id == DefaultTenantId))
            return;

        logger.LogInformation("Seeding default tenant...");

        // Create the default/platform tenant
        var tenant = Tenant.Create(
            "IVF Platform Default",
            "default",
            "admin@ivf-platform.vn",
            "0123456789",
            "Hà Nội, Việt Nam");
        tenant.Activate();
        tenant.SetResourceLimits(999, 99999, 1_048_576, true, true, true, true);
        tenant.SetRootTenant(true);

        // Set the Id to our well-known value
        typeof(Tenant).GetProperty("Id")!.SetValue(tenant, DefaultTenantId);
        context.Tenants.Add(tenant);

        // Create platform subscription
        var subscription = TenantSubscription.Create(
            DefaultTenantId, SubscriptionPlan.Enterprise, BillingCycle.Annually, 0);
        context.TenantSubscriptions.Add(subscription);

        // Create initial usage record
        var now = DateTime.UtcNow;
        var usage = TenantUsageRecord.Create(DefaultTenantId, now.Year, now.Month);
        context.TenantUsageRecords.Add(usage);

        await context.SaveChangesAsync();

        // Migrate existing records: set TenantId = DefaultTenantId where TenantId is empty
        await MigrateExistingDataAsync(context, logger);

        logger.LogInformation("Default tenant seeded and existing data migrated");
    }

    private static async Task MigrateExistingDataAsync(IvfDbContext context, ILogger logger)
    {
        var defaultId = DefaultTenantId;

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
