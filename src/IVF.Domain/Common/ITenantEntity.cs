namespace IVF.Domain.Common;

/// <summary>
/// Interface marker for entities that belong to a specific tenant.
/// All clinical/operational entities should implement this.
/// System-wide entities (e.g., Tenant itself, MenuItems) should NOT.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; }
    void SetTenantId(Guid tenantId);
}
