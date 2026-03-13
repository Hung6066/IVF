using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IVF.API.Extensions;

/// <summary>
/// Enterprise database configuration extensions
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Adds enterprise-grade database configuration with connection pooling,
    /// retry policies, and performance optimizations
    /// </summary>
    public static IServiceCollection AddEnterpriseDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string is required");

        // Build optimized connection string
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            // Connection Pooling
            Pooling = true,
            MinPoolSize = configuration.GetValue("Database:MinPoolSize", 10),
            MaxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200),
            ConnectionIdleLifetime = configuration.GetValue("Database:ConnectionIdleLifetime", 300),
            ConnectionPruningInterval = configuration.GetValue("Database:ConnectionPruningInterval", 10),

            // Timeouts
            CommandTimeout = configuration.GetValue("Database:CommandTimeout", 30),
            Timeout = configuration.GetValue("Database:ConnectionTimeout", 15),

            // Keep-alive
            TcpKeepAlive = true,
            KeepAlive = configuration.GetValue("Database:KeepAlive", 30),

            // Performance
            ReadBufferSize = configuration.GetValue("Database:ReadBufferSize", 8192),
            WriteBufferSize = configuration.GetValue("Database:WriteBufferSize", 8192),
            NoResetOnClose = true,

            // SSL
            SslMode = configuration.GetValue("Database:SslMode", SslMode.Prefer),

            // Application info
            ApplicationName = "IVF-API"
        };

        var optimizedConnectionString = builder.ConnectionString;

        // Configure DbContext with optimizations
        services.AddDbContext<IvfDbContext>((sp, options) =>
        {
            options.UseNpgsql(optimizedConnectionString, npgsqlOptions =>
            {
                // Retry on transient failures
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);

                // Performance optimizations
                npgsqlOptions.CommandTimeout(30);
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);

                // Migration assembly
                npgsqlOptions.MigrationsAssembly("IVF.Infrastructure");
            });

            // Query tracking behavior
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

            // Enable detailed errors in development
            if (configuration.GetValue<bool>("Database:EnableDetailedErrors", false))
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }

            // Add interceptors
            var logger = sp.GetRequiredService<ILogger<IvfDbContext>>();
            options.AddInterceptors(
                new SlowQueryInterceptor(logger, configuration.GetValue("Database:SlowQueryThresholdMs", 500)),
                new ConnectionPoolInterceptor(logger));
        });

        // Add DbContext factory for background services
        services.AddDbContextFactory<IvfDbContext>((sp, options) =>
        {
            options.UseNpgsql(optimizedConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                npgsqlOptions.CommandTimeout(30);
            });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        // Add read replica support (optional)
        var readReplicaConnectionString = configuration.GetConnectionString("ReadReplica");
        if (!string.IsNullOrEmpty(readReplicaConnectionString))
        {
            services.AddScoped<IvfReadOnlyDbContext>(sp =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<IvfDbContext>();
                optionsBuilder.UseNpgsql(readReplicaConnectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
                    npgsqlOptions.CommandTimeout(30);
                });
                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                return new IvfReadOnlyDbContext(optionsBuilder.Options);
            });
        }

        return services;
    }

    /// <summary>
    /// Configures database health and monitoring
    /// </summary>
    public static IServiceCollection AddDatabaseMonitoring(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add database metrics collector
        services.AddHostedService<DatabaseMetricsCollector>();

        return services;
    }
}

/// <summary>
/// Read-only DbContext for read replicas
/// </summary>
public class IvfReadOnlyDbContext : IvfDbContext
{
    public IvfReadOnlyDbContext(DbContextOptions<IvfDbContext> options)
        : base(options)
    {
    }

    public override int SaveChanges()
    {
        throw new InvalidOperationException("Read-only context cannot save changes");
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Read-only context cannot save changes");
    }
}

/// <summary>
/// Interceptor to log slow queries
/// </summary>
public class SlowQueryInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
{
    private readonly ILogger _logger;
    private readonly int _thresholdMs;

    public SlowQueryInterceptor(ILogger logger, int thresholdMs)
    {
        _logger = logger;
        _thresholdMs = thresholdMs;
    }

    public override System.Data.Common.DbDataReader ReaderExecuted(
        System.Data.Common.DbCommand command,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,
        System.Data.Common.DbDataReader result)
    {
        LogSlowQuery(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(
        System.Data.Common.DbCommand command,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,
        System.Data.Common.DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogSlowQuery(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        System.Data.Common.DbCommand command,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,
        int result)
    {
        LogSlowQuery(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        System.Data.Common.DbCommand command,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogSlowQuery(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void LogSlowQuery(System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData)
    {
        if (eventData.Duration.TotalMilliseconds > _thresholdMs)
        {
            _logger.LogWarning(
                "Slow query detected ({Duration}ms): {CommandText}",
                eventData.Duration.TotalMilliseconds,
                command.CommandText);
        }
    }
}

/// <summary>
/// Interceptor to monitor connection pool usage
/// </summary>
public class ConnectionPoolInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor
{
    private readonly ILogger _logger;
    private static int _activeConnections;
    private static int _peakConnections;

    public ConnectionPoolInterceptor(ILogger logger)
    {
        _logger = logger;
    }

    public override void ConnectionOpened(
        System.Data.Common.DbConnection connection,
        Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData)
    {
        var current = Interlocked.Increment(ref _activeConnections);
        var peak = Interlocked.CompareExchange(ref _peakConnections, current, _peakConnections);

        if (current > peak)
        {
            Interlocked.Exchange(ref _peakConnections, current);
        }

        if (current > 150) // Warn at 75% of typical max pool size
        {
            _logger.LogWarning(
                "High connection pool usage: {Active} active, {Peak} peak",
                current, _peakConnections);
        }

        base.ConnectionOpened(connection, eventData);
    }

    public override void ConnectionClosed(
        System.Data.Common.DbConnection connection,
        Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData)
    {
        Interlocked.Decrement(ref _activeConnections);
        base.ConnectionClosed(connection, eventData);
    }

    public static (int Active, int Peak) GetStatistics() => (_activeConnections, _peakConnections);
}

/// <summary>
/// Background service to collect database metrics
/// </summary>
public class DatabaseMetricsCollector : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMetricsCollector> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public DatabaseMetricsCollector(
        IServiceProvider serviceProvider,
        ILogger<DatabaseMetricsCollector> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectMetricsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect database metrics");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task CollectMetricsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        try
        {
            // Get pool statistics
            var stats = await context.Database.SqlQueryRaw<DbStats>(@"
                SELECT
                    numbackends as ""ActiveConnections"",
                    xact_commit as ""TransactionsCommitted"",
                    xact_rollback as ""TransactionsRolledBack"",
                    blks_read as ""BlocksRead"",
                    blks_hit as ""BlocksHit"",
                    tup_returned as ""TuplesReturned"",
                    tup_fetched as ""TuplesFetched"",
                    tup_inserted as ""TuplesInserted"",
                    tup_updated as ""TuplesUpdated"",
                    tup_deleted as ""TuplesDeleted""
                FROM pg_stat_database
                WHERE datname = current_database()")
                .FirstOrDefaultAsync(ct);

            if (stats != null)
            {
                var cacheHitRatio = stats.BlocksHit + stats.BlocksRead > 0
                    ? (double)stats.BlocksHit / (stats.BlocksHit + stats.BlocksRead) * 100
                    : 100;

                var (activeLocal, peakLocal) = ConnectionPoolInterceptor.GetStatistics();

                _logger.LogInformation(
                    "DB Stats - Active: {Active}, Peak: {Peak}, Commits: {Commits}, " +
                    "Rollbacks: {Rollbacks}, CacheHit: {CacheHit:F1}%",
                    stats.ActiveConnections,
                    peakLocal,
                    stats.TransactionsCommitted,
                    stats.TransactionsRolledBack,
                    cacheHitRatio);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query database stats");
        }
    }

    private record DbStats(
        int ActiveConnections,
        long TransactionsCommitted,
        long TransactionsRolledBack,
        long BlocksRead,
        long BlocksHit,
        long TuplesReturned,
        long TuplesFetched,
        long TuplesInserted,
        long TuplesUpdated,
        long TuplesDeleted);
}
