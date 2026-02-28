using IVF.Domain.Entities;

namespace IVF.API.Endpoints;

public static class CertificateAuthorityEndpoints
{
    public static void MapCertificateAuthorityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/certificates")
            .WithTags("Certificate Authority")
            .RequireAuthorization("AdminOnly");

        // ═══ Dashboard ═══════════════════════════════════════

        group.MapGet("/dashboard", async (Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(ct)))
            .WithName("GetCaDashboard");

        // ═══ CA Management ═══════════════════════════════════

        group.MapGet("/ca", async (Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListCAsAsync(ct)))
            .WithName("ListCAs");

        group.MapGet("/ca/{id:guid}", async (Guid id, Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var ca = await svc.GetCaAsync(id, ct);
            return ca != null
                ? Results.Ok(new
                {
                    ca.Id,
                    ca.Name,
                    ca.CommonName,
                    ca.Organization,
                    ca.OrganizationalUnit,
                    ca.Country,
                    ca.State,
                    ca.Locality,
                    ca.Type,
                    ca.Status,
                    ca.KeyAlgorithm,
                    ca.KeySize,
                    ca.Fingerprint,
                    ca.NotBefore,
                    ca.NotAfter,
                    ca.ParentCaId,
                    ca.NextSerialNumber,
                    CertificatePem = ca.CertificatePem,
                    ChainPem = ca.ChainPem,
                    IssuedCerts = ca.IssuedCertificates.Count
                })
                : Results.NotFound();
        })
        .WithName("GetCA");

        group.MapPost("/ca", async (Services.CreateCaRequest req,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var ca = await svc.CreateRootCaAsync(req, ct);
            return Results.Created($"/api/admin/certificates/ca/{ca.Id}", new { ca.Id, ca.Name, ca.Fingerprint });
        })
        .WithName("CreateRootCA");

        // ═══ Intermediate CA ═════════════════════════════════

        group.MapPost("/ca/intermediate", async (Services.CreateIntermediateCaRequest req,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var ca = await svc.CreateIntermediateCaAsync(req, ct);
            return Results.Created($"/api/admin/certificates/ca/{ca.Id}",
                new { ca.Id, ca.Name, ca.Fingerprint, ca.ParentCaId, ca.Type });
        })
        .WithName("CreateIntermediateCA");

        group.MapGet("/ca/{id:guid}/chain", async (Guid id,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var ca = await svc.GetCaAsync(id, ct);
            return ca != null
                ? Results.Text(ca.ChainPem ?? ca.CertificatePem, "application/x-pem-file")
                : Results.NotFound();
        })
        .WithName("DownloadCaChain");

        // ═══ Certificate Management ══════════════════════════

        group.MapGet("/certs", async (Guid? caId,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListCertificatesAsync(caId, ct)))
            .WithName("ListCertificates");

        group.MapPost("/certs", async (Services.IssueCertRequest req,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var cert = await svc.IssueCertificateAsync(req, ct);
            return Results.Created($"/api/admin/certificates/certs/{cert.Id}", new
            {
                cert.Id,
                cert.CommonName,
                cert.Purpose,
                cert.Fingerprint,
                cert.SerialNumber,
                cert.NotBefore,
                cert.NotAfter
            });
        })
        .WithName("IssueCertificate");

        group.MapGet("/certs/{id:guid}/bundle", async (Guid id,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var bundle = await svc.GetCertBundleAsync(id, ct);
            return bundle != null ? Results.Ok(bundle) : Results.NotFound();
        })
        .WithName("GetCertBundle");

        // ═══ Certificate Rotation / Renewal ══════════════════

        group.MapPost("/certs/{id:guid}/renew", async (Guid id,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var newCert = await svc.RenewCertificateAsync(id, ct);
            return Results.Ok(new
            {
                newCert.Id,
                newCert.CommonName,
                newCert.Purpose,
                newCert.Fingerprint,
                newCert.SerialNumber,
                newCert.NotBefore,
                newCert.NotAfter,
                ReplacedCertId = id
            });
        })
        .WithName("RenewCertificate");

        group.MapPut("/certs/{id:guid}/auto-renew", async (Guid id, AutoRenewRequest req,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            await svc.SetAutoRenewAsync(id, req.Enabled, req.RenewBeforeDays, ct);
            return Results.NoContent();
        })
        .WithName("SetAutoRenew");

        group.MapGet("/certs/expiring", async (Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetExpiringCertificatesAsync(ct)))
            .WithName("GetExpiringCertificates");

        group.MapPost("/certs/auto-renew-now", async (Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.AutoRenewExpiringAsync(ct)))
            .WithName("TriggerAutoRenewal");

        // ═══ Certificate Revocation (with reason) ════════════

        group.MapPost("/certs/{id:guid}/revoke", async (Guid id, Services.RevokeCertRequest? req,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            await svc.RevokeCertificateAsync(id, req?.Reason ?? RevocationReason.Unspecified, ct);
            return Results.NoContent();
        })
        .WithName("RevokeCertificate");

        // ═══ CRL (Certificate Revocation List) ═══════════════

        group.MapPost("/ca/{id:guid}/crl/generate", async (Guid id,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var crl = await svc.GenerateCrlAsync(id, ct);
            return Results.Ok(new { crl.Id, crl.CrlNumber, crl.ThisUpdate, crl.NextUpdate, crl.RevokedCount });
        })
        .WithName("GenerateCrl");

        group.MapGet("/ca/{id:guid}/crl", async (Guid id,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListCrlsAsync(id, ct)))
            .WithName("ListCrls");

        group.MapGet("/ca/{id:guid}/crl/latest", async (Guid id,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var crl = await svc.GetLatestCrlAsync(id, ct);
            return crl != null
                ? Results.Text(crl.CrlPem, "application/x-pem-file")
                : Results.NotFound();
        })
        .WithName("GetLatestCrl");

        // ═══ OCSP (Certificate Status) ═══════════════════════

        group.MapGet("/ocsp/{caId:guid}/{serialNumber}", async (Guid caId, string serialNumber,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.CheckCertStatusAsync(serialNumber, caId, ct)))
            .WithName("OcspQuery");

        // ═══ Audit Trail ═════════════════════════════════════

        group.MapGet("/audit", async (Guid? certId, Guid? caId,
            CertAuditEventType? eventType, int? limit,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAuditEventsAsync(certId, caId, eventType, limit ?? 100, ct)))
            .WithName("ListCertAuditEvents");

        // ═══ Certificate Deployment ══════════════════════════

        group.MapPost("/certs/{id:guid}/deploy", async (Guid id, DeployCertRequest req,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.DeployCertificateAsync(id, req.Container, req.CertPath, req.KeyPath, req.CaPath, ct)))
            .WithName("DeployCertificate");

        group.MapPost("/certs/{id:guid}/deploy-pg", async (Guid id,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.DeployPgSslAsync(id, ct)))
            .WithName("DeployPgSsl");

        group.MapPost("/certs/{id:guid}/deploy-minio", async (Guid id,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.DeployMinioSslAsync(id, ct)))
            .WithName("DeployMinioSsl");

        group.MapPost("/certs/{id:guid}/deploy-replica-pg", async (Guid id,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.DeployReplicaPgSslAsync(id, ct)))
            .WithName("DeployReplicaPgSsl");

        group.MapPost("/certs/{id:guid}/deploy-replica-minio", async (Guid id,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.DeployReplicaMinioSslAsync(id, ct)))
            .WithName("DeployReplicaMinioSsl");

        // Deploy logs
        group.MapGet("/deploy-logs", async (Guid? certId, int? limit,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListDeployLogsAsync(certId, limit ?? 50, ct)))
            .WithName("ListDeployLogs");

        group.MapGet("/deploy-logs/{operationId}", async (string operationId,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var log = await svc.GetDeployLogAsync(operationId, ct);
            return log is null ? Results.NotFound() : Results.Ok(log);
        }).WithName("GetDeployLog");

        // ═══ Public PKI Endpoints (no auth) ══════════════════
        // CRL distribution point and OCSP responder must be
        // accessible without authentication for RFC compliance.
        MapPublicPkiEndpoints(app);
    }

    /// <summary>
    /// Public PKI endpoints for CRL distribution and OCSP queries.
    /// These are unauthenticated per RFC 5280 / RFC 6960.
    /// </summary>
    private static void MapPublicPkiEndpoints(WebApplication app)
    {
        var pki = app.MapGroup("/api/pki")
            .WithTags("PKI Public")
            .AllowAnonymous();

        // CRL Distribution Point (HTTP GET) — embedded in issued certs
        pki.MapGet("/crl/{caId:guid}", async (Guid caId,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
        {
            var crl = await svc.GetLatestCrlAsync(caId, ct);
            if (crl == null) return Results.NotFound();
            return Results.Bytes(crl.CrlDer, "application/pkix-crl");
        })
        .WithName("PublicCrlDistributionPoint");

        // Public OCSP endpoint
        pki.MapGet("/ocsp/{caId:guid}/{serialNumber}", async (Guid caId, string serialNumber,
            Services.CertificateAuthorityService svc, CancellationToken ct) =>
            Results.Ok(await svc.CheckCertStatusAsync(serialNumber, caId, ct)))
            .WithName("PublicOcspQuery");
    }

    // ── Request DTOs ─────────────────────────────────────

    private record AutoRenewRequest(bool Enabled, int? RenewBeforeDays);
    private record DeployCertRequest(string Container, string CertPath, string KeyPath, string? CaPath);
}
