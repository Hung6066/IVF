using System.Security.Cryptography.X509Certificates;
using IVF.API.Endpoints;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Services;

/// <summary>
/// Orchestrates per-tenant Sub-CA lifecycle and tenant-aware user certificate provisioning.
/// 
/// Flow:
///   1. Admin provisions Sub-CA for tenant → CreateIntermediateCA → TenantSubCa record
///   2. User cert provisioning → finds tenant's Sub-CA → issues end-entity cert via CertificateAuthorityService
///   3. Worker naming: PDFSigner_{tenantSlug}_{sanitizedUsername}
///   4. Worker ID: hash(tenantId, userId) → wider range (1000–9999) to avoid bootstrap workers
///   5. Tenant offboarding → revoke Sub-CA → all user certs invalidated atomically
/// </summary>
public sealed class TenantCertificateService(
    CertificateAuthorityService caService,
    IServiceScopeFactory scopeFactory,
    ILogger<TenantCertificateService> logger)
{
    // ═══════════════════════════════════════════════════════
    // Sub-CA Provisioning
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Provision a dedicated Sub-CA for a tenant, signed by the specified Root CA.
    /// Idempotent — returns existing TenantSubCa if already provisioned.
    /// </summary>
    public async Task<TenantSubCa> ProvisionTenantSubCaAsync(
        Guid tenantId, Guid rootCaId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        // Check if already provisioned
        var existing = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct);

        if (existing is not null)
        {
            if (existing.Status == TenantSubCaStatus.Active)
            {
                logger.LogInformation("Tenant {TenantId} already has active Sub-CA {CaId}",
                    tenantId, existing.CertificateAuthorityId);
                return existing;
            }

            // Re-activate if suspended
            if (existing.Status == TenantSubCaStatus.Suspended)
            {
                existing.Activate();
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Re-activated Sub-CA for tenant {TenantId}", tenantId);
                return existing;
            }

            // Revoked → must create a new one (fall through)
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found");

        // Verify root CA exists and is active
        var rootCa = await caService.GetCaAsync(rootCaId, ct)
            ?? throw new InvalidOperationException($"Root CA {rootCaId} not found");

        if (rootCa.Status != CaStatus.Active)
            throw new InvalidOperationException($"Root CA '{rootCa.Name}' is not active");

        // Create Intermediate CA via the existing CertificateAuthorityService
        var workerPrefix = $"PDFSigner_{SanitizeSlug(tenant.Slug)}";
        var caName = $"Tenant-{tenant.Slug}-SubCA";
        var commonName = $"{tenant.Name} Signing CA";

        var intermediateCa = await caService.CreateIntermediateCaAsync(new CreateIntermediateCaRequest(
            ParentCaId: rootCaId,
            Name: caName,
            CommonName: commonName,
            Organization: tenant.Name,
            OrgUnit: "Digital Signing",
            Country: "VN",
            State: null,
            Locality: null,
            KeyAlgorithm: "RSA",
            KeySize: 4096,
            ValidityDays: 1825 // 5 years
        ), ct);

        // Create TenantSubCa linking record
        var tenantSubCa = TenantSubCa.Create(
            tenantId: tenant.Id,
            certificateAuthorityId: intermediateCa.Id,
            workerNamePrefix: workerPrefix,
            organizationName: tenant.Name);

        db.TenantSubCas.Add(tenantSubCa);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Provisioned Sub-CA for tenant {TenantSlug}: CA={CaName}, prefix={Prefix}",
            tenant.Slug, caName, workerPrefix);

        return tenantSubCa;
    }

    // ═══════════════════════════════════════════════════════
    // User Certificate Provisioning (Sub-CA issued)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Provision a signing certificate for a user under their tenant's Sub-CA.
    /// Issues the cert via CertificateAuthorityService (not keytool self-signed) and
    /// creates a SignServer worker with tenant-scoped naming.
    /// </summary>
    public async Task<TenantCertProvisionResult> ProvisionUserCertAsync(
        User user, Guid tenantId, DigitalSigningOptions opts, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var tenantSubCa = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .Include(t => t.Tenant)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct)
            ?? throw new InvalidOperationException(
                "Tenant chưa có Sub-CA. Vui lòng liên hệ admin để cấp Sub-CA trước.");

        if (tenantSubCa.Status != TenantSubCaStatus.Active)
            throw new InvalidOperationException(
                $"Sub-CA của tenant đang ở trạng thái {tenantSubCa.Status}. Không thể cấp chứng thư.");

        if (tenantSubCa.ActiveWorkerCount >= tenantSubCa.MaxWorkers)
            throw new InvalidOperationException(
                $"Tenant đã đạt giới hạn {tenantSubCa.MaxWorkers} worker. Liên hệ admin để tăng quota.");

        // Tenant-aware worker naming to avoid cross-tenant collision
        var sanitizedUsername = UserSignatureEndpoints.SanitizeWorkerId(user.Username);
        var workerName = $"{tenantSubCa.WorkerNamePrefix}_{sanitizedUsername}";

        // Wider worker ID range (1000–9999) using both tenantId + userId
        var workerId = 1000 + Math.Abs(HashCode.Combine(tenantId, user.Id) % 9000);

        var sanitizedName = UserSignatureEndpoints.SanitizeForDN(user.FullName);
        var certDN = $"CN={sanitizedName}, O={tenantSubCa.OrganizationName}, OU={user.Role ?? "Staff"}, C=VN";

        var validityDays = tenantSubCa.DefaultCertValidityDays;

        logger.LogInformation(
            "Provisioning Sub-CA cert for user {User} (tenant={TenantId}), worker={Worker}, ID={WorkerId}",
            user.FullName, tenantId, workerName, workerId);

        // Issue certificate via CertificateAuthorityService using tenant's Sub-CA
        var managedCert = await caService.IssueCertificateAsync(new IssueCertRequest(
            CaId: tenantSubCa.CertificateAuthorityId,
            CommonName: sanitizedName,
            SubjectAltNames: null,
            Type: CertType.Client,
            Purpose: $"pdf-signing:{workerName}",
            ValidityDays: validityDays,
            KeySize: 2048,
            RenewBeforeDays: tenantSubCa.RenewBeforeDays,
            KeyAlgorithm: "RSA"
        ), ct);

        // Export cert + key as PKCS#12 and deploy to SignServer as worker
        await DeployWorkerToSignServerAsync(
            managedCert, workerId, workerName, certDN, validityDays, opts, db);

        // Update worker count
        tenantSubCa.IncrementWorkerCount();
        await db.SaveChangesAsync(ct);

        var expiry = managedCert.NotAfter;
        logger.LogInformation(
            "Sub-CA cert provisioned: {Subject}, worker={Worker}, serial={Serial}, expires={Expiry}",
            certDN, workerName, managedCert.SerialNumber, expiry);

        return new TenantCertProvisionResult(
            CertSubject: certDN,
            SerialNumber: managedCert.SerialNumber,
            Expiry: expiry,
            WorkerName: workerName,
            ManagedCertId: managedCert.Id,
            CertificateAuthorityId: tenantSubCa.CertificateAuthorityId);
    }

    // ═══════════════════════════════════════════════════════
    // Tenant CA Status & Management
    // ═══════════════════════════════════════════════════════

    /// <summary>Get the Sub-CA status for a tenant.</summary>
    public async Task<TenantSubCaStatusDto?> GetTenantCaStatusAsync(Guid tenantId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var tenantSubCa = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .Include(t => t.CertificateAuthority)
            .Include(t => t.Tenant)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct);

        if (tenantSubCa?.CertificateAuthority is null) return null;

        var ca = tenantSubCa.CertificateAuthority;

        // Count active user certs issued by this Sub-CA
        var activeCerts = await db.ManagedCertificates
            .CountAsync(c => c.IssuingCaId == ca.Id && c.Status == ManagedCertStatus.Active, ct);

        return new TenantSubCaStatusDto(
            TenantId: tenantSubCa.TenantId,
            TenantName: tenantSubCa.Tenant?.Name ?? "",
            CaId: ca.Id,
            CaName: ca.Name,
            CaCommonName: ca.CommonName,
            CaFingerprint: ca.Fingerprint,
            CaNotBefore: ca.NotBefore,
            CaNotAfter: ca.NotAfter,
            CaStatus: ca.Status,
            SubCaStatus: tenantSubCa.Status,
            WorkerNamePrefix: tenantSubCa.WorkerNamePrefix,
            OrganizationName: tenantSubCa.OrganizationName,
            ActiveWorkerCount: tenantSubCa.ActiveWorkerCount,
            MaxWorkers: tenantSubCa.MaxWorkers,
            ActiveCertCount: activeCerts,
            DefaultCertValidityDays: tenantSubCa.DefaultCertValidityDays,
            RenewBeforeDays: tenantSubCa.RenewBeforeDays,
            AutoProvisionEnabled: tenantSubCa.AutoProvisionEnabled);
    }

    /// <summary>List all tenant Sub-CAs for admin overview.</summary>
    public async Task<List<TenantSubCaStatusDto>> ListAllTenantCasAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var tenantSubCas = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted)
            .Include(t => t.CertificateAuthority)
            .Include(t => t.Tenant)
            .ToListAsync(ct);

        var result = new List<TenantSubCaStatusDto>();
        foreach (var tsc in tenantSubCas)
        {
            if (tsc.CertificateAuthority is null) continue;
            var ca = tsc.CertificateAuthority;

            var activeCerts = await db.ManagedCertificates
                .CountAsync(c => c.IssuingCaId == ca.Id && c.Status == ManagedCertStatus.Active, ct);

            result.Add(new TenantSubCaStatusDto(
                TenantId: tsc.TenantId,
                TenantName: tsc.Tenant?.Name ?? "",
                CaId: ca.Id,
                CaName: ca.Name,
                CaCommonName: ca.CommonName,
                CaFingerprint: ca.Fingerprint,
                CaNotBefore: ca.NotBefore,
                CaNotAfter: ca.NotAfter,
                CaStatus: ca.Status,
                SubCaStatus: tsc.Status,
                WorkerNamePrefix: tsc.WorkerNamePrefix,
                OrganizationName: tsc.OrganizationName,
                ActiveWorkerCount: tsc.ActiveWorkerCount,
                MaxWorkers: tsc.MaxWorkers,
                ActiveCertCount: activeCerts,
                DefaultCertValidityDays: tsc.DefaultCertValidityDays,
                RenewBeforeDays: tsc.RenewBeforeDays,
                AutoProvisionEnabled: tsc.AutoProvisionEnabled));
        }

        return result;
    }

    /// <summary>Update Sub-CA configuration for a tenant.</summary>
    public async Task UpdateTenantCaConfigAsync(
        Guid tenantId, int? validityDays, int? renewBefore, int? maxWorkers, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var tenantSubCa = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct)
            ?? throw new InvalidOperationException("Tenant chưa có Sub-CA");

        tenantSubCa.UpdateConfig(validityDays, renewBefore, maxWorkers);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Updated Sub-CA config for tenant {TenantId}: validity={V}, renew={R}, max={M}",
            tenantId, validityDays, renewBefore, maxWorkers);
    }

    /// <summary>Suspend a tenant's Sub-CA — prevents new cert issuance.</summary>
    public async Task SuspendTenantCaAsync(Guid tenantId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var tenantSubCa = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct)
            ?? throw new InvalidOperationException("Tenant chưa có Sub-CA");

        tenantSubCa.Suspend();
        await db.SaveChangesAsync(ct);

        logger.LogWarning("Suspended Sub-CA for tenant {TenantId}", tenantId);
    }

    /// <summary>
    /// Revoke a tenant's Sub-CA — invalidates ALL certificates issued by it.
    /// This is the nuclear option for tenant offboarding.
    /// </summary>
    public async Task RevokeTenantCaAsync(Guid tenantId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var tenantSubCa = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct)
            ?? throw new InvalidOperationException("Tenant chưa có Sub-CA");

        // Revoke the Intermediate CA itself (cascades to all issued certs logically)
        await caService.RevokeCertificateAuthorityAsync(tenantSubCa.CertificateAuthorityId, ct);

        tenantSubCa.Revoke();
        await db.SaveChangesAsync(ct);

        logger.LogWarning("Revoked Sub-CA for tenant {TenantId} — all tenant certs invalidated", tenantId);
    }

    // ═══════════════════════════════════════════════════════
    // User Cert Renewal via Sub-CA
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Auto-renew expiring user signing certs across all tenants.
    /// Called by the cert lifecycle background service.
    /// </summary>
    public async Task<int> AutoRenewTenantUserCertsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var now = DateTime.UtcNow;
        var renewedCount = 0;

        // Find active tenant Sub-CAs
        var activeTenantCas = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted && t.Status == TenantSubCaStatus.Active)
            .ToListAsync(ct);

        foreach (var tsc in activeTenantCas)
        {
            // Find user certs issued by this Sub-CA that are expiring soon
            var expiringCerts = await db.ManagedCertificates
                .Where(c => c.IssuingCaId == tsc.CertificateAuthorityId
                    && c.Status == ManagedCertStatus.Active
                    && c.AutoRenewEnabled
                    && c.ReplacedByCertId == null
                    && c.Purpose.StartsWith("pdf-signing:"))
                .ToListAsync(ct);

            foreach (var cert in expiringCerts.Where(c => c.IsExpiringSoon()))
            {
                try
                {
                    await caService.RenewCertificateAsync(cert.Id, ct);
                    renewedCount++;
                    logger.LogInformation("Auto-renewed tenant user cert {CertId} (CN={CN}, tenant={Tenant})",
                        cert.Id, cert.CommonName, tsc.TenantId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to auto-renew cert {CertId} for tenant {TenantId}",
                        cert.Id, tsc.TenantId);
                }
            }
        }

        return renewedCount;
    }

    // ═══════════════════════════════════════════════════════
    // SignServer Worker Deployment
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Deploy a CA-issued certificate as a SignServer P12 worker.
    /// Exports the managed cert's PEM to PKCS#12, copies to SignServer container,
    /// and configures the worker properties.
    /// </summary>
    private async Task DeployWorkerToSignServerAsync(
        ManagedCertificate managedCert,
        int workerId,
        string workerName,
        string certDN,
        int validityDays,
        DigitalSigningOptions opts,
        IvfDbContext db)
    {
        var certPem = managedCert.CertificatePem;
        var keyPem = await caService.GetDecryptedPrivateKeyAsync(managedCert.Id, CancellationToken.None);

        const string keyDir = "/opt/keyfactor/persistent/keys";
        var keystorePassword = "changeit";
        var keystorePath = $"{keyDir}/{workerName.ToLowerInvariant()}.p12";
        var keyAlias = "signer";

        // Create PKCS#12 from PEM cert + key using .NET crypto
        using var cert = X509Certificate2.CreateFromPem(certPem, keyPem);
        var pfxBytes = cert.Export(X509ContentType.Pfx, keystorePassword);

        // Write PFX to temp file and copy to container
        var tempPfx = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempPfx, pfxBytes);

            // Ensure directory exists
            await UserSignatureEndpoints.RunDockerExecAsRootAsync("ivf-signserver",
                $"mkdir -p {keyDir} && chown 10001:root {keyDir} && chmod 700 {keyDir}",
                logger);

            // Copy PKCS#12 to container
            await UserSignatureEndpoints.RunProcessAsync("docker",
                $"cp \"{tempPfx}\" ivf-signserver:{keystorePath}", logger);

            // Set restrictive permissions
            await UserSignatureEndpoints.RunDockerExecAsRootAsync("ivf-signserver",
                $"chmod 400 {keystorePath} && chown 10001:root {keystorePath}", logger);
        }
        finally
        {
            File.Delete(tempPfx);
        }

        // Write worker properties
        var sanitizedName = UserSignatureEndpoints.SanitizeForDN(certDN);
        var propsContent =
            $"GLOB.WORKER{workerId}.CLASSPATH = org.signserver.module.pdfsigner.PDFSigner\n" +
            $"GLOB.WORKER{workerId}.SIGNERTOKEN.CLASSPATH = org.signserver.server.cryptotokens.P12CryptoToken\n" +
            $"WORKER{workerId}.NAME = {workerName}\n" +
            $"WORKER{workerId}.AUTHTYPE = org.signserver.server.ClientCertAuthorizer\n" +
            $"WORKER{workerId}.DEFAULTKEY = {keyAlias}\n" +
            $"WORKER{workerId}.KEYSTOREPATH = {keystorePath}\n" +
            $"WORKER{workerId}.KEYSTOREPASSWORD = {keystorePassword}\n";

        var tempProps = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempProps, propsContent);
            var containerPropsPath = $"/tmp/worker_{workerId}.properties";
            await UserSignatureEndpoints.RunProcessAsync("docker",
                $"cp \"{tempProps}\" ivf-signserver:{containerPropsPath}", logger);

            await UserSignatureEndpoints.RunDockerExecAsync("ivf-signserver",
                $"bin/signserver setproperties {containerPropsPath}", logger);

            // Clean up temp properties file (contains keystore password)
            await UserSignatureEndpoints.RunDockerExecAsync("ivf-signserver",
                $"rm -f {containerPropsPath}", logger);
        }
        finally
        {
            File.Delete(tempProps);
        }

        // Set additional properties
        await UserSignatureEndpoints.RunDockerExecAsync("ivf-signserver",
            $"bin/signserver setproperty {workerId} TYPE PROCESSABLE", logger);
        await UserSignatureEndpoints.RunDockerExecAsync("ivf-signserver",
            $"bin/signserver setproperty {workerId} CERTIFICATION_LEVEL NOT_CERTIFIED", logger);
        await UserSignatureEndpoints.RunDockerExecAsync("ivf-signserver",
            $"bin/signserver setproperty {workerId} ADD_VISIBLE_SIGNATURE false", logger);
        await UserSignatureEndpoints.RunDockerExecAsync("ivf-signserver",
            $"bin/signserver setproperty {workerId} REASON \"Ky boi {UserSignatureEndpoints.SanitizeForDN(certDN)}\"",
            logger);
        await UserSignatureEndpoints.RunDockerExecAsync("ivf-signserver",
            $"bin/signserver setproperty {workerId} LOCATION \"IVF Clinic\"", logger);

        // Add authorized API client certificate (mTLS) if configured
        if (!string.IsNullOrEmpty(opts.ClientCertificatePath) && File.Exists(opts.ClientCertificatePath))
        {
            try
            {
                using var apiCert = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                    .LoadPkcs12FromFile(opts.ClientCertificatePath, opts.ResolveClientCertificatePassword());
                var serial = apiCert.SerialNumber;
                var issuerDN = apiCert.Issuer;
                await UserSignatureEndpoints.RunDockerExecAsync("ivf-signserver",
                    $"bin/signserver addauthorizedclient {workerId} {serial} \"{issuerDN}\"", logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to add authorized client to worker {WorkerId}", workerId);
            }
        }

        // Reload and activate
        await UserSignatureEndpoints.RunDockerExecAsync("ivf-signserver",
            $"bin/signserver reload {workerId}", logger);
        await UserSignatureEndpoints.RunDockerExecAsync("ivf-signserver",
            $"bin/signserver activatecryptotoken {workerId} {keystorePassword}", logger);
    }

    // ═══════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════

    /// <summary>Sanitize tenant slug for use in worker names (alphanumeric + hyphens only).</summary>
    private static string SanitizeSlug(string slug)
        => new(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
}

// ═══════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════

public record TenantCertProvisionResult(
    string CertSubject,
    string? SerialNumber,
    DateTime Expiry,
    string WorkerName,
    Guid ManagedCertId,
    Guid CertificateAuthorityId);

public record TenantSubCaStatusDto(
    Guid TenantId,
    string TenantName,
    Guid CaId,
    string CaName,
    string CaCommonName,
    string CaFingerprint,
    DateTime CaNotBefore,
    DateTime CaNotAfter,
    CaStatus CaStatus,
    TenantSubCaStatus SubCaStatus,
    string WorkerNamePrefix,
    string OrganizationName,
    int ActiveWorkerCount,
    int MaxWorkers,
    int ActiveCertCount,
    int DefaultCertValidityDays,
    int RenewBeforeDays,
    bool AutoProvisionEnabled);

public record TenantCaConfigRequest(
    int? DefaultCertValidityDays,
    int? RenewBeforeDays,
    int? MaxWorkers);

public record ProvisionTenantCaRequest(Guid RootCaId);
