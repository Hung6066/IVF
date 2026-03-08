namespace IVF.Application.Common;

/// <summary>
/// MinIO bucket name constants — shared across handlers and endpoints.
/// Matches defaults in MinioOptions (Infrastructure layer).
/// </summary>
public static class StorageBuckets
{
    public const string Documents = "ivf-documents";
    public const string SignedPdfs = "ivf-signed-pdfs";
    public const string MedicalImages = "ivf-medical-images";
    public const string AuditArchive = "ivf-audit-archive";
}

/// <summary>
/// Tenant-scoped object key prefixing for multi-tenant storage isolation.
/// All object keys are prefixed with "tenants/{tenantId}/" to ensure
/// tenant data is logically separated within shared buckets.
/// </summary>
public static class TenantStoragePrefix
{
    /// <summary>
    /// Prefix an object key with the tenant scope.
    /// Returns "tenants/{tenantId}/{objectKey}" or the original key if tenantId is null.
    /// </summary>
    public static string Prefix(Guid? tenantId, string objectKey)
    {
        if (tenantId == null || tenantId == Guid.Empty)
            return objectKey;

        return $"tenants/{tenantId}/{objectKey}";
    }
}
