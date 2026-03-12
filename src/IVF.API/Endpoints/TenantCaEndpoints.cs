using IVF.API.Services;
using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IVF.API.Endpoints;

/// <summary>
/// Admin endpoints for managing per-tenant Sub-CAs (EJBCA-backed) and tenant certificate lifecycle.
/// Platform admin can provision, configure, suspend, and revoke tenant Sub-CAs.
/// </summary>
public static class TenantCaEndpoints
{
    public static void MapTenantCaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/tenant-ca")
            .WithTags("Tenant Certificate Authority")
            .RequireAuthorization("AdminOnly");

        // ─── List all tenant Sub-CAs ────────────────────────────
        group.MapGet("/", async (
            TenantCertificateService tenantCaService,
            CancellationToken ct) =>
        {
            var items = await tenantCaService.ListAllTenantCasAsync(ct);
            return Results.Ok(new { items, total = items.Count });
        })
        .WithName("ListTenantCAs");

        // ─── List tenants without a Sub-CA (for provisioning) ───
        group.MapGet("/available-tenants", async (
            TenantCertificateService tenantCaService,
            CancellationToken ct) =>
        {
            var tenants = await tenantCaService.ListAvailableTenantsAsync(ct);
            return Results.Ok(tenants);
        })
        .WithName("ListAvailableTenants");

        // ─── Get specific tenant's Sub-CA status ────────────────
        group.MapGet("/{tenantId:guid}", async (
            Guid tenantId,
            TenantCertificateService tenantCaService,
            CancellationToken ct) =>
        {
            var status = await tenantCaService.GetTenantCaStatusAsync(tenantId, ct);
            if (status is null)
                return Results.NotFound(new { error = "Tenant chưa có Sub-CA" });
            return Results.Ok(status);
        })
        .WithName("GetTenantCA");

        // ─── Provision Sub-CA for a tenant (registers EJBCA CA reference) ───
        group.MapPost("/{tenantId:guid}/provision", async (
            Guid tenantId,
            ProvisionTenantCaRequest request,
            TenantCertificateService tenantCaService,
            CancellationToken ct) =>
        {
            try
            {
                var result = await tenantCaService.ProvisionTenantSubCaAsync(
                    tenantId, request.CaName, request.CertProfileName, request.EeProfileName, ct);

                var status = await tenantCaService.GetTenantCaStatusAsync(tenantId, ct);
                return Results.Created($"/api/admin/tenant-ca/{tenantId}", new
                {
                    success = true,
                    message = "Đã cấp Sub-CA cho tenant",
                    status
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ProvisionTenantCA");

        // ─── Update tenant Sub-CA config ────────────────────────
        group.MapPut("/{tenantId:guid}/config", async (
            Guid tenantId,
            TenantCaConfigRequest request,
            TenantCertificateService tenantCaService,
            CancellationToken ct) =>
        {
            try
            {
                await tenantCaService.UpdateTenantCaConfigAsync(
                    tenantId,
                    request.DefaultCertValidityDays,
                    request.RenewBeforeDays,
                    request.MaxWorkers,
                    request.AutoProvisionEnabled,
                    request.EjbcaCaName,
                    request.EjbcaCertProfileName,
                    request.EjbcaEeProfileName,
                    ct);

                var status = await tenantCaService.GetTenantCaStatusAsync(tenantId, ct);
                return Results.Ok(new { success = true, status });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpdateTenantCAConfig");

        // ─── Suspend tenant Sub-CA ──────────────────────────────
        group.MapPost("/{tenantId:guid}/suspend", async (
            Guid tenantId,
            TenantCertificateService tenantCaService,
            CancellationToken ct) =>
        {
            try
            {
                await tenantCaService.SuspendTenantCaAsync(tenantId, ct);
                return Results.Ok(new { success = true, message = "Đã tạm ngưng Sub-CA của tenant" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SuspendTenantCA");

        // ─── Revoke tenant Sub-CA (revokes all tenant certs via EJBCA) ──────
        group.MapPost("/{tenantId:guid}/revoke", async (
            Guid tenantId,
            TenantCertificateService tenantCaService,
            CancellationToken ct) =>
        {
            try
            {
                await tenantCaService.RevokeTenantCaAsync(tenantId, ct);
                return Results.Ok(new
                {
                    success = true,
                    message = "Đã thu hồi Sub-CA và toàn bộ chứng thư của tenant"
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RevokeTenantCA");

        // ─── Delete (soft-delete) tenant Sub-CA ─────────────────
        group.MapDelete("/{tenantId:guid}", async (
            Guid tenantId,
            TenantCertificateService tenantCaService,
            CancellationToken ct) =>
        {
            try
            {
                await tenantCaService.DeleteTenantCaAsync(tenantId, ct);
                return Results.Ok(new { success = true, message = "Đã xóa Sub-CA của tenant" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DeleteTenantCA");

        // ─── List SignServer workers for a tenant (filtered by prefix) ──────
        group.MapGet("/{tenantId:guid}/workers", async (
            Guid tenantId,
            TenantCertificateService tenantCaService,
            IOptions<DigitalSigningOptions> options,
            CancellationToken ct) =>
        {
            var opts = options.Value;
            if (!opts.Enabled)
                return Results.Ok(new { workers = Array.Empty<object>(), error = "Ký số chưa được bật" });

            var tenantCa = await tenantCaService.GetTenantCaStatusAsync(tenantId, ct);
            if (tenantCa is null)
                return Results.NotFound(new { error = "Tenant chưa có Sub-CA" });

            // Get all SignServer workers via CLI
            var (output, error) = await RunSignServerCliAsync(opts, "getstatus brief all");
            if (error != null)
                return Results.Ok(new { workers = Array.Empty<object>(), prefix = tenantCa.WorkerNamePrefix, error });

            var allWorkers = SigningAdminEndpoints.ParseGetStatusBriefOutput(output!);

            // Filter by tenant's worker prefix
            var tenantWorkers = allWorkers
                .Where(w => w.Name.StartsWith(tenantCa.WorkerNamePrefix, StringComparison.OrdinalIgnoreCase))
                .Select(w => new
                {
                    w.Id,
                    w.Name,
                    workerStatus = w.WorkerStatus,
                    tokenStatus = w.TokenStatus,
                    w.Signings
                })
                .ToList();

            return Results.Ok(new { workers = tenantWorkers, prefix = tenantCa.WorkerNamePrefix });
        })
        .WithName("GetTenantWorkers");

        // ─── List enrolled users (users with active cert) for a tenant ──────
        group.MapGet("/{tenantId:guid}/enrolled-users", async (
            Guid tenantId,
            IVF.Infrastructure.Persistence.IvfDbContext db,
            CancellationToken ct) =>
        {
            var enrolledUsers = await db.UserSignatures
                .Include(s => s.User)
                .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive
                    && s.WorkerName != null)
                .Select(s => new
                {
                    userId = s.UserId,
                    fullName = s.User != null ? s.User.FullName : "",
                    username = s.User != null ? s.User.Username : "",
                    role = s.User != null ? s.User.Role : "",
                    workerName = s.WorkerName,
                    certificateSubject = s.CertificateSubject,
                    certificateExpiry = s.CertificateExpiry,
                    certStatus = s.CertStatus.ToString(),
                    createdAt = s.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(new { items = enrolledUsers, total = enrolledUsers.Count });
        })
        .WithName("GetTenantEnrolledUsers");

        // ─── Provision user cert via tenant Sub-CA (EJBCA enrollment) ───────
        // Enforces 1 user = 1 active certificate
        group.MapPost("/{tenantId:guid}/users/{userId:guid}/provision", async (
            Guid tenantId,
            Guid userId,
            TenantCertificateService tenantCaService,
            IVF.Infrastructure.Persistence.IvfDbContext db,
            Microsoft.Extensions.Options.IOptions<DigitalSigningOptions> options,
            CancellationToken ct) =>
        {
            var opts = options.Value;
            if (!opts.Enabled)
                return Results.BadRequest(new { error = "Ký số chưa được bật" });

            var user = await db.Users.FindAsync([userId], ct);
            if (user is null)
                return Results.NotFound(new { error = "Người dùng không tồn tại" });

            // Find or create signature record
            var sig = await db.UserSignatures
                .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive)
                .FirstOrDefaultAsync(ct);

            if (sig is null)
                return Results.BadRequest(new { error = "Người dùng chưa có chữ ký tay. Yêu cầu tải chữ ký trước." });

            // ── 1 user = 1 certificate guard ──
            // Check if user already has an active certificate/worker
            if (!string.IsNullOrEmpty(sig.WorkerName) &&
                sig.CertStatus != CertificateStatus.Revoked &&
                sig.CertStatus != CertificateStatus.Error &&
                sig.CertStatus != CertificateStatus.Expired)
            {
                return Results.Conflict(new
                {
                    success = false,
                    error = $"Người dùng đã có chứng thư số đang hoạt động (Worker: {sig.WorkerName}). " +
                            "Mỗi người dùng chỉ được cấp 1 chứng thư. Hãy thu hồi chứng thư cũ trước khi cấp mới.",
                    existingWorkerName = sig.WorkerName,
                    existingCertSubject = sig.CertificateSubject,
                    existingCertExpiry = sig.CertificateExpiry,
                    existingCertStatus = sig.CertStatus.ToString()
                });
            }

            try
            {
                sig.SetCertificateStatus(CertificateStatus.Pending);
                await db.SaveChangesAsync(ct);

                var result = await tenantCaService.ProvisionUserCertAsync(user, tenantId, ct);

                sig.SetCertificateInfo(
                    subject: result.CertSubject,
                    serialNumber: result.EjbcaUsername,
                    expiry: result.EstimatedExpiry,
                    workerName: result.WorkerName,
                    keystorePath: null);

                await db.SaveChangesAsync(ct);

                return Results.Ok(new
                {
                    success = true,
                    certificateSubject = result.CertSubject,
                    workerName = result.WorkerName,
                    ejbcaUsername = result.EjbcaUsername,
                    estimatedExpiry = result.EstimatedExpiry,
                    workerId = result.WorkerId,
                    message = $"Đã cấp chứng thư EJBCA cho {user.FullName}"
                });
            }
            catch (Exception ex)
            {
                sig.SetCertificateStatus(CertificateStatus.Error);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { success = false, error = ex.Message });
            }
        })
        .WithName("ProvisionUserCertViaTenantCA")
        .RequireRateLimiting("signing-provision");
    }

    /// <summary>Delegate to SigningAdminEndpoints' CLI runner.</summary>
    private static Task<(string? Output, string? Error)> RunSignServerCliAsync(
        DigitalSigningOptions opts, string cliArgs) =>
        SigningAdminEndpoints.RunSignServerCliAsync(opts, cliArgs);
}
