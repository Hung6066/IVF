using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Background service that automatically creates future partitions for partitioned tables.
/// Runs daily and ensures partitions exist for the next 3 months ahead.
/// </summary>
public class PartitionMaintenanceService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PartitionMaintenanceService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public PartitionMaintenanceService(
        IServiceScopeFactory scopeFactory,
        ILogger<PartitionMaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for app startup to complete
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsurePartitionsExist(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during partition maintenance");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task EnsurePartitionsExist(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var now = DateTime.UtcNow;
        // Ensure partitions exist for next 3 months
        var monthsAhead = 3;

        _logger.LogInformation("Partition maintenance: ensuring partitions exist through {EndDate}",
            now.AddMonths(monthsAhead).ToString("yyyy-MM"));

        // Range-partitioned tables (by month)
        var rangePartitionedTables = new[]
        {
            ("form_responses", false),
            ("form_field_values", false),
            ("\"FormFieldValueDetails\"", true),
            ("semen_analyses", false)
        };

        foreach (var (tableName, isQuoted) in rangePartitionedTables)
        {
            for (int i = 0; i <= monthsAhead; i++)
            {
                var partitionStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(i);
                var partitionEnd = partitionStart.AddMonths(1);
                var suffix = partitionStart.ToString("yyyy_MM");

                string partitionName;
                string sql;

                if (isQuoted)
                {
                    // PascalCase table name (quoted)
                    var baseNameUnquoted = tableName.Trim('"');
                    partitionName = $"\"{baseNameUnquoted}_{suffix}\"";
                    sql = $@"
                        DO $$
                        BEGIN
                            IF NOT EXISTS (
                                SELECT 1 FROM pg_class c
                                JOIN pg_namespace n ON n.oid = c.relnamespace
                                WHERE c.relname = '{baseNameUnquoted}_{suffix}'
                                AND n.nspname = 'public'
                            ) THEN
                                CREATE TABLE {partitionName} PARTITION OF {tableName}
                                    FOR VALUES FROM ('{partitionStart:yyyy-MM-dd}') TO ('{partitionEnd:yyyy-MM-dd}');
                            END IF;
                        END $$;";
                }
                else
                {
                    partitionName = $"{tableName}_{suffix}";
                    sql = $@"
                        DO $$
                        BEGIN
                            IF NOT EXISTS (
                                SELECT 1 FROM pg_class c
                                JOIN pg_namespace n ON n.oid = c.relnamespace
                                WHERE c.relname = '{partitionName}'
                                AND n.nspname = 'public'
                            ) THEN
                                CREATE TABLE {partitionName} PARTITION OF {tableName}
                                    FOR VALUES FROM ('{partitionStart:yyyy-MM-dd}') TO ('{partitionEnd:yyyy-MM-dd}');
                            END IF;
                        END $$;";
                }

                try
                {
                    await context.Database.ExecuteSqlRawAsync(sql, ct);
                    _logger.LogDebug("Ensured partition {PartitionName} exists for {TableName}", partitionName, tableName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not create partition {PartitionName} for {TableName}", partitionName, tableName);
                }
            }
        }

        _logger.LogInformation("Partition maintenance completed successfully");
    }
}
