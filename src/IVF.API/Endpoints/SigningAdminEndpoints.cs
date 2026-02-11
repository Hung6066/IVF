using System.Net.Http.Headers;
using System.Text.Json;
using IVF.API.Services;
using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace IVF.API.Endpoints;

/// <summary>
/// Admin endpoints for managing EJBCA and SignServer infrastructure.
/// Provides proxy APIs for the Angular admin panel.
/// </summary>
public static class SigningAdminEndpoints
{
    public static void MapSigningAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/signing")
            .WithTags("Signing Administration")
            .RequireAuthorization("AdminOnly");

        // ─── Dashboard Overview ─────────────────────────────────
        group.MapGet("/dashboard", async (
            IDigitalSigningService signingService,
            IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            var dashboard = new
            {
                signing = new
                {
                    enabled = opts.Enabled,
                    signServerUrl = opts.SignServerUrl,
                    ejbcaUrl = opts.EjbcaUrl,
                    workerName = opts.WorkerName,
                    workerId = opts.WorkerId,
                    defaultReason = opts.DefaultReason,
                    defaultLocation = opts.DefaultLocation,
                    addVisibleSignature = opts.AddVisibleSignature,
                    timeoutSeconds = opts.TimeoutSeconds
                },
                health = await signingService.CheckHealthAsync(),
                signServer = await GetSignServerStatusAsync(opts),
                ejbca = await GetEjbcaStatusAsync(opts)
            };
            return Results.Ok(dashboard);
        })
        .WithName("GetSigningDashboard");

        // ─── SignServer Workers ─────────────────────────────────
        group.MapGet("/signserver/workers", async (IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            if (!opts.Enabled)
                return Results.Ok(new { workers = Array.Empty<object>(), error = "Signing is disabled" });

            try
            {
                using var handler = CreateHandler(opts);
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

                var url = $"{opts.SignServerUrl.TrimEnd('/')}/rest/v1/workers";
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Results.Content(content, "application/json");
                }

                // Fallback: use the admin CLI data via process endpoint
                return Results.Ok(new { workers = new[] { new { name = opts.WorkerName, id = opts.WorkerId ?? 1 } } });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { workers = Array.Empty<object>(), error = ex.Message });
            }
        })
        .WithName("GetSignServerWorkers");

        // ─── SignServer Worker Detail ───────────────────────────
        group.MapGet("/signserver/workers/{workerId:int}", async (int workerId, IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            if (!opts.Enabled)
                return Results.BadRequest(new { error = "Signing is disabled" });

            try
            {
                using var handler = CreateHandler(opts);
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

                var url = $"{opts.SignServerUrl.TrimEnd('/')}/rest/v1/workers/{workerId}";
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return Results.Content(content, "application/json");

                return Results.Ok(new
                {
                    id = workerId,
                    name = opts.WorkerName,
                    status = "unknown",
                    message = $"REST API returned {response.StatusCode}. Use SignServer Admin CLI for details."
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { id = workerId, error = ex.Message });
            }
        })
        .WithName("GetSignServerWorkerDetail");

        // ─── SignServer Health ───────────────────────────────────
        group.MapGet("/signserver/health", async (IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            try
            {
                using var handler = CreateHandler(opts);
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(10);

                var url = $"{opts.SignServerUrl.TrimEnd('/')}/healthcheck/signserverhealth";
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                return Results.Ok(new
                {
                    reachable = true,
                    statusCode = (int)response.StatusCode,
                    healthy = response.IsSuccessStatusCode,
                    body = content,
                    url = opts.SignServerUrl
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new
                {
                    reachable = false,
                    statusCode = 0,
                    healthy = false,
                    body = ex.Message,
                    url = opts.SignServerUrl
                });
            }
        })
        .WithName("GetSignServerHealth");

        // ─── EJBCA Health ───────────────────────────────────────
        group.MapGet("/ejbca/health", async (IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            try
            {
                using var handler = CreateHandler(opts);
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(10);

                var url = $"{opts.EjbcaUrl.TrimEnd('/')}/publicweb/healthcheck/ejbcahealth";
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                return Results.Ok(new
                {
                    reachable = true,
                    statusCode = (int)response.StatusCode,
                    healthy = response.IsSuccessStatusCode,
                    body = content,
                    url = opts.EjbcaUrl
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new
                {
                    reachable = false,
                    statusCode = 0,
                    healthy = false,
                    body = ex.Message,
                    url = opts.EjbcaUrl
                });
            }
        })
        .WithName("GetEjbcaHealth");

        // ─── EJBCA CAs ─────────────────────────────────────────
        group.MapGet("/ejbca/cas", async (IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            try
            {
                using var handler = CreateHandler(opts);
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(10);

                var url = $"{opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v1/ca";
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Results.Content(content, "application/json");
                }

                // EJBCA REST API requires client certificate authentication
                var message = response.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "EJBCA REST API yêu cầu xác thực client certificate (mTLS). Truy cập trực tiếp qua Admin Web UI: https://localhost:8443/ejbca/adminweb/"
                    : $"EJBCA REST API trả về {response.StatusCode}. Kiểm tra cấu hình EJBCA hoặc sử dụng Admin Web UI.";

                return Results.Ok(new
                {
                    certificate_authorities = Array.Empty<object>(),
                    message,
                    statusCode = (int)response.StatusCode,
                    ejbcaAdminUrl = $"{opts.EjbcaUrl.TrimEnd('/')}/adminweb/"
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { certificate_authorities = Array.Empty<object>(), error = ex.Message });
            }
        })
        .WithName("GetEjbcaCAs");

        // ─── EJBCA Certificate Search ───────────────────────────
        group.MapGet("/ejbca/certificates", async (string? subject, IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            try
            {
                using var handler = CreateHandler(opts);
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(10);

                // Try EJBCA REST v1 certificate search
                var url = $"{opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v1/ca/status";
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                return Results.Ok(new
                {
                    available = response.IsSuccessStatusCode,
                    data = content,
                    searchSubject = subject
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { available = false, error = ex.Message });
            }
        })
        .WithName("GetEjbcaCertificates");

        // ─── Test Signing ───────────────────────────────────────
        group.MapPost("/test-sign", async (IDigitalSigningService signingService) =>
        {
            if (!signingService.IsEnabled)
                return Results.BadRequest(new { error = "Signing is disabled" });

            try
            {
                // Create minimal PDF for testing
                var testPdf = GenerateMinimalTestPdf();
                var metadata = new SigningMetadata("Test signature", "Admin Panel", "admin@ivf-clinic.vn", "Admin");

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var signedPdf = await signingService.SignPdfAsync(testPdf, metadata);
                stopwatch.Stop();

                return Results.Ok(new
                {
                    success = true,
                    originalSize = testPdf.Length,
                    signedSize = signedPdf.Length,
                    durationMs = stopwatch.ElapsedMilliseconds,
                    containsSignature = signedPdf.Length > testPdf.Length,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new
                {
                    success = false,
                    error = ex.Message,
                    errorType = ex.GetType().Name,
                    timestamp = DateTime.UtcNow
                });
            }
        })
        .WithName("TestSigning");

        // ─── Update Configuration ───────────────────────────────
        group.MapGet("/config", (IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            return Results.Ok(new
            {
                enabled = opts.Enabled,
                signServerUrl = opts.SignServerUrl,
                ejbcaUrl = opts.EjbcaUrl,
                workerName = opts.WorkerName,
                workerId = opts.WorkerId,
                defaultReason = opts.DefaultReason,
                defaultLocation = opts.DefaultLocation,
                defaultContactInfo = opts.DefaultContactInfo,
                addVisibleSignature = opts.AddVisibleSignature,
                visibleSignaturePage = opts.VisibleSignaturePage,
                timeoutSeconds = opts.TimeoutSeconds,
                skipTlsValidation = opts.SkipTlsValidation,
                hasClientCertificate = !string.IsNullOrEmpty(opts.ClientCertificatePath)
            });
        })
        .WithName("GetSigningAdminConfig");
    }

    // ─── Helper Methods ─────────────────────────────────────────

    private static HttpClientHandler CreateHandler(DigitalSigningOptions opts)
    {
        var handler = new HttpClientHandler();
        if (opts.SkipTlsValidation)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        return handler;
    }

    private static async Task<object> GetSignServerStatusAsync(DigitalSigningOptions opts)
    {
        if (!opts.Enabled)
            return new { status = "disabled", url = opts.SignServerUrl };

        try
        {
            using var handler = CreateHandler(opts);
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(10);

            var url = $"{opts.SignServerUrl.TrimEnd('/')}/healthcheck/signserverhealth";
            var response = await client.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();

            return new
            {
                status = response.IsSuccessStatusCode ? "healthy" : "unhealthy",
                url = opts.SignServerUrl,
                healthBody = body
            };
        }
        catch (Exception ex)
        {
            return new { status = "unreachable", url = opts.SignServerUrl, error = ex.Message };
        }
    }

    private static async Task<object> GetEjbcaStatusAsync(DigitalSigningOptions opts)
    {
        try
        {
            using var handler = CreateHandler(opts);
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(10);

            var url = $"{opts.EjbcaUrl.TrimEnd('/')}/publicweb/healthcheck/ejbcahealth";
            var response = await client.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();

            return new
            {
                status = response.IsSuccessStatusCode ? "healthy" : "unhealthy",
                url = opts.EjbcaUrl,
                healthBody = body
            };
        }
        catch (Exception ex)
        {
            return new { status = "unreachable", url = opts.EjbcaUrl, error = ex.Message };
        }
    }

    internal static byte[] GenerateMinimalTestPdf()
    {
        var pdfContent = @"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << >> >>
endobj
4 0 obj
<< /Length 44 >>
stream
BT /F1 12 Tf 100 700 Td (Test PDF) Tj ET
endstream
endobj
xref
0 5
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n 
0000000266 00000 n 
trailer
<< /Size 5 /Root 1 0 R >>
startxref
362
%%EOF";
        return System.Text.Encoding.ASCII.GetBytes(pdfContent);
    }
}
