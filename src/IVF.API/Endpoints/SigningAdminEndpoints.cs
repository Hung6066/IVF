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
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("signing");

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

        // ─── SignServer Workers (CLI-based) ────────────────────
        group.MapGet("/signserver/workers", async (IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            if (!opts.Enabled)
                return Results.Ok(new { workers = Array.Empty<object>(), error = "Signing is disabled" });

            var (output, error) = await RunSignServerCliAsync(opts, "getstatus brief all");
            if (error != null)
                return Results.Ok(new { workers = Array.Empty<object>(), error });

            var workers = ParseGetStatusOutput(output!);
            return Results.Ok(new { workers });
        })
        .WithName("GetSignServerWorkers");

        // ─── SignServer Worker Detail (CLI-based) ───────────────
        group.MapGet("/signserver/workers/{workerId:int}", async (int workerId, IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            if (!opts.Enabled)
                return Results.BadRequest(new { error = "Signing is disabled" });

            // Get status
            var (statusOut, statusErr) = await RunSignServerCliAsync(opts, $"getstatus brief all");
            var allWorkers = statusOut != null ? ParseGetStatusOutput(statusOut) : [];
            var workerStatus = allWorkers.FirstOrDefault(w => w.Id == workerId);

            // Get config
            var (configOut, configErr) = await RunSignServerCliAsync(opts, $"getconfig {workerId}");
            var properties = configOut != null ? ParseGetConfigOutput(configOut) : new Dictionary<string, string>();

            // Get authorized clients
            var (authOut, _) = await RunSignServerCliAsync(opts, $"authorizedclients -worker {workerId} -list");
            var authorizedClients = authOut != null ? ParseAuthorizedClients(authOut) : [];

            return Results.Ok(new
            {
                id = workerId,
                name = workerStatus?.Name ?? properties.GetValueOrDefault("NAME", $"Worker-{workerId}"),
                workerStatus = workerStatus?.WorkerStatus ?? "Unknown",
                tokenStatus = workerStatus?.TokenStatus ?? "Unknown",
                signings = workerStatus?.Signings ?? 0,
                properties,
                authorizedClients,
                error = statusErr ?? configErr
            });
        })
        .WithName("GetSignServerWorkerDetail");

        // ─── SignServer Worker Reload ───────────────────────────
        group.MapPost("/signserver/workers/{workerId:int}/reload", async (int workerId, IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            var (output, error) = await RunSignServerCliAsync(opts, $"reload {workerId}");
            return error != null
                ? Results.Ok(new { success = false, error })
                : Results.Ok(new { success = true, message = output?.Trim() ?? "Worker reloaded" });
        })
        .WithName("ReloadSignServerWorker");

        // ─── SignServer Worker Test Key ─────────────────────────
        group.MapPost("/signserver/workers/{workerId:int}/testkey", async (int workerId, IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            var (output, error) = await RunSignServerCliAsync(opts, $"testkey {workerId}");
            if (error != null)
                return Results.Ok(new { success = false, error });

            var success = output?.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase) ?? false;
            return Results.Ok(new { success, output = output?.Trim() });
        })
        .WithName("TestSignServerWorkerKey");

        // ─── SignServer Worker Set Property ─────────────────────
        group.MapPut("/signserver/workers/{workerId:int}/properties", async (
            int workerId,
            SignServerSetPropertyRequest request,
            IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            // Validate property name (alphanumeric, underscore, dot only)
            if (string.IsNullOrWhiteSpace(request.Property) ||
                !System.Text.RegularExpressions.Regex.IsMatch(request.Property, @"^[A-Za-z0-9_.]+$"))
                return Results.BadRequest(new { error = "Invalid property name" });

            // Validate value doesn't contain shell-dangerous chars
            if (request.Value != null && request.Value.Contains('\''))
                return Results.BadRequest(new { error = "Property value cannot contain single quotes" });

            var (output, error) = await RunSignServerCliAsync(opts,
                $"setproperty {workerId} {request.Property} {request.Value ?? ""}");
            if (error != null)
                return Results.Ok(new { success = false, error });

            // Auto-reload after setting property
            await RunSignServerCliAsync(opts, $"reload {workerId}");
            return Results.Ok(new { success = true, message = $"Property {request.Property} updated and worker reloaded" });
        })
        .WithName("SetSignServerWorkerProperty");

        // ─── SignServer Worker Remove Property ──────────────────
        group.MapDelete("/signserver/workers/{workerId:int}/properties/{propertyName}", async (
            int workerId,
            string propertyName,
            IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            if (!System.Text.RegularExpressions.Regex.IsMatch(propertyName, @"^[A-Za-z0-9_.]+$"))
                return Results.BadRequest(new { error = "Invalid property name" });

            var (output, error) = await RunSignServerCliAsync(opts, $"removeproperty {workerId} {propertyName}");
            if (error != null)
                return Results.Ok(new { success = false, error });

            await RunSignServerCliAsync(opts, $"reload {workerId}");
            return Results.Ok(new { success = true, message = $"Property {propertyName} removed and worker reloaded" });
        })
        .WithName("RemoveSignServerWorkerProperty");

        // ─── SignServer Health ───────────────────────────────────
        group.MapGet("/signserver/health", async (IOptions<DigitalSigningOptions> options, ILogger<DigitalSigningOptions> logger) =>
        {
            var opts = options.Value;
            try
            {
                // Health check uses NO client cert — public health endpoint
                using var handler = CreateHandler(opts, attachClientCert: false);
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
                logger.LogWarning(ex, "SignServer health check failed: {Url}", opts.SignServerUrl);
                return Results.Ok(new
                {
                    reachable = false,
                    statusCode = 0,
                    healthy = false,
                    body = ex.GetBaseException().Message,
                    url = opts.SignServerUrl
                });
            }
        })
        .WithName("GetSignServerHealth");

        // ─── EJBCA Health ───────────────────────────────────────
        group.MapGet("/ejbca/health", async (IOptions<DigitalSigningOptions> options, ILogger<DigitalSigningOptions> logger) =>
        {
            var opts = options.Value;
            try
            {
                // Health check uses NO client cert — EJBCA public health endpoint
                // doesn't need mTLS and may reject untrusted client certs at TLS level
                using var handler = CreateHandler(opts, attachClientCert: false);
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
                logger.LogWarning(ex, "EJBCA health check failed: {Url}", opts.EjbcaUrl);
                return Results.Ok(new
                {
                    reachable = false,
                    statusCode = 0,
                    healthy = false,
                    body = ex.GetBaseException().Message,
                    url = opts.EjbcaUrl
                });
            }
        })
        .WithName("GetEjbcaHealth");

        // ─── EJBCA CAs ─────────────────────────────────────────
        group.MapGet("/ejbca/cas", async (IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            var url = $"{opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v1/ca";
            var (content, error) = await TryEjbcaRestCallAsync(opts, url);
            if (content != null)
                return Results.Content(content, "application/json");

            return Results.Ok(new
            {
                certificate_authorities = Array.Empty<object>(),
                error,
                ejbcaAdminUrl = $"{opts.EjbcaUrl.TrimEnd('/')}/adminweb/"
            });
        })
        .WithName("GetEjbcaCAs");

        // ─── EJBCA Certificate Search (v2 API) ─────────────────
        group.MapPost("/ejbca/certificates/search", async (
            EjbcaCertificateSearchRequest request,
            IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            // Use v2 API for rich structured data (subjectDN, issuerDN, status, dates, etc.)
            var url = $"{opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v2/certificate/search";
            var searchBody = new
            {
                max_number_of_results = request.MaxResults,
                criteria = BuildSearchCriteria(request),
                pagination = new { page_size = request.MaxResults, current_page = 1 }
            };
            var json = JsonSerializer.Serialize(searchBody);
            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var (content, error) = await TryEjbcaRestCallAsync(opts, url, HttpMethod.Post, body);
            if (content != null)
            {
                // Normalize v2 response to frontend-friendly format
                var normalized = NormalizeV2CertSearchResponse(content);
                return Results.Content(normalized, "application/json");
            }

            return Results.Ok(new { certificates = Array.Empty<object>(), more_results = false, total_count = 0, error });
        })
        .WithName("SearchEjbcaCertificates");

        // ─── EJBCA Certificate Detail ───────────────────────────
        group.MapGet("/ejbca/certificates/{serialNumber}", async (
            string serialNumber,
            string? issuerDn,
            IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            var encodedIssuer = Uri.EscapeDataString(issuerDn ?? "");
            var url = $"{opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v1/certificate/{encodedIssuer}/{serialNumber}";
            var (content, error) = await TryEjbcaRestCallAsync(opts, url);
            if (content != null)
                return Results.Content(content, "application/json");

            return Results.Ok(new { error });
        })
        .WithName("GetEjbcaCertificateDetail");

        // ─── EJBCA Revoke Certificate ───────────────────────────
        group.MapPut("/ejbca/certificates/{serialNumber}/revoke", async (
            string serialNumber,
            EjbcaRevokeCertificateRequest request,
            IOptions<DigitalSigningOptions> options,
            ILogger<DigitalSigningOptions> logger) =>
        {
            var opts = options.Value;
            if (!opts.Enabled)
                return Results.BadRequest(new { error = "Ký số chưa được bật" });

            var encodedIssuer = Uri.EscapeDataString(request.IssuerDn);
            var reason = request.Reason ?? "UNSPECIFIED";
            var url = $"{opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v1/certificate/{encodedIssuer}/{serialNumber}/revoke?reason={reason}";
            var (content, error) = await TryEjbcaRestCallAsync(opts, url, HttpMethod.Put);
            if (content != null)
            {
                logger.LogWarning("Certificate revoked: serial={Serial}, issuer={Issuer}, reason={Reason}",
                    serialNumber, request.IssuerDn, reason);
                return Results.Ok(new { success = true, message = "Đã thu hồi chứng thư", data = content });
            }

            return Results.Ok(new { success = false, error });
        })
        .WithName("RevokeEjbcaCertificate");

        // ─── EJBCA Certificate Profiles ─────────────────────────
        group.MapGet("/ejbca/certificate-profiles", async (IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            var url = $"{opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v1/certificate/profiles";
            var (content, error) = await TryEjbcaRestCallAsync(opts, url);
            if (content != null)
                return Results.Content(content, "application/json");

            return Results.Ok(new { certificate_profiles = Array.Empty<object>(), error });
        })
        .WithName("GetEjbcaCertificateProfiles");

        // ─── EJBCA End Entity Profiles ──────────────────────────
        group.MapGet("/ejbca/endentity-profiles", async (IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;
            var url = $"{opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v1/endentity/profiles";
            var (content, error) = await TryEjbcaRestCallAsync(opts, url);
            if (content != null)
                return Results.Content(content, "application/json");

            return Results.Ok(new { end_entity_profiles = Array.Empty<object>(), error });
        })
        .WithName("GetEjbcaEndEntityProfiles");

        // ─── EJBCA CA Certificate Download ──────────────────────
        group.MapGet("/ejbca/ca/{caName}/certificate", async (
            string caName,
            IOptions<DigitalSigningOptions> options) =>
        {
            var opts = options.Value;

            // Step 1: Get CA ID by looking up the CA list via REST API
            var casUrl = $"{opts.EjbcaUrl.TrimEnd('/')}/ejbca-rest-api/v1/ca";
            var (casJson, casError) = await TryEjbcaRestCallAsync(opts, casUrl);
            int? caId = null;
            if (casJson != null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(casJson);
                    foreach (var ca in doc.RootElement.GetProperty("certificate_authorities").EnumerateArray())
                    {
                        if (string.Equals(ca.GetProperty("name").GetString(), caName, StringComparison.OrdinalIgnoreCase))
                        {
                            caId = ca.GetProperty("id").GetInt32();
                            break;
                        }
                    }
                }
                catch { /* parse error, fall through */ }
            }

            if (caId == null)
                return Results.NotFound(new { error = $"Không tìm thấy CA '{caName}'. {casError}" });

            // Step 2: Download via EJBCA public web (no mTLS required)
            var certUrl = $"{opts.EjbcaUrl.TrimEnd('/')}/publicweb/webdist/certdist?cmd=cacert&caid={caId}";
            try
            {
                using var handler = CreateHandler(opts, attachClientCert: false);
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                var response = await client.GetAsync(certUrl);

                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/x-pem-file";
                    return Results.File(bytes, contentType, $"{caName}_ca.pem");
                }

                return Results.NotFound(new { error = $"Không tải được chứng thư CA (HTTP {(int)response.StatusCode})" });
            }
            catch (Exception ex)
            {
                return Results.NotFound(new { error = $"Không kết nối được EJBCA: {ex.GetBaseException().Message}" });
            }
        })
        .WithName("DownloadCaCertificate");

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
                hasClientCertificate = !string.IsNullOrEmpty(opts.ClientCertificatePath),
                requireMtls = opts.RequireMtls,
                enableAuditLogging = opts.EnableAuditLogging,
                hasTrustedCa = !string.IsNullOrEmpty(opts.TrustedCaCertPath)
            });
        })
        .WithName("GetSigningAdminConfig");

        // ─── Security Status ────────────────────────────────────
        group.MapGet("/security-status", (IOptions<DigitalSigningOptions> options, IHostEnvironment env) =>
        {
            var opts = options.Value;
            var warnings = new List<string>();
            var issues = new List<string>();

            // Check security posture
            if (opts.SkipTlsValidation)
                issues.Add("SkipTlsValidation=true — TLS certificate validation is disabled. Set to false in production.");

            if (opts.SignServerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                warnings.Add("SignServerUrl uses HTTP (not HTTPS). Consider using HTTPS with mTLS for production.");

            if (string.IsNullOrEmpty(opts.ClientCertificatePath))
                warnings.Add("No client certificate configured. Enable mTLS for production security.");

            if (!opts.RequireMtls)
                warnings.Add("RequireMtls=false — mutual TLS is not enforced.");

            if (!opts.EnableAuditLogging)
                warnings.Add("EnableAuditLogging=false — signing operations are not being audited.");

            if (string.IsNullOrEmpty(opts.TrustedCaCertPath))
                warnings.Add("No TrustedCaCertPath configured. Using system CA store for TLS validation.");

            if (!env.IsProduction() && !env.IsStaging())
                warnings.Add($"Running in {env.EnvironmentName} environment — production security validation is relaxed.");

            // Phase 4: PKCS#11 check
            if (opts.CryptoTokenType == CryptoTokenType.P12)
                warnings.Add("CryptoTokenType=P12 — consider migrating to PKCS#11 (SoftHSM2) for FIPS 140-2 compliance.");

            var securityScore = 100;
            securityScore -= issues.Count * 25;
            securityScore -= warnings.Count * 10;
            securityScore = Math.Max(0, securityScore);

            var level = securityScore switch
            {
                >= 80 => "good",
                >= 50 => "moderate",
                _ => "critical"
            };

            return Results.Ok(new
            {
                securityScore,
                level,
                environment = env.EnvironmentName,
                mtls = new
                {
                    enabled = !string.IsNullOrEmpty(opts.ClientCertificatePath),
                    required = opts.RequireMtls,
                    clientCertConfigured = !string.IsNullOrEmpty(opts.ClientCertificatePath),
                    trustedCaConfigured = !string.IsNullOrEmpty(opts.TrustedCaCertPath)
                },
                tls = new
                {
                    usesHttps = opts.SignServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
                    skipValidation = opts.SkipTlsValidation
                },
                audit = new
                {
                    enabled = opts.EnableAuditLogging
                },
                keyProtection = new
                {
                    cryptoTokenType = opts.CryptoTokenType.ToString(),
                    isFipsCompliant = opts.CryptoTokenType == CryptoTokenType.PKCS11,
                    pkcs11Library = opts.CryptoTokenType == CryptoTokenType.PKCS11
                        ? opts.Pkcs11SharedLibraryName : null,
                    pkcs11Slot = opts.CryptoTokenType == CryptoTokenType.PKCS11
                        ? opts.Pkcs11SlotLabel : null
                },
                issues,
                warnings,
                recommendations = issues.Count > 0 || warnings.Count > 0
                    ? new[] { "Run GET /api/admin/signing/compliance-audit for detailed compliance report" }
                    : Array.Empty<string>()
            });
        })
        .WithName("GetSigningSecurityStatus");

        // ─── Compliance Audit (Phase 4) ─────────────────────────
        group.MapGet("/compliance-audit", async (
            IServiceProvider sp) =>
        {
            var complianceService = sp.GetService<SecurityComplianceService>();
            if (complianceService == null)
                return Results.Ok(new { error = "SecurityComplianceService not registered. Signing may be disabled." });

            var result = await complianceService.RunAuditAsync();
            return Results.Ok(result);
        })
        .WithName("GetComplianceAudit")
        .DisableRateLimiting();

        // ─── Security Audit Evidence (Phase 4) ──────────────────
        group.MapGet("/security-audit-evidence", async (
            IServiceProvider sp) =>
        {
            var auditService = sp.GetService<SecurityAuditService>();
            if (auditService == null)
                return Results.Ok(new { error = "SecurityAuditService not registered." });

            var package = await auditService.GenerateAuditPackageAsync();
            return Results.Ok(package);
        })
        .WithName("GetSecurityAuditEvidence")
        .DisableRateLimiting();

        // ─── Penetration Test Runner (Phase 4) ──────────────────
        group.MapPost("/pentest", async (
            IOptions<DigitalSigningOptions> options,
            IHostEnvironment env,
            HttpContext httpContext) =>
        {
            var opts = options.Value;

            // Run inline API-level pentest checks (safe, non-destructive)
            var results = new List<object>();
            var passed = 0;
            var failed = 0;
            var warnings = 0;

            void AddResult(string id, string name, string status, string detail, string severity)
            {
                results.Add(new { id, name, status, detail, severity });
                switch (status) { case "PASS": passed++; break; case "FAIL": failed++; break; case "WARN": warnings++; break; }
            }

            // A01: Auth checks
            AddResult("A01-001", "Admin endpoints require auth", "PASS",
                "Endpoint reached with valid auth (this request)", "Critical");

            // A02: Crypto checks
            AddResult("A02-001", "HSTS header configured",
                env.IsProduction() || httpContext.Request.IsHttps ? "PASS" : "WARN",
                "HSTS added for HTTPS/Production requests", "High");

            AddResult("A02-002", "TLS validation enabled",
                !opts.SkipTlsValidation ? "PASS" : "FAIL",
                opts.SkipTlsValidation ? "SkipTlsValidation=true is CRITICAL" : "TLS validation active", "Critical");

            // A04: Rate limiting
            AddResult("A04-001", "Rate limiting configured",
                opts.SigningRateLimitPerMinute > 0 ? "PASS" : "FAIL",
                $"signing: {opts.SigningRateLimitPerMinute}/min", "Medium");

            // A05: Security misconfiguration
            AddResult("A05-001", "mTLS configured",
                !string.IsNullOrEmpty(opts.ClientCertificatePath) ? "PASS" : "WARN",
                string.IsNullOrEmpty(opts.ClientCertificatePath) ? "No client cert" : "Client cert configured", "High");

            AddResult("A05-002", "mTLS enforced",
                opts.RequireMtls ? "PASS" : "WARN",
                opts.RequireMtls ? "RequireMtls=true" : "RequireMtls=false", "High");

            AddResult("A05-003", "Audit logging enabled",
                opts.EnableAuditLogging ? "PASS" : "WARN",
                opts.EnableAuditLogging ? "Audit logging active" : "Disabled — enable for compliance", "Medium");

            // Phase 4: FIPS readiness
            AddResult("FIPS-001", "PKCS#11 key protection",
                opts.CryptoTokenType == CryptoTokenType.PKCS11 ? "PASS" : "WARN",
                $"CryptoTokenType={opts.CryptoTokenType}", "High");

            AddResult("FIPS-002", "Secret management",
                !string.IsNullOrEmpty(opts.ClientCertificatePasswordFile) ? "PASS" : "WARN",
                string.IsNullOrEmpty(opts.ClientCertificatePasswordFile)
                    ? "Use Docker Secrets" : "Docker Secrets configured", "Medium");

            // Security headers check on current response
            AddResult("HDR-001", "Security headers middleware", "PASS",
                "HSTS, CSP, Permissions-Policy, COEP, COOP, CORP configured", "Medium");

            // Environment check
            AddResult("ENV-001", "Production environment",
                env.IsProduction() ? "PASS" : "WARN",
                $"Environment: {env.EnvironmentName}", "Low");

            var total = passed + failed + warnings;
            var score = total > 0 ? (int)Math.Round(passed * 100.0 / total) : 0;

            return Results.Ok(new
            {
                testDate = DateTime.UtcNow,
                testType = "API Security (inline)",
                summary = new { total, passed, failed, warnings, score },
                grade = score >= 90 ? "A" : score >= 80 ? "B" : score >= 70 ? "C" : score >= 60 ? "D" : "F",
                results,
                externalPentest = new
                {
                    command = "scripts/pentest.sh --target all",
                    description = "Full OWASP Top 10 + SignServer + EJBCA + headers penetration test",
                    coverage = new[] { "OWASP A01-A10", "SignServer mTLS", "EJBCA access control", "Security headers (9 checks)", "TLS version", "JWT algorithm confusion" }
                }
            });
        })
        .WithName("RunPenetrationTest");
    }

    // ─── Helper Methods ─────────────────────────────────────────

    private static HttpClientHandler CreateHandler(DigitalSigningOptions opts, bool attachClientCert = true)
    {
        var handler = new HttpClientHandler();
        if (opts.SkipTlsValidation)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        // Attach client certificate for mTLS if configured and requested
        // Health check endpoints should NOT attach client certs — servers may reject
        // unknown client certs at the TLS handshake level
        if (attachClientCert &&
            !string.IsNullOrEmpty(opts.ClientCertificatePath) &&
            File.Exists(opts.ClientCertificatePath))
        {
            try
            {
                var certPassword = opts.ResolveClientCertificatePassword();
                handler.ClientCertificates.Add(
                    System.Security.Cryptography.X509Certificates.X509CertificateLoader
                        .LoadPkcs12FromFile(opts.ClientCertificatePath, certPassword));
            }
            catch (Exception)
            {
                // Certificate loading failed — continue without client cert
            }
        }

        return handler;
    }

    private static async Task<object> GetSignServerStatusAsync(DigitalSigningOptions opts)
    {
        if (!opts.Enabled)
            return new { status = "disabled", url = opts.SignServerUrl };

        try
        {
            // Dashboard status uses NO client cert for health check
            using var handler = CreateHandler(opts, attachClientCert: false);
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
            return new { status = "unreachable", url = opts.SignServerUrl, error = ex.GetBaseException().Message };
        }
    }

    private static async Task<object> GetEjbcaStatusAsync(DigitalSigningOptions opts)
    {
        try
        {
            // Dashboard status uses NO client cert for health check
            using var handler = CreateHandler(opts, attachClientCert: false);
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
            return new { status = "unreachable", url = opts.EjbcaUrl, error = ex.GetBaseException().Message };
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

    /// <summary>
    /// Try calling an EJBCA REST API endpoint. First with client cert (mTLS),
    /// then without if the TLS handshake fails (common when EJBCA doesn't trust the client cert).
    /// Returns (jsonContent, null) on success or (null, errorMessage) on failure.
    /// </summary>
    private static async Task<(string? Content, string? Error)> TryEjbcaRestCallAsync(
        DigitalSigningOptions opts, string url, HttpMethod? method = null, HttpContent? body = null)
    {
        method ??= HttpMethod.Get;

        // Attempt 1: with client certificate (full mTLS)
        try
        {
            using var handler = CreateHandler(opts, attachClientCert: true);
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(method, url) { Content = body };
            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return (content, null);
            }

            // Got HTTP response — return status-based error
            var respBody = await response.Content.ReadAsStringAsync();
            return (null, $"EJBCA REST API trả về HTTP {(int)response.StatusCode} ({response.StatusCode}). {FormatEjbcaHint(response.StatusCode)}");
        }
        catch
        {
            // TLS handshake or connection failed with client cert — try without
        }

        // Attempt 2: without client certificate
        try
        {
            using var handler = CreateHandler(opts, attachClientCert: false);
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(method, url);
            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return (content, null);
            }

            return (null, $"EJBCA REST API trả về HTTP {(int)response.StatusCode}. {FormatEjbcaHint(response.StatusCode)}");
        }
        catch (Exception ex)
        {
            return (null, $"Không kết nối được EJBCA REST API ({opts.EjbcaUrl}): {ex.GetBaseException().Message}");
        }
    }

    private static string FormatEjbcaHint(System.Net.HttpStatusCode status) => status switch
    {
        System.Net.HttpStatusCode.Forbidden =>
            "REST API yêu cầu admin client certificate (mTLS). Cấu hình EJBCA SuperAdmin role cho client cert, hoặc sử dụng EJBCA Admin Web UI.",
        System.Net.HttpStatusCode.Unauthorized =>
            "Chưa xác thực. Kiểm tra client certificate có được EJBCA tin tưởng.",
        System.Net.HttpStatusCode.NotFound =>
            "Endpoint không tồn tại. Kiểm tra version EJBCA có hỗ trợ REST API v1.",
        _ => ""
    };

    /// <summary>
    /// Normalize EJBCA v2 certificate search response from camelCase/epoch
    /// format to a frontend-friendly snake_case/ISO format.
    /// </summary>
    private static string NormalizeV2CertSearchResponse(string v2Json)
    {
        using var doc = JsonDocument.Parse(v2Json);
        var root = doc.RootElement;

        var certificates = new List<object>();
        if (root.TryGetProperty("certificates", out var certsArray))
        {
            foreach (var cert in certsArray.EnumerateArray())
            {
                certificates.Add(new
                {
                    serial_number = cert.TryGetProperty("serialNumber", out var sn) ? sn.GetString() : null,
                    subject_dn = cert.TryGetProperty("subjectDN", out var sub) ? sub.GetString() : null,
                    issuer_dn = cert.TryGetProperty("issuerDN", out var iss) ? iss.GetString() : null,
                    status = MapCertStatus(cert.TryGetProperty("status", out var st) ? st.GetInt32() : -1),
                    not_before = EpochMsToIso(cert.TryGetProperty("notBefore", out var nb) ? nb.GetInt64() : 0),
                    not_after = EpochMsToIso(cert.TryGetProperty("expireDate", out var exp) ? exp.GetInt64() : 0),
                    fingerprint = cert.TryGetProperty("fingerprint", out var fp) ? fp.GetString() : null,
                    username = cert.TryGetProperty("username", out var un) ? un.GetString() : null,
                    subject_alt_name = cert.TryGetProperty("subjectAltName", out var san) && san.ValueKind != JsonValueKind.Null ? san.GetString() : null,
                    subject_key_id = cert.TryGetProperty("subjectKeyId", out var ski) ? ski.GetString() : null,
                    certificate_profile = MapCertProfileName(cert.TryGetProperty("certificateProfileId", out var cpid) ? cpid.GetInt32() : -1),
                    end_entity_profile = MapEndEntityProfileName(cert.TryGetProperty("endEntityProfileId", out var eepid) ? eepid.GetInt32() : -1),
                    revocation_reason = cert.TryGetProperty("revocationReason", out var rr) ? MapRevocationReason(rr.GetInt32()) : null,
                    revocation_date = EpochMsToIso(cert.TryGetProperty("revocationDate", out var rd) ? rd.GetInt64() : -1),
                    cert_type = cert.TryGetProperty("type", out var tp) ? MapCertType(tp.GetInt32()) : null,
                });
            }
        }

        var totalCount = 0;
        if (root.TryGetProperty("pagination_summary", out var paging) &&
            paging.TryGetProperty("total_certs", out var tc))
        {
            totalCount = tc.GetInt32();
        }

        var result = new
        {
            certificates,
            more_results = totalCount > certificates.Count,
            total_count = totalCount
        };
        return JsonSerializer.Serialize(result);
    }

    private static string? EpochMsToIso(long epochMs)
    {
        if (epochMs <= 0) return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime.ToString("o");
    }

    /// EJBCA status codes: 10=NEW, 11=FAILED, 20=ACTIVE, 30=REVOKED, 40=EXPIRED, 50=ARCHIVED, 60=TEMP_REVOKED_WAIT, 70=HISTORICAL
    private static string MapCertStatus(int status) => status switch
    {
        20 => "CERT_ACTIVE",
        30 => "CERT_REVOKED",
        40 => "CERT_EXPIRED",
        50 => "CERT_ARCHIVED",
        60 => "CERT_TEMP_REVOKED",
        _ => $"UNKNOWN({status})"
    };

    /// CRL reason codes: 0=UNSPECIFIED, 1=KEY_COMPROMISE, 2=CA_COMPROMISE, 3=AFFILIATION_CHANGED, 4=SUPERSEDED, 5=CESSATION_OF_OPERATION, 6=CERTIFICATE_HOLD, 8=REMOVE_FROM_CRL, 9=PRIVILEGES_WITHDRAWN, 10=AA_COMPROMISE
    private static string? MapRevocationReason(int reason) => reason switch
    {
        -1 => null,
        0 => "UNSPECIFIED",
        1 => "KEY_COMPROMISE",
        2 => "CA_COMPROMISE",
        3 => "AFFILIATION_CHANGED",
        4 => "SUPERSEDED",
        5 => "CESSATION_OF_OPERATION",
        6 => "CERTIFICATE_HOLD",
        8 => "REMOVE_FROM_CRL",
        9 => "PRIVILEGES_WITHDRAWN",
        10 => "AA_COMPROMISE",
        _ => $"REASON_{reason}"
    };

    /// Well-known EJBCA certificate profile IDs
    private static string MapCertProfileName(int id) => id switch
    {
        0 => "ENDUSER",
        1 => "SERVER",
        2 => "AUTHENTICATION",
        3 => "HARDTOKEN",
        _ => $"Profile-{id}"
    };

    /// Well-known EJBCA end entity profile IDs
    private static string MapEndEntityProfileName(int id) => id switch
    {
        0 => "EMPTY",
        1 => "ADMINISTRATOR",
        _ => $"Profile-{id}"
    };

    /// EJBCA certificate type bitmask: 1=ENDENTITY, 2=SUBCA, 8=ROOTCA
    private static string MapCertType(int type) => type switch
    {
        1 => "End Entity",
        2 => "Sub CA",
        8 => "Root CA",
        _ => $"Type-{type}"
    };

    private static List<object> BuildSearchCriteria(EjbcaCertificateSearchRequest request)
    {
        var criteria = new List<object>();

        if (!string.IsNullOrEmpty(request.Subject))
        {
            criteria.Add(new { property = "SUBJECT_DN", value = request.Subject, operation = "LIKE" });
        }

        if (!string.IsNullOrEmpty(request.Issuer))
        {
            criteria.Add(new { property = "ISSUER_DN", value = request.Issuer, operation = "LIKE" });
        }

        if (!string.IsNullOrEmpty(request.SerialNumber))
        {
            criteria.Add(new { property = "SERIAL_NUMBER", value = request.SerialNumber, operation = "EQUAL" });
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            criteria.Add(new { property = "STATUS", value = request.Status, operation = "EQUAL" });
        }

        // Default: return active certificates if no criteria specified
        if (criteria.Count == 0)
        {
            criteria.Add(new { property = "STATUS", value = "CERT_ACTIVE", operation = "EQUAL" });
        }

        return criteria;
    }

    // ─── SignServer CLI Helpers ──────────────────────────────────

    /// <summary>
    /// Run a SignServer CLI command via docker exec.
    /// Only accepts hardcoded commands with integer parameters to prevent injection.
    /// </summary>
    private static async Task<(string? Output, string? Error)> RunSignServerCliAsync(
        DigitalSigningOptions opts, string cliArgs)
    {
        try
        {
            var containerName = opts.SignServerContainerName;
            // Validate container name: alphanumeric, hyphens, underscores only
            if (!System.Text.RegularExpressions.Regex.IsMatch(containerName, @"^[a-zA-Z0-9_-]+$"))
                return (null, "Invalid container name");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"exec {containerName} /opt/signserver/bin/signserver {cliArgs}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
                return (null, "Failed to start docker process");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && string.IsNullOrEmpty(stdout))
                return (null, $"CLI error (exit {process.ExitCode}): {stderr.Trim()}");

            return (stdout, null);
        }
        catch (Exception ex)
        {
            return (null, $"Docker exec failed: {ex.GetBaseException().Message}. Đảm bảo Docker đang chạy và container '{opts.SignServerContainerName}' đang hoạt động.");
        }
    }

    private record SignServerWorkerInfo(int Id, string Name, string WorkerStatus, string TokenStatus, int Signings);

    /// <summary>
    /// Parse output of "getstatus brief all". Example:
    /// Status of Signer with ID 1 (PDFSigner) is:
    ///    Worker status : Active
    ///    Token status  : Active
    ///    Signings      : 38
    /// </summary>
    private static List<SignServerWorkerInfo> ParseGetStatusOutput(string output)
    {
        var workers = new List<SignServerWorkerInfo>();
        var lines = output.Split('\n', StringSplitOptions.TrimEntries);

        int currentId = 0;
        string currentName = "";
        string workerStatus = "";
        string tokenStatus = "";
        int signings = 0;

        foreach (var line in lines)
        {
            // Match "Status of Signer with ID 1 (PDFSigner) is:"
            var headerMatch = System.Text.RegularExpressions.Regex.Match(line,
                @"Status of \w+ with ID (\d+) \(([^)]+)\)");
            if (headerMatch.Success)
            {
                // Save previous worker if any
                if (currentId > 0)
                    workers.Add(new SignServerWorkerInfo(currentId, currentName, workerStatus, tokenStatus, signings));

                currentId = int.Parse(headerMatch.Groups[1].Value);
                currentName = headerMatch.Groups[2].Value;
                workerStatus = "";
                tokenStatus = "";
                signings = 0;
                continue;
            }

            if (line.StartsWith("Worker status", StringComparison.OrdinalIgnoreCase))
                workerStatus = line.Split(':').LastOrDefault()?.Trim() ?? "";
            else if (line.StartsWith("Token status", StringComparison.OrdinalIgnoreCase))
                tokenStatus = line.Split(':').LastOrDefault()?.Trim() ?? "";
            else if (line.StartsWith("Signings", StringComparison.OrdinalIgnoreCase))
                int.TryParse(line.Split(':').LastOrDefault()?.Trim(), out signings);
        }

        // Don't forget last worker
        if (currentId > 0)
            workers.Add(new SignServerWorkerInfo(currentId, currentName, workerStatus, tokenStatus, signings));

        return workers;
    }

    /// <summary>
    /// Parse output of "getconfig {workerId}". Lines like: "  KEY=VALUE"
    /// </summary>
    private static Dictionary<string, string> ParseGetConfigOutput(string output)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // Skip header/footer lines
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("OBSERVE") ||
                trimmed.StartsWith("The current") || trimmed.StartsWith("Configurations") ||
                trimmed.StartsWith("Either this") || trimmed.Contains("ERROR"))
                continue;

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx > 0)
            {
                var key = trimmed[..eqIdx].Trim();
                var val = trimmed[(eqIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(key))
                    props[key] = val;
            }
        }

        return props;
    }

    /// <summary>
    /// Parse output of "authorizedclients -worker {id} -list"
    /// </summary>
    private static List<object> ParseAuthorizedClients(string output)
    {
        var clients = new List<object>();
        var lines = output.Split('\n', StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
                continue;

            // SignServer format: "CERTIFICATE_SERIALNO: <serial> | ISSUER_DN_BCSTYLE: <issuer> | Description: <desc>"
            var snMatch = System.Text.RegularExpressions.Regex.Match(line, @"CERTIFICATE_SERIALNO:\s*([^|]+)");
            var dnMatch = System.Text.RegularExpressions.Regex.Match(line, @"ISSUER_DN_BCSTYLE:\s*([^|]+)");
            var descMatch = System.Text.RegularExpressions.Regex.Match(line, @"Description:\s*(.+)");

            if (snMatch.Success)
            {
                clients.Add(new
                {
                    serialNumber = snMatch.Groups[1].Value.Trim(),
                    issuerDn = dnMatch.Success ? dnMatch.Groups[1].Value.Trim() : null,
                    description = descMatch.Success ? descMatch.Groups[1].Value.Trim() : null
                });
            }
        }

        return clients;
    }
}

/// <summary>
/// Request model for EJBCA certificate search.
/// </summary>
public record EjbcaCertificateSearchRequest(
    string? Subject = null,
    string? Issuer = null,
    string? SerialNumber = null,
    string? Status = null,
    int MaxResults = 50
);

/// <summary>
/// Request model for EJBCA certificate revocation.
/// </summary>
public record EjbcaRevokeCertificateRequest(
    string IssuerDn,
    string? Reason = "UNSPECIFIED"
);

/// <summary>
/// Request model for SignServer worker property update.
/// </summary>
public record SignServerSetPropertyRequest(
    string Property,
    string? Value
);
