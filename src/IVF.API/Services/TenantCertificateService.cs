using System.Text.Json;
using IVF.API.Endpoints;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IVF.API.Services;

/// <summary>
/// Orchestrates per-tenant Sub-CA lifecycle and tenant-aware user certificate provisioning
/// via EJBCA (Certificate Authority) and SignServer (PDF signing).
/// 
/// Flow:
///   1. Admin provisions TenantSubCa record → references EJBCA CA name + profiles
///   2. User cert provisioning → EJBCA CLI: addendentity + batch → PKCS#12
///      → docker pipe to SignServer → configure worker
///   3. Worker naming: PDFSigner_{tenantSlug}_{sanitizedUsername}
///   4. Worker ID: hash(tenantId, userId) → range (1000–9999) to avoid bootstrap workers
///   5. Tenant offboarding → revoke all certs via EJBCA REST API
/// </summary>
public sealed class TenantCertificateService(
    IServiceScopeFactory scopeFactory,
    IOptions<DigitalSigningOptions> signingOptions,
    ILogger<TenantCertificateService> logger)
{
    private readonly DigitalSigningOptions _opts = signingOptions.Value;

    // ═══════════════════════════════════════════════════════
    // Sub-CA Provisioning (logical — registers EJBCA CA ref)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Register a TenantSubCa record linking a tenant to an EJBCA CA.
    /// Idempotent — returns existing if already provisioned.
    /// The EJBCA CA must already exist (created via EJBCA Admin UI or CLI).
    /// </summary>
    public async Task<TenantSubCa> ProvisionTenantSubCaAsync(
        Guid tenantId,
        string? caName,
        string? certProfileName,
        string? eeProfileName,
        CancellationToken ct)
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
                logger.LogInformation("Tenant {TenantId} already has active Sub-CA (EJBCA CA={CaName})",
                    tenantId, existing.EjbcaCaName);
                return existing;
            }

            if (existing.Status == TenantSubCaStatus.Suspended)
            {
                existing.Activate();
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Re-activated Sub-CA for tenant {TenantId}", tenantId);
                return existing;
            }
            // Revoked → create a new one (fall through)
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found");

        var ejbcaCa = caName ?? _opts.EjbcaDefaultCaName;
        var ejbcaCertProfile = certProfileName ?? _opts.EjbcaDefaultCertProfile;
        var ejbcaEeProfile = eeProfileName ?? _opts.EjbcaDefaultEeProfile;

        // Verify EJBCA CA exists via REST API
        await VerifyEjbcaCaExistsAsync(ejbcaCa);

        var workerPrefix = $"PDFSigner_{SanitizeSlug(tenant.Slug)}";

        var tenantSubCa = TenantSubCa.Create(
            tenantId: tenant.Id,
            ejbcaCaName: ejbcaCa,
            ejbcaCertProfileName: ejbcaCertProfile,
            ejbcaEeProfileName: ejbcaEeProfile,
            workerNamePrefix: workerPrefix,
            organizationName: tenant.Name);

        db.TenantSubCas.Add(tenantSubCa);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Provisioned Sub-CA for tenant {TenantSlug}: EJBCA CA={CaName}, profile={Profile}, prefix={Prefix}",
            tenant.Slug, ejbcaCa, ejbcaCertProfile, workerPrefix);

        return tenantSubCa;
    }

    // ═══════════════════════════════════════════════════════
    // User Certificate Provisioning (EJBCA enrollment)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Provision a signing certificate for a user under their tenant's EJBCA CA.
    /// Uses EJBCA CLI: addendentity → batch (P12) → docker pipe to SignServer → configure worker.
    /// </summary>
    public async Task<TenantCertProvisionResult> ProvisionUserCertAsync(
        User user, Guid tenantId, CancellationToken ct)
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
        var cn = sanitizedName;
        var org = tenantSubCa.OrganizationName;
        var ou = user.Role ?? "Staff";

        var eeUsername = $"ivf-signer-{SanitizeSlug(tenantSubCa.Tenant?.Slug ?? tenantId.ToString()[..8])}-{sanitizedUsername}";
        var keyFile = $"{workerName.ToLowerInvariant()}.p12";

        logger.LogInformation(
            "Enrolling EJBCA cert for user {User} (tenant={TenantId}), worker={Worker}, ID={WorkerId}, EE={EE}",
            user.FullName, tenantId, workerName, workerId, eeUsername);

        // Step 1: Create End Entity in EJBCA via CLI
        await EjbcaAddEndEntityAsync(
            eeUsername, cn, org, ou,
            tenantSubCa.EjbcaCaName,
            tenantSubCa.EjbcaCertProfileName,
            tenantSubCa.EjbcaEeProfileName);

        // Step 2: Generate PKCS#12 via EJBCA batch
        await EjbcaBatchEnrollAsync(eeUsername);

        // Step 3: Deploy P12 from EJBCA → SignServer via docker pipe
        await DeployP12ToSignServerAsync(eeUsername, keyFile);

        // Step 4: Normalize key alias
        var keyAlias = await NormalizeKeyAliasAsync(keyFile, workerName.ToLowerInvariant());

        // Step 5: Configure SignServer worker
        await ConfigureSignServerWorkerAsync(workerId, workerName, keyFile, keyAlias, cn, org);

        // Step 6: Cleanup EJBCA temp file
        await CleanupEjbcaTempAsync(eeUsername);

        // Update worker count
        tenantSubCa.IncrementWorkerCount();
        await db.SaveChangesAsync(ct);

        // Estimate expiry (EJBCA controls actual validity via cert profile)
        var estimatedExpiry = DateTime.UtcNow.AddDays(tenantSubCa.DefaultCertValidityDays);

        var certSubject = $"CN={cn}, O={org}, OU={ou}, C=VN";
        logger.LogInformation(
            "EJBCA cert enrolled: {Subject}, worker={Worker}, EE={EE}",
            certSubject, workerName, eeUsername);

        return new TenantCertProvisionResult(
            CertSubject: certSubject,
            EjbcaUsername: eeUsername,
            EstimatedExpiry: estimatedExpiry,
            WorkerName: workerName,
            WorkerId: workerId);
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
            .Include(t => t.Tenant)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct);

        if (tenantSubCa is null) return null;

        // Count active user signatures for this tenant
        var activeSignatures = await db.UserSignatures
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive
                && s.WorkerName != null)
            .CountAsync(ct);

        return new TenantSubCaStatusDto(
            TenantId: tenantSubCa.TenantId,
            TenantName: tenantSubCa.Tenant?.Name ?? "",
            EjbcaCaName: tenantSubCa.EjbcaCaName,
            EjbcaCertProfileName: tenantSubCa.EjbcaCertProfileName,
            EjbcaEeProfileName: tenantSubCa.EjbcaEeProfileName,
            SubCaStatus: tenantSubCa.Status,
            WorkerNamePrefix: tenantSubCa.WorkerNamePrefix,
            OrganizationName: tenantSubCa.OrganizationName,
            ActiveWorkerCount: tenantSubCa.ActiveWorkerCount,
            MaxWorkers: tenantSubCa.MaxWorkers,
            ActiveSignatureCount: activeSignatures,
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
            .Include(t => t.Tenant)
            .ToListAsync(ct);

        var result = new List<TenantSubCaStatusDto>();
        foreach (var tsc in tenantSubCas)
        {
            var activeSignatures = await db.UserSignatures
                .Where(s => s.TenantId == tsc.TenantId && !s.IsDeleted && s.IsActive
                    && s.WorkerName != null)
                .CountAsync(ct);

            result.Add(new TenantSubCaStatusDto(
                TenantId: tsc.TenantId,
                TenantName: tsc.Tenant?.Name ?? "",
                EjbcaCaName: tsc.EjbcaCaName,
                EjbcaCertProfileName: tsc.EjbcaCertProfileName,
                EjbcaEeProfileName: tsc.EjbcaEeProfileName,
                SubCaStatus: tsc.Status,
                WorkerNamePrefix: tsc.WorkerNamePrefix,
                OrganizationName: tsc.OrganizationName,
                ActiveWorkerCount: tsc.ActiveWorkerCount,
                MaxWorkers: tsc.MaxWorkers,
                ActiveSignatureCount: activeSignatures,
                DefaultCertValidityDays: tsc.DefaultCertValidityDays,
                RenewBeforeDays: tsc.RenewBeforeDays,
                AutoProvisionEnabled: tsc.AutoProvisionEnabled));
        }

        return result;
    }

    /// <summary>Update Sub-CA configuration for a tenant.</summary>
    public async Task UpdateTenantCaConfigAsync(
        Guid tenantId, int? validityDays, int? renewBefore, int? maxWorkers,
        bool? autoProvision, string? ejbcaCaName, string? ejbcaCertProfile,
        string? ejbcaEeProfile, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var tenantSubCa = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct)
            ?? throw new InvalidOperationException("Tenant chưa có Sub-CA");

        tenantSubCa.UpdateConfig(validityDays, renewBefore, maxWorkers, autoProvision,
            ejbcaCaName, ejbcaCertProfile, ejbcaEeProfile);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Updated Sub-CA config for tenant {TenantId}: validity={V}, renew={R}, max={M}, auto={A}, ca={CA}",
            tenantId, validityDays, renewBefore, maxWorkers, autoProvision, ejbcaCaName);
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
    /// Revoke a tenant's certificates via EJBCA REST API.
    /// Searches all certs issued for the tenant's worker prefix and revokes them.
    /// </summary>
    public async Task RevokeTenantCaAsync(Guid tenantId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var tenantSubCa = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct)
            ?? throw new InvalidOperationException("Tenant chưa có Sub-CA");

        // Revoke all certificates via EJBCA REST API (search by CA name + username pattern)
        await RevokeAllTenantCertsViaEjbcaAsync(tenantSubCa);

        tenantSubCa.Revoke();
        await db.SaveChangesAsync(ct);

        logger.LogWarning("Revoked Sub-CA for tenant {TenantId} — all tenant certs revoked via EJBCA", tenantId);
    }

    /// <summary>Soft-delete a tenant's Sub-CA record (must be Revoked or Suspended first).</summary>
    public async Task DeleteTenantCaAsync(Guid tenantId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var tenantSubCa = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct)
            ?? throw new InvalidOperationException("Tenant chưa có Sub-CA");

        if (tenantSubCa.Status == TenantSubCaStatus.Active)
            throw new InvalidOperationException("Không thể xóa Sub-CA đang hoạt động. Hãy tạm dừng hoặc thu hồi trước.");

        tenantSubCa.MarkAsDeleted();
        await db.SaveChangesAsync(ct);

        logger.LogWarning("Deleted Sub-CA record for tenant {TenantId}", tenantId);
    }

    /// <summary>List all tenants that do not yet have a Sub-CA provisioned.</summary>
    public async Task<List<AvailableTenantDto>> ListAvailableTenantsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var tenantsWithCa = await db.Set<TenantSubCa>()
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted)
            .Select(t => t.TenantId)
            .ToListAsync(ct);

        var available = await db.Tenants
            .Where(t => !tenantsWithCa.Contains(t.Id))
            .Select(t => new AvailableTenantDto(t.Id, t.Name, t.Slug))
            .ToListAsync(ct);

        return available;
    }

    // ═══════════════════════════════════════════════════════
    // User Cert Renewal via EJBCA Re-enrollment
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Auto-renew expiring user signing certs across all tenants.
    /// Checks UserSignature.CertificateExpiry and re-enrolls via EJBCA.
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
            .Include(t => t.Tenant)
            .ToListAsync(ct);

        foreach (var tsc in activeTenantCas)
        {
            var renewThreshold = now.AddDays(tsc.RenewBeforeDays);

            // Find user signatures with expiring certs
            var expiringSignatures = await db.UserSignatures
                .Include(s => s.User)
                .Where(s => s.TenantId == tsc.TenantId
                    && !s.IsDeleted && s.IsActive
                    && s.WorkerName != null
                    && s.CertificateExpiry != null
                    && s.CertificateExpiry <= renewThreshold
                    && s.CertStatus != CertificateStatus.Revoked)
                .ToListAsync(ct);

            foreach (var sig in expiringSignatures)
            {
                if (sig.User is null) continue;
                try
                {
                    var result = await ProvisionUserCertAsync(sig.User, tsc.TenantId, ct);

                    sig.SetCertificateInfo(
                        subject: result.CertSubject,
                        serialNumber: result.EjbcaUsername,
                        expiry: result.EstimatedExpiry,
                        workerName: result.WorkerName,
                        keystorePath: null);
                    await db.SaveChangesAsync(ct);

                    renewedCount++;
                    logger.LogInformation("Auto-renewed tenant user cert via EJBCA: user={User}, tenant={Tenant}",
                        sig.User.FullName, tsc.TenantId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to auto-renew cert for user {UserId} in tenant {TenantId}",
                        sig.UserId, tsc.TenantId);
                }
            }
        }

        return renewedCount;
    }

    // ═══════════════════════════════════════════════════════
    // EJBCA CLI Operations (via docker exec)
    // ═══════════════════════════════════════════════════════

    /// <summary>Create/update End Entity in EJBCA via CLI.</summary>
    private async Task EjbcaAddEndEntityAsync(
        string eeUsername, string cn, string org, string ou,
        string caName, string certProfile, string eeProfile)
    {
        var ejbcaContainer = _opts.EjbcaContainerName;
        var keystorePassword = _opts.EjbcaKeystorePassword;
        var dn = $"CN={cn},O={org},OU={ou},C=VN";

        // Try to add new End Entity
        var (addExit, _) = await RunDockerExecWithExitCodeAsync(ejbcaContainer,
            $"/opt/keyfactor/bin/ejbca.sh ra addendentity " +
            $"--username \"{eeUsername}\" " +
            $"--dn \"{dn}\" " +
            $"--caname \"{caName}\" " +
            $"--type 1 " +
            $"--token P12 " +
            $"--password \"{keystorePassword}\" " +
            $"--certprofile \"{certProfile}\" " +
            $"--eeprofile \"{eeProfile}\"");

        if (addExit != 0)
        {
            // Check if entity actually exists before assuming re-enrollment
            var (findExit, _) = await RunDockerExecWithExitCodeAsync(ejbcaContainer,
                $"/opt/keyfactor/bin/ejbca.sh ra findendentity --username \"{eeUsername}\"");

            if (findExit != 0)
                throw new InvalidOperationException(
                    $"EJBCA addendentity failed for '{eeUsername}'. Verify CA '{caName}', certProfile '{certProfile}', eeProfile '{eeProfile}' exist and are correctly configured.");

            // Entity exists — reset status to NEW (10) for re-enrollment
            await UserSignatureEndpoints.RunDockerExecAsync(ejbcaContainer,
                $"/opt/keyfactor/bin/ejbca.sh ra setendentitystatus \"{eeUsername}\" 10",
                logger);
            logger.LogInformation("EJBCA End Entity {EE} already exists — reset for re-enrollment", eeUsername);
        }

        // Set clear-text password (required for PKCS#12 batch generation)
        await UserSignatureEndpoints.RunDockerExecAsync(ejbcaContainer,
            $"/opt/keyfactor/bin/ejbca.sh ra setclearpwd \"{eeUsername}\" \"{keystorePassword}\"",
            logger);
    }

    /// <summary>Generate PKCS#12 keystore via EJBCA batch command.</summary>
    private async Task EjbcaBatchEnrollAsync(string eeUsername)
    {
        var ejbcaContainer = _opts.EjbcaContainerName;

        // Ensure temp directory exists
        await UserSignatureEndpoints.RunDockerExecAsync(ejbcaContainer,
            "mkdir -p /tmp/ejbca-certs", logger);

        // Batch generate PKCS#12 — capture output for error diagnostics
        var batchOutput = await UserSignatureEndpoints.RunProcessAsync("docker",
            $"exec {ejbcaContainer} /opt/keyfactor/bin/ejbca.sh batch --username \"{eeUsername}\" -dir /tmp/ejbca-certs",
            logger);

        // Verify P12 was generated
        var p12Path = $"/tmp/ejbca-certs/{eeUsername}.p12";
        var (exitCode, _) = await RunDockerExecWithExitCodeAsync(ejbcaContainer,
            $"test -f {p12Path}");

        if (exitCode != 0)
        {
            // Extract actionable error from EJBCA output
            var errorDetail = ExtractEjbcaError(batchOutput);
            throw new InvalidOperationException(
                $"EJBCA batch enrollment failed for '{eeUsername}': {errorDetail}");
        }
    }

    /// <summary>Copy PKCS#12 from EJBCA container to SignServer container via docker pipe.</summary>
    private async Task DeployP12ToSignServerAsync(string eeUsername, string keyFile)
    {
        var ejbcaContainer = _opts.EjbcaContainerName;
        var signServerContainer = _opts.SignServerContainerName;
        var p12Path = $"/tmp/ejbca-certs/{eeUsername}.p12";
        const string keyDir = "/opt/keyfactor/persistent/keys";

        // Docker pipe: EJBCA → SignServer (avoids host filesystem)
        await UserSignatureEndpoints.RunProcessAsync("docker",
            $"exec {ejbcaContainer} cat {p12Path}", logger);

        // Use a combined command to pipe between containers
        var pipeCommand = $"exec {ejbcaContainer} cat {p12Path}";
        // Since RunProcessAsync doesn't support piping, use shell approach
        var isWindows = OperatingSystem.IsWindows();
        if (isWindows)
        {
            await UserSignatureEndpoints.RunProcessAsync("cmd",
                $"/c \"docker exec {ejbcaContainer} cat {p12Path} | docker exec -i {signServerContainer} bash -c \\\"cat > /tmp/_deploy_p12\\\"\"",
                logger);
        }
        else
        {
            await UserSignatureEndpoints.RunProcessAsync("bash",
                $"-c \"docker exec {ejbcaContainer} cat {p12Path} | docker exec -i {signServerContainer} bash -c 'cat > /tmp/_deploy_p12'\"",
                logger);
        }

        // Move to final location with correct permissions
        await UserSignatureEndpoints.RunDockerExecAsRootAsync(signServerContainer,
            $"mkdir -p {keyDir} && " +
            $"rm -f '{keyDir}/{keyFile}' && " +
            $"cp /tmp/_deploy_p12 '{keyDir}/{keyFile}' && " +
            $"rm -f /tmp/_deploy_p12 && " +
            $"chmod 400 '{keyDir}/{keyFile}' && " +
            $"chown 10001:root '{keyDir}/{keyFile}'",
            logger);
    }

    /// <summary>Normalize PKCS#12 key alias (EJBCA uses CN which may contain spaces).</summary>
    private async Task<string> NormalizeKeyAliasAsync(string keyFile, string desiredAlias)
    {
        var signServerContainer = _opts.SignServerContainerName;
        var keystorePassword = _opts.EjbcaKeystorePassword;
        const string keyDir = "/opt/keyfactor/persistent/keys";
        var keystorePath = $"{keyDir}/{keyFile}";

        // Get current alias
        var (_, output) = await RunDockerExecWithExitCodeAsync(signServerContainer,
            $"keytool -list -keystore {keystorePath} -storepass {keystorePassword} -storetype PKCS12");

        var currentAlias = "";
        if (output is not null)
        {
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("PrivateKeyEntry", StringComparison.OrdinalIgnoreCase))
                {
                    currentAlias = line.Split(',')[0].Trim();
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(currentAlias) && currentAlias != desiredAlias)
        {
            await UserSignatureEndpoints.RunDockerExecAsRootAsync(signServerContainer,
                $"chmod 600 '{keystorePath}' && " +
                $"keytool -changealias -keystore '{keystorePath}' " +
                $"-storepass '{keystorePassword}' -storetype PKCS12 " +
                $"-alias '{currentAlias}' -destalias '{desiredAlias}' && " +
                $"chmod 400 '{keystorePath}'",
                logger);
            logger.LogInformation("Key alias normalized: {Old} → {New}", currentAlias, desiredAlias);
            return desiredAlias;
        }

        return string.IsNullOrEmpty(currentAlias) ? desiredAlias : currentAlias;
    }

    /// <summary>Configure SignServer worker properties for the deployed PKCS#12.</summary>
    private async Task ConfigureSignServerWorkerAsync(
        int workerId, string workerName, string keyFile, string keyAlias,
        string cn, string org)
    {
        var signServerContainer = _opts.SignServerContainerName;
        var keystorePassword = _opts.EjbcaKeystorePassword;
        const string keyDir = "/opt/keyfactor/persistent/keys";
        var keystorePath = $"{keyDir}/{keyFile}";
        const string signerCli = "bin/signserver";

        // Write worker properties file
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
                $"cp \"{tempProps}\" {signServerContainer}:{containerPropsPath}", logger);

            await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
                $"{signerCli} setproperties {containerPropsPath}", logger);

            // Clean up temp properties file (contains keystore password)
            await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
                $"rm -f {containerPropsPath}", logger);
        }
        finally
        {
            File.Delete(tempProps);
        }

        // Set additional properties
        await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
            $"{signerCli} setproperty {workerId} TYPE PROCESSABLE", logger);
        await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
            $"{signerCli} setproperty {workerId} KEYSTOREPATH {keystorePath}", logger);
        await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
            $"{signerCli} setproperty {workerId} KEYSTOREPASSWORD {keystorePassword}", logger);

        if (!string.IsNullOrEmpty(keyAlias))
        {
            await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
                $"{signerCli} setproperty {workerId} DEFAULTKEY {keyAlias}", logger);
        }

        await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
            $"{signerCli} setproperty {workerId} CERTIFICATION_LEVEL NOT_CERTIFIED", logger);
        await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
            $"{signerCli} setproperty {workerId} ADD_VISIBLE_SIGNATURE false", logger);
        await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
            $"{signerCli} setproperty {workerId} REASON \"Ky boi {cn}\"", logger);
        await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
            $"{signerCli} setproperty {workerId} LOCATION \"{org}\"", logger);

        // Add authorized API client certificate (mTLS) if configured
        if (!string.IsNullOrEmpty(_opts.ClientCertificatePath) && File.Exists(_opts.ClientCertificatePath))
        {
            try
            {
                using var apiCert = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                    .LoadPkcs12FromFile(_opts.ClientCertificatePath, _opts.ResolveClientCertificatePassword());
                var serial = apiCert.SerialNumber;
                var issuerDN = apiCert.Issuer;
                await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
                    $"{signerCli} addauthorizedclient {workerId} {serial} \"{issuerDN}\"", logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to add authorized client to worker {WorkerId}", workerId);
            }
        }

        // Activate crypto token and reload
        await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
            $"{signerCli} activatecryptotoken {workerId} {keystorePassword}", logger);
        await UserSignatureEndpoints.RunDockerExecAsync(signServerContainer,
            $"{signerCli} reload {workerId}", logger);
    }

    /// <summary>Cleanup temporary EJBCA enrollment files.</summary>
    private async Task CleanupEjbcaTempAsync(string eeUsername)
    {
        var p12Path = $"/tmp/ejbca-certs/{eeUsername}.p12";
        await UserSignatureEndpoints.RunDockerExecAsync(_opts.EjbcaContainerName,
            $"rm -f {p12Path}", logger);
    }

    // ═══════════════════════════════════════════════════════
    // EJBCA REST API Operations
    // ═══════════════════════════════════════════════════════

    /// <summary>Verify that an EJBCA CA exists via REST API.</summary>
    private async Task VerifyEjbcaCaExistsAsync(string caName)
    {
        var url = $"{_opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v1/ca";
        var (content, error) = await SigningAdminEndpoints.TryEjbcaRestCallAsync(_opts, url);

        if (error is not null)
        {
            logger.LogWarning("Cannot verify EJBCA CA '{CaName}': {Error}. Proceeding anyway.", caName, error);
            return; // Don't block provisioning if REST API is unavailable
        }

        if (content is not null)
        {
            using var doc = JsonDocument.Parse(content);
            var cas = doc.RootElement.TryGetProperty("certificate_authorities", out var casArray)
                ? casArray : doc.RootElement;

            var found = false;
            if (cas.ValueKind == JsonValueKind.Array)
            {
                foreach (var ca in cas.EnumerateArray())
                {
                    var name = ca.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.Equals(name, caName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
                logger.LogWarning("EJBCA CA '{CaName}' not found in REST API response. Verify it exists.", caName);
        }
    }

    /// <summary>Revoke all certificates issued for a tenant via EJBCA REST API.</summary>
    private async Task RevokeAllTenantCertsViaEjbcaAsync(TenantSubCa tenantSubCa)
    {
        // Search for certs by CA name using EJBCA REST API v2
        var searchUrl = $"{_opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v2/certificate/search";
        var searchBody = JsonSerializer.Serialize(new
        {
            max_number_of_results = 100,
            criteria = new[]
            {
                new { property = "CA", value = tenantSubCa.EjbcaCaName, operation = "EQUAL" },
                new { property = "STATUS", value = "CERT_ACTIVE", operation = "EQUAL" }
            }
        });

        var (content, error) = await SigningAdminEndpoints.TryEjbcaRestCallAsync(
            _opts, searchUrl, HttpMethod.Post,
            new StringContent(searchBody, System.Text.Encoding.UTF8, "application/json"));

        if (error is not null)
        {
            logger.LogWarning("Cannot search EJBCA certs for revocation: {Error}", error);
            return;
        }

        if (content is null) return;

        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty("certificates", out var certs)) return;

        foreach (var cert in certs.EnumerateArray())
        {
            var serial = cert.TryGetProperty("serialNumber", out var sn) ? sn.GetString() : null;
            var issuerDn = cert.TryGetProperty("issuerDN", out var iss) ? iss.GetString() : null;

            if (serial is null || issuerDn is null) continue;

            // Only revoke certs matching tenant's worker prefix pattern
            var username = cert.TryGetProperty("username", out var un) ? un.GetString() : null;
            var tenantSlug = tenantSubCa.Tenant?.Slug ?? "";
            if (username is not null && !username.Contains(SanitizeSlug(tenantSlug))) continue;

            var revokeUrl = $"{_opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v1/certificate/{Uri.EscapeDataString(issuerDn)}/{serial}/revoke?reason=CESSATION_OF_OPERATION";
            var (_, revokeError) = await SigningAdminEndpoints.TryEjbcaRestCallAsync(
                _opts, revokeUrl, HttpMethod.Put);

            if (revokeError is not null)
                logger.LogWarning("Failed to revoke cert {Serial}: {Error}", serial, revokeError);
            else
                logger.LogInformation("Revoked EJBCA cert {Serial} for tenant {Tenant}", serial, tenantSubCa.TenantId);
        }
    }

    // ═══════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════

    /// <summary>Sanitize tenant slug for use in worker names and EE usernames.</summary>
    private static string SanitizeSlug(string slug)
        => new(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

    /// <summary>Run docker exec and capture exit code + output.</summary>
    private static async Task<(int ExitCode, string? Output)> RunDockerExecWithExitCodeAsync(
        string containerName, string command)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("docker",
            $"exec {containerName} {command}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null) return (-1, null);

        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout);
    }

    /// <summary>Extract actionable error from EJBCA CLI output (stderr+stdout combined).</summary>
    private static string ExtractEjbcaError(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return "No output from EJBCA batch command.";

        // Look for common EJBCA error patterns
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("is not active", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            if (trimmed.Contains("EJBException:", StringComparison.OrdinalIgnoreCase))
                return trimmed.Split("EJBException:")[^1].Trim();
            if (trimmed.Contains("ERROR") && !trimmed.Contains("setting status to FAILED"))
                return trimmed;
        }

        // Fallback: return last non-empty line
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 0 ? lines[^1].Trim() : "Unknown EJBCA error.";
    }
}

// ═══════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════

public record TenantCertProvisionResult(
    string CertSubject,
    string EjbcaUsername,
    DateTime EstimatedExpiry,
    string WorkerName,
    int WorkerId);

public record TenantSubCaStatusDto(
    Guid TenantId,
    string TenantName,
    string EjbcaCaName,
    string EjbcaCertProfileName,
    string EjbcaEeProfileName,
    TenantSubCaStatus SubCaStatus,
    string WorkerNamePrefix,
    string OrganizationName,
    int ActiveWorkerCount,
    int MaxWorkers,
    int ActiveSignatureCount,
    int DefaultCertValidityDays,
    int RenewBeforeDays,
    bool AutoProvisionEnabled);

public record TenantCaConfigRequest(
    int? DefaultCertValidityDays,
    int? RenewBeforeDays,
    int? MaxWorkers,
    bool? AutoProvisionEnabled,
    string? EjbcaCaName,
    string? EjbcaCertProfileName,
    string? EjbcaEeProfileName);

public record ProvisionTenantCaRequest(
    string? CaName,
    string? CertProfileName,
    string? EeProfileName);

public record AvailableTenantDto(
    Guid Id,
    string Name,
    string Slug);
