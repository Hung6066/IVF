using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Provisions infrastructure resources for tenant isolation strategies.
/// - SeparateSchema: Creates a dedicated PostgreSQL schema with all tenant tables
/// - SeparateDatabase: Creates a dedicated PostgreSQL database with full schema
/// </summary>
public interface ITenantProvisioningService
{
    /// <summary>
    /// Provision infrastructure for the given tenant based on the isolation strategy.
    /// Returns the provisioned schema name or connection string.
    /// </summary>
    Task<TenantProvisioningResult> ProvisionAsync(
        Guid tenantId,
        string tenantSlug,
        DataIsolationStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Deprovision (clean up) tenant infrastructure when switching back to SharedDatabase or deleting.
    /// </summary>
    Task DeprovisionAsync(
        Guid tenantId,
        DataIsolationStrategy previousStrategy,
        string? schemaName,
        string? connectionString,
        CancellationToken ct = default);

    /// <summary>
    /// Check if a schema or database already exists.
    /// </summary>
    Task<bool> ResourceExistsAsync(
        DataIsolationStrategy strategy,
        string resourceName,
        CancellationToken ct = default);
}

public record TenantProvisioningResult(
    bool Success,
    string? SchemaName,
    string? ConnectionString,
    string? ErrorMessage);
