using IVF.API.Services;
using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

/// <summary>
/// Admin endpoints for managing per-tenant Sub-CAs and tenant certificate lifecycle.
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

        // ─── Provision Sub-CA for a tenant ──────────────────────
        group.MapPost("/{tenantId:guid}/provision", async (
            Guid tenantId,
            ProvisionTenantCaRequest request,
            TenantCertificateService tenantCaService,
            CancellationToken ct) =>
        {
            try
            {
                var result = await tenantCaService.ProvisionTenantSubCaAsync(
                    tenantId, request.RootCaId, ct);

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

        // ─── Revoke tenant Sub-CA (nuclear option) ──────────────
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

        // ─── Provision user cert via tenant Sub-CA ──────────────
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

            try
            {
                sig.SetCertificateStatus(CertificateStatus.Pending);
                await db.SaveChangesAsync(ct);

                var result = await tenantCaService.ProvisionUserCertAsync(user, tenantId, opts, ct);

                sig.SetCertificateInfo(
                    subject: result.CertSubject,
                    serialNumber: result.SerialNumber,
                    expiry: result.Expiry,
                    workerName: result.WorkerName,
                    keystorePath: null);

                await db.SaveChangesAsync(ct);

                return Results.Ok(new
                {
                    success = true,
                    certificateSubject = result.CertSubject,
                    workerName = result.WorkerName,
                    serialNumber = result.SerialNumber,
                    expiry = result.Expiry,
                    managedCertId = result.ManagedCertId,
                    message = $"Đã cấp chứng thư Sub-CA cho {user.FullName}"
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
}
