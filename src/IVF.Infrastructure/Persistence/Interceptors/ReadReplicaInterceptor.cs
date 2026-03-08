using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// EF Core interceptor that routes read-only queries (SELECT) to a PostgreSQL
/// read replica when available. Falls back to primary if replica is not configured
/// or if the connection fails.
/// Configure via ConnectionStrings:ReadReplicaConnection in appsettings.
/// </summary>
public class ReadReplicaInterceptor : DbCommandInterceptor
{
    private readonly string? _replicaConnectionString;
    private readonly ILogger<ReadReplicaInterceptor> _logger;
    private volatile bool _replicaAvailable = true;
    private DateTime _lastFailureCheck = DateTime.MinValue;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);

    public ReadReplicaInterceptor(
        IConfiguration configuration,
        ILogger<ReadReplicaInterceptor> logger)
    {
        _replicaConnectionString = configuration.GetConnectionString("ReadReplicaConnection");
        _logger = logger;

        if (!string.IsNullOrEmpty(_replicaConnectionString))
        {
            _logger.LogInformation("Read-replica routing enabled");
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        TryRouteToReplica(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        TryRouteToReplica(command);
        return ValueTask.FromResult(result);
    }

    private void TryRouteToReplica(DbCommand command)
    {
        if (string.IsNullOrEmpty(_replicaConnectionString))
            return;

        // Only route SELECT queries (read-only)
        if (!IsReadOnlyQuery(command.CommandText))
            return;

        // Skip if in a transaction (must use primary)
        if (command.Transaction is not null)
            return;

        // Check if replica is available (with retry backoff)
        if (!_replicaAvailable)
        {
            if (DateTime.UtcNow - _lastFailureCheck < RetryInterval)
                return;

            _replicaAvailable = true; // Retry
        }

        try
        {
            command.Connection?.ConnectionString = _replicaConnectionString;
        }
        catch (Exception ex)
        {
            _replicaAvailable = false;
            _lastFailureCheck = DateTime.UtcNow;
            _logger.LogWarning(ex, "Read-replica connection failed, falling back to primary");
        }
    }

    private static bool IsReadOnlyQuery(string commandText)
    {
        var trimmed = commandText.AsSpan().TrimStart();
        return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("-- ", StringComparison.OrdinalIgnoreCase)
               && trimmed.ToString().Contains("SELECT", StringComparison.OrdinalIgnoreCase);
    }
}
