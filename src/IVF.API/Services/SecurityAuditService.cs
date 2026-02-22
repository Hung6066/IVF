using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace IVF.API.Services;

/// <summary>
/// Generates comprehensive security audit evidence packages for third-party auditors.
/// Phase 4: Supports external security audit and compliance verification.
///
/// Collects:
///   - System configuration snapshot (sanitized — no secrets)
///   - Security posture assessment
///   - Compliance audit results
///   - Architecture & network topology description
///   - Certificate inventory
///   - Security controls inventory
///   - Access control matrix
///   - Audit log summary
/// </summary>
public sealed class SecurityAuditService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<SecurityAuditService> _logger;

    public SecurityAuditService(IServiceProvider sp, ILogger<SecurityAuditService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    /// <summary>
    /// Generates a full audit evidence package for third-party review.
    /// All secrets are redacted — safe to share with external auditors.
    /// </summary>
    public async Task<AuditEvidencePackage> GenerateAuditPackageAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[SecurityAudit] Generating audit evidence package");

        var opts = _sp.GetRequiredService<IOptions<DigitalSigningOptions>>().Value;
        var env = _sp.GetRequiredService<IHostEnvironment>();

        // Run compliance audit
        var complianceService = _sp.GetService<SecurityComplianceService>();
        ComplianceAuditResult? complianceResult = null;
        if (complianceService != null)
            complianceResult = await complianceService.RunAuditAsync(ct);

        return new AuditEvidencePackage
        {
            GeneratedAt = DateTime.UtcNow,
            AuditVersion = "1.0",
            SystemInfo = CollectSystemInfo(env),
            SecurityConfiguration = CollectSecurityConfig(opts),
            ComplianceAudit = complianceResult,
            CertificateInventory = await CollectCertificateInventoryAsync(opts, ct),
            SecurityControls = CollectSecurityControls(opts),
            AccessControlMatrix = CollectAccessControlMatrix(),
            NetworkTopology = CollectNetworkTopology(),
            DataProtection = CollectDataProtection(opts),
            IncidentResponse = CollectIncidentResponsePlan(),
            PentestCapabilities = CollectPentestCapabilities(),
            AuditTrailConfig = CollectAuditTrailConfig(opts),
            Recommendations = GenerateAuditRecommendations(opts, complianceResult)
        };
    }

    private static SystemInfoSection CollectSystemInfo(IHostEnvironment env) => new()
    {
        ApplicationName = "IVF System — Digital Signing Infrastructure",
        Environment = env.EnvironmentName,
        RuntimeVersion = RuntimeInformation.FrameworkDescription,
        OsPlatform = RuntimeInformation.OSDescription,
        Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
        DotNetVersion = Environment.Version.ToString(),
        Timestamp = DateTime.UtcNow,
        ContainerRuntime = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
            ? "Docker" : "Native"
    };

    private static SecurityConfigSection CollectSecurityConfig(DigitalSigningOptions opts) => new()
    {
        SigningEnabled = opts.Enabled,
        SignServerUrl = opts.SignServerUrl,
        UsesHttps = opts.SignServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
        TlsValidationEnabled = !opts.SkipTlsValidation,
        MtlsEnabled = !string.IsNullOrEmpty(opts.ClientCertificatePath),
        MtlsEnforced = opts.RequireMtls,
        AuditLoggingEnabled = opts.EnableAuditLogging,
        CryptoTokenType = opts.CryptoTokenType.ToString(),
        IsFipsCompliant = opts.CryptoTokenType == CryptoTokenType.PKCS11,
        Pkcs11Library = opts.CryptoTokenType == CryptoTokenType.PKCS11
            ? opts.Pkcs11SharedLibraryName : null,
        ClientCertConfigured = !string.IsNullOrEmpty(opts.ClientCertificatePath),
        TrustedCaConfigured = !string.IsNullOrEmpty(opts.TrustedCaCertPath),
        SecretManagement = !string.IsNullOrEmpty(opts.ClientCertificatePasswordFile)
            ? "Docker Secrets (file-based)"
            : !string.IsNullOrEmpty(opts.ClientCertificatePassword)
                ? "Environment/Config (plaintext — NOT recommended)"
                : "Not configured",
        CertExpiryMonitoring = new CertExpiryConfig
        {
            WarningDays = opts.CertExpiryWarningDays,
            CheckIntervalMinutes = opts.CertExpiryCheckIntervalMinutes
        },
        RateLimiting = new RateLimitConfig
        {
            SigningPerMinute = opts.SigningRateLimitPerMinute,
            ProvisioningPerMinute = 3
        }
    };

    private static async Task<CertificateInventorySection> CollectCertificateInventoryAsync(
        DigitalSigningOptions opts, CancellationToken ct)
    {
        var certs = new List<CertificateInfo>();

        // Client certificate
        if (!string.IsNullOrEmpty(opts.ClientCertificatePath))
        {
            try
            {
                if (File.Exists(opts.ClientCertificatePath))
                {
                    using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                        .LoadPkcs12FromFile(opts.ClientCertificatePath, opts.ResolveClientCertificatePassword());
                    certs.Add(new CertificateInfo
                    {
                        Name = "API Client Certificate (mTLS)",
                        Subject = cert.Subject,
                        Issuer = cert.Issuer,
                        SerialNumber = cert.SerialNumber,
                        NotBefore = cert.NotBefore,
                        NotAfter = cert.NotAfter,
                        DaysRemaining = (int)(cert.NotAfter - DateTime.UtcNow).TotalDays,
                        KeyAlgorithm = cert.GetKeyAlgorithm(),
                        SignatureAlgorithm = cert.SignatureAlgorithm.FriendlyName ?? "unknown",
                        Thumbprint = cert.Thumbprint,
                        KeySize = cert.PublicKey.GetRSAPublicKey()?.KeySize
                            ?? cert.PublicKey.GetECDsaPublicKey()?.KeySize ?? 0,
                        StorageType = "PKCS#12 File",
                        FilePath = "[REDACTED]" // Don't expose path
                    });
                }
                else
                {
                    certs.Add(new CertificateInfo
                    {
                        Name = "API Client Certificate (mTLS)",
                        Subject = "FILE NOT FOUND",
                        StorageType = "PKCS#12 File",
                        FilePath = "[REDACTED]"
                    });
                }
            }
            catch (Exception ex)
            {
                certs.Add(new CertificateInfo
                {
                    Name = "API Client Certificate (mTLS)",
                    Subject = $"ERROR: {ex.Message}",
                    StorageType = "PKCS#12 File"
                });
            }
        }

        // Trusted CA certificate
        if (!string.IsNullOrEmpty(opts.TrustedCaCertPath))
        {
            try
            {
                if (File.Exists(opts.TrustedCaCertPath))
                {
                    using var caCert = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                        .LoadCertificateFromFile(opts.TrustedCaCertPath);
                    certs.Add(new CertificateInfo
                    {
                        Name = "Trusted CA Certificate",
                        Subject = caCert.Subject,
                        Issuer = caCert.Issuer,
                        SerialNumber = caCert.SerialNumber,
                        NotBefore = caCert.NotBefore,
                        NotAfter = caCert.NotAfter,
                        DaysRemaining = (int)(caCert.NotAfter - DateTime.UtcNow).TotalDays,
                        SignatureAlgorithm = caCert.SignatureAlgorithm.FriendlyName ?? "unknown",
                        Thumbprint = caCert.Thumbprint,
                        StorageType = "PEM File",
                        FilePath = "[REDACTED]"
                    });
                }
            }
            catch { /* Non-critical */ }
        }

        return new CertificateInventorySection
        {
            TotalCertificates = certs.Count,
            Certificates = certs,
            SignServerWorkers = new[]
            {
                new WorkerCertInfo { WorkerId = 1, WorkerName = "PlainSigner", TokenType = opts.CryptoTokenType.ToString() },
                new WorkerCertInfo { WorkerId = 272, WorkerName = "PDFSigner_Technical", TokenType = opts.CryptoTokenType.ToString() },
                new WorkerCertInfo { WorkerId = 444, WorkerName = "PDFSigner_HeadDepartment", TokenType = opts.CryptoTokenType.ToString() },
                new WorkerCertInfo { WorkerId = 597, WorkerName = "PDFSigner_Doctor1", TokenType = opts.CryptoTokenType.ToString() },
                new WorkerCertInfo { WorkerId = 907, WorkerName = "PDFSigner_Admin", TokenType = opts.CryptoTokenType.ToString() }
            }
        };
    }

    private static SecurityControlsSection CollectSecurityControls(DigitalSigningOptions opts) => new()
    {
        Controls =
        [
            new SecurityControl("SC-001", "Authentication", "JWT Bearer Token",
                "All API endpoints require JWT authentication (HMAC-SHA256)",
                true, "Phase 1"),
            new SecurityControl("SC-002", "Authorization", "Role-Based Access Control",
                "Admin endpoints restricted to AdminOnly policy",
                true, "Phase 1"),
            new SecurityControl("SC-003", "Transport Security", "HTTPS / TLS 1.2+",
                "All SignServer communication via HTTPS with TLS 1.2+",
                !opts.SkipTlsValidation, "Phase 2"),
            new SecurityControl("SC-004", "Mutual Authentication", "mTLS (Client Certificate)",
                "API ↔ SignServer uses mutual TLS with X.509 client certificates",
                opts.RequireMtls, "Phase 2"),
            new SecurityControl("SC-005", "Worker Authentication", "ClientCertAuthorizer",
                "All SignServer workers require client certificate (not NOAUTH)",
                true, "Phase 2"),
            new SecurityControl("SC-006", "Rate Limiting", "Fixed Window",
                $"signing: {opts.SigningRateLimitPerMinute}/min, provision: 3/min",
                true, "Phase 3"),
            new SecurityControl("SC-007", "Network Isolation", "Docker Networks",
                "3 isolated networks: public, signing (internal), data (internal)",
                true, "Phase 3"),
            new SecurityControl("SC-008", "Container Hardening", "Read-Only + No-New-Privileges",
                "Containers run read-only with no-new-privileges security option",
                true, "Phase 3"),
            new SecurityControl("SC-009", "Audit Logging", "Structured Logging",
                "Signing operations logged with correlation ID, duration, document hash",
                opts.EnableAuditLogging, "Phase 3"),
            new SecurityControl("SC-010", "Certificate Monitoring", "Background Service",
                "CertificateExpiryMonitorService checks cert expiry periodically",
                true, "Phase 3"),
            new SecurityControl("SC-011", "Key Protection", opts.CryptoTokenType.ToString(),
                opts.CryptoTokenType == CryptoTokenType.PKCS11
                    ? "PKCS#11 (SoftHSM2) — keys non-extractable, FIPS 140-2 Level 1"
                    : "PKCS#12 file-based — keys encrypted with strong password",
                opts.CryptoTokenType == CryptoTokenType.PKCS11, "Phase 4"),
            new SecurityControl("SC-012", "Security Headers", "OWASP Recommended",
                "HSTS, CSP, Permissions-Policy, COEP, COOP, CORP, X-Frame-Options, etc.",
                true, "Phase 4"),
            new SecurityControl("SC-013", "Compliance Audit", "Automated 19-Check System",
                "SecurityComplianceService: A-F grading across 4 security phases",
                true, "Phase 4"),
            new SecurityControl("SC-014", "Vulnerability Scanning", "Trivy Container Scanner",
                "Docker Compose profile 'security-scan' runs Trivy against all images",
                true, "Phase 4"),
            new SecurityControl("SC-015", "Penetration Testing", "Automated OWASP Script",
                "scripts/pentest.sh: OWASP Top 10, SignServer, EJBCA security testing",
                true, "Phase 4"),
            new SecurityControl("SC-016", "Certificate Rotation", "Automated Script",
                "scripts/rotate-certs.sh: API, admin, and worker cert rotation with grace period",
                true, "Phase 4"),
            new SecurityControl("SC-017", "Secret Management", "Docker Secrets",
                "All passwords/PINs stored as Docker Secrets, not environment variables",
                !string.IsNullOrEmpty(opts.ClientCertificatePasswordFile), "Phase 1")
        ]
    };

    private static AccessControlMatrixSection CollectAccessControlMatrix() => new()
    {
        Endpoints =
        [
            new EndpointAccess("/api/auth/login", "POST", "Anonymous", "User authentication"),
            new EndpointAccess("/api/auth/refresh", "POST", "Authenticated", "Token refresh"),
            new EndpointAccess("/api/patients", "GET/POST", "Authenticated", "Patient management"),
            new EndpointAccess("/api/admin/signing/*", "ALL", "AdminOnly", "Signing administration"),
            new EndpointAccess("/api/admin/signing/compliance-audit", "GET", "AdminOnly", "Compliance audit (no rate limit)"),
            new EndpointAccess("/api/admin/signing/security-audit-evidence", "GET", "AdminOnly", "Audit evidence export"),
            new EndpointAccess("/api/admin/signing/pentest", "POST", "AdminOnly", "Run penetration tests"),
            new EndpointAccess("/api/admin/signing/test-sign", "POST", "AdminOnly + Rate Limited", "Test signing"),
            new EndpointAccess("/api/user-signatures/provision", "POST", "AdminOnly + Rate Limited (3/min)", "Provision signing worker")
        ],
        ServiceToService =
        [
            new ServiceAccess("IVF API", "SignServer", "mTLS (X.509 client cert)", "HTTPS 8443, ivf-signing network"),
            new ServiceAccess("IVF API", "EJBCA", "mTLS (X.509 client cert)", "HTTPS 8443, ivf-signing network"),
            new ServiceAccess("IVF API", "PostgreSQL", "Password (Docker Secret)", "Port 5432, ivf-data network"),
            new ServiceAccess("IVF API", "Redis", "Password", "Port 6379, ivf-data network"),
            new ServiceAccess("IVF API", "MinIO", "Access Key (Docker Secret)", "Port 9000, ivf-data network"),
            new ServiceAccess("SignServer", "SignServer DB", "Password (Docker Secret)", "Port 5432, ivf-data network"),
            new ServiceAccess("EJBCA", "EJBCA DB", "Password (Docker Secret)", "Port 5432, ivf-data network")
        ]
    };

    private static NetworkTopologySection CollectNetworkTopology() => new()
    {
        Networks =
        [
            new NetworkInfo("ivf-public", "bridge", false,
                ["IVF API (port 5000)"],
                "External access network — API exposed to clients"),
            new NetworkInfo("ivf-signing", "bridge", true,
                ["IVF API", "SignServer (port 8443)", "EJBCA (port 8443)"],
                "Internal signing network — no internet access"),
            new NetworkInfo("ivf-data", "bridge", true,
                ["IVF API", "PostgreSQL", "Redis", "MinIO", "SignServer DB", "EJBCA DB"],
                "Internal data network — no internet access")
        ],
        ExposedPorts =
        [
            new ExposedPort(5000, "IVF API", "HTTP (behind reverse proxy)"),
            new ExposedPort(8443, "EJBCA Admin", "HTTPS — firewall restricted"),
            new ExposedPort(9443, "SignServer Admin", "HTTPS — firewall restricted"),
            new ExposedPort(9000, "MinIO API", "localhost only (127.0.0.1)"),
            new ExposedPort(9001, "MinIO Console", "localhost only (127.0.0.1)")
        ],
        RemovedPorts =
        [
            new ExposedPort(9080, "SignServer HTTP", "REMOVED in Phase 3 — no HTTP access"),
            new ExposedPort(5432, "PostgreSQL", "REMOVED — internal only"),
            new ExposedPort(6379, "Redis", "REMOVED — internal only")
        ]
    };

    private static DataProtectionSection CollectDataProtection(DigitalSigningOptions opts) => new()
    {
        EncryptionAtRest = new EncryptionInfo
        {
            KeystoreEncryption = "AES-256-CBC (PKCS#12 password-protected)",
            DatabaseEncryption = "PostgreSQL — relies on volume encryption",
            ObjectStorage = "MinIO — server-side encryption available",
            TokenStorage = opts.CryptoTokenType == CryptoTokenType.PKCS11
                ? "SoftHSM2 PKCS#11 — keys non-extractable"
                : "PKCS#12 files — password-encrypted"
        },
        EncryptionInTransit = new TransitEncryption
        {
            ApiToSignServer = "TLS 1.2+ with mTLS",
            ApiToDatabase = "PostgreSQL SSL (configurable)",
            ApiToRedis = "Redis TLS (optional)",
            ApiToMinIO = "HTTP (internal network only — TLS available)"
        },
        DataClassification =
        [
            new DataClass("Private Keys", "Critical", "PKCS#12/PKCS#11 encrypted, chmod 400, Docker volume"),
            new DataClass("Patient PII", "High", "PostgreSQL with access control, encrypted backups"),
            new DataClass("Signed PDFs", "High", "MinIO object storage with access control"),
            new DataClass("JWT Secrets", "Critical", "Docker Secrets (file-based, not in env vars)"),
            new DataClass("Database Passwords", "Critical", "Docker Secrets (file-based)"),
            new DataClass("Audit Logs", "Medium", "Structured logging with correlation IDs")
        ]
    };

    private static IncidentResponseSection CollectIncidentResponsePlan() => new()
    {
        Procedures =
        [
            new IncidentProcedure("Key Compromise", "Critical",
            [
                "1. Immediately deactivate all SignServer workers",
                "2. Revoke compromised certificates via EJBCA",
                "3. Generate new key pairs and certificates",
                "4. Upload new keys to workers and reactivate",
                "5. Update CRL/OCSP responders",
                "6. Investigate breach scope and timeline",
                "7. Notify affected parties per compliance requirements"
            ]),
            new IncidentProcedure("Certificate Expiry", "High",
            [
                "1. CertificateExpiryMonitorService alerts at 30 days",
                "2. Run scripts/rotate-certs.sh --type <type> --dry-run",
                "3. Execute rotation: scripts/rotate-certs.sh --type <type>",
                "4. Verify new certificates via compliance-audit endpoint",
                "5. Update authorized clients if cert serial changed"
            ]),
            new IncidentProcedure("Unauthorized Access Attempt", "High",
            [
                "1. Review audit logs for correlation IDs",
                "2. Check rate limiting counters",
                "3. Verify JWT token source and claims",
                "4. Block suspicious IPs at firewall/reverse proxy",
                "5. Rotate compromised credentials if needed"
            ]),
            new IncidentProcedure("Container Compromise", "Critical",
            [
                "1. Isolate affected container (docker stop)",
                "2. Capture forensic image (docker commit)",
                "3. Review container logs and audit trail",
                "4. Rebuild container from trusted image",
                "5. Run Trivy scan on all images",
                "6. Verify no lateral movement via network isolation"
            ])
        ]
    };

    private static PentestCapabilitiesSection CollectPentestCapabilities() => new()
    {
        AvailableTools =
        [
            new PentestTool("pentest.sh", "Automated OWASP + infrastructure testing",
                "scripts/pentest.sh --target all",
                ["OWASP Top 10", "SignServer mTLS", "EJBCA access control", "Security headers"]),
            new PentestTool("Trivy Scanner", "Container vulnerability scanning",
                "docker compose --profile security-scan up trivy-scan",
                ["CVE detection", "OS package vulnerabilities", "Misconfigurations"]),
            new PentestTool("Compliance Audit", "Automated compliance checking",
                "GET /api/admin/signing/compliance-audit",
                ["19-check audit", "A-F grading", "Phase 1-4 coverage"]),
            new PentestTool("Certificate Rotation", "Automated cert lifecycle",
                "scripts/rotate-certs.sh --check",
                ["Expiry monitoring", "Automated renewal", "Grace period support"])
        ],
        TestCoverage = new TestCoverage
        {
            OWASPTop10 = "A01-A10 covered by pentest.sh (automated)",
            SignServerSecurity = "mTLS, port exposure, admin access, health (automated)",
            EJBCASecurity = "Admin access, REST API, enrollment (automated)",
            SecurityHeaders = "9 OWASP headers + server identification checks (automated)",
            ContainerSecurity = "Trivy CVE scanning (automated)",
            CryptographicControls = "PKCS#11 key protection, TLS version, cert chain (automated)",
            ManualTestingRequired = new[]
            {
                "Business logic testing",
                "Social engineering assessment",
                "Physical security review",
                "Supply chain analysis",
                "Advanced persistent threat simulation"
            }
        }
    };

    private static AuditTrailConfigSection CollectAuditTrailConfig(DigitalSigningOptions opts) => new()
    {
        Enabled = opts.EnableAuditLogging,
        LoggedEvents =
        [
            "PDF signing operation (worker, document hash, duration, result)",
            "Worker provisioning (user, worker ID, certificate details)",
            "Certificate expiry warnings (worker, days remaining)",
            "Authentication failures (JWT validation, mTLS failures)",
            "Rate limit violations (endpoint, user, count)",
            "Security compliance audit runs (score, grade, findings)"
        ],
        CorrelationTracking = "Each signing operation includes a unique correlation ID",
        RetentionPolicy = "Follows application logging configuration (default: structured JSON)",
        LogDestinations = new[]
        {
            "Console (structured JSON in production)",
            "File (configurable via Serilog/NLog)",
            "External SIEM (configurable via log forwarding)"
        }
    };

    private static List<string> GenerateAuditRecommendations(
        DigitalSigningOptions opts, ComplianceAuditResult? compliance)
    {
        var recs = new List<string>();

        if (!opts.EnableAuditLogging)
            recs.Add("AUDIT-REC-001: Enable audit logging for full compliance trail (DigitalSigning__EnableAuditLogging=true)");

        if (opts.CryptoTokenType == CryptoTokenType.P12)
            recs.Add("AUDIT-REC-002: Migrate to PKCS#11 (SoftHSM2) for FIPS 140-2 Level 1 compliance");

        if (opts.SkipTlsValidation)
            recs.Add("AUDIT-REC-003: CRITICAL — Enable TLS validation (SkipTlsValidation=false)");

        if (!opts.RequireMtls)
            recs.Add("AUDIT-REC-004: Enforce mutual TLS (RequireMtls=true)");

        if (string.IsNullOrEmpty(opts.ClientCertificatePasswordFile))
            recs.Add("AUDIT-REC-005: Use Docker Secrets for certificate passwords instead of plaintext");

        if (compliance != null)
        {
            if (compliance.ComplianceScore < 80)
                recs.Add($"AUDIT-REC-006: Compliance score is {compliance.ComplianceScore}% (Grade {compliance.Grade}) — address failing checks");

            if (compliance.Summary.Failed > 0)
                recs.Add($"AUDIT-REC-007: {compliance.Summary.Failed} compliance check(s) failing — review /compliance-audit for details");
        }

        recs.Add("AUDIT-REC-008: Schedule regular penetration tests (monthly/quarterly): scripts/pentest.sh --target all");
        recs.Add("AUDIT-REC-009: Run container vulnerability scans before each deployment: docker compose --profile security-scan up trivy-scan");
        recs.Add("AUDIT-REC-010: Review certificate expiry monthly: scripts/rotate-certs.sh --check");

        return recs;
    }
}

// ─── Audit Evidence Package Model ────────────────────────

public class AuditEvidencePackage
{
    public DateTime GeneratedAt { get; set; }
    public string AuditVersion { get; set; } = "1.0";
    public SystemInfoSection SystemInfo { get; set; } = new();
    public SecurityConfigSection SecurityConfiguration { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ComplianceAuditResult? ComplianceAudit { get; set; }

    public CertificateInventorySection CertificateInventory { get; set; } = new();
    public SecurityControlsSection SecurityControls { get; set; } = new();
    public AccessControlMatrixSection AccessControlMatrix { get; set; } = new();
    public NetworkTopologySection NetworkTopology { get; set; } = new();
    public DataProtectionSection DataProtection { get; set; } = new();
    public IncidentResponseSection IncidentResponse { get; set; } = new();
    public PentestCapabilitiesSection PentestCapabilities { get; set; } = new();
    public AuditTrailConfigSection AuditTrailConfig { get; set; } = new();
    public List<string> Recommendations { get; set; } = [];
}

// ─── Section Models ──────────────────────────────────────

public class SystemInfoSection
{
    public string ApplicationName { get; set; } = "";
    public string Environment { get; set; } = "";
    public string RuntimeVersion { get; set; } = "";
    public string OsPlatform { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string AssemblyVersion { get; set; } = "";
    public string DotNetVersion { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string ContainerRuntime { get; set; } = "";
}

public class SecurityConfigSection
{
    public bool SigningEnabled { get; set; }
    public string SignServerUrl { get; set; } = "";
    public bool UsesHttps { get; set; }
    public bool TlsValidationEnabled { get; set; }
    public bool MtlsEnabled { get; set; }
    public bool MtlsEnforced { get; set; }
    public bool AuditLoggingEnabled { get; set; }
    public string CryptoTokenType { get; set; } = "";
    public bool IsFipsCompliant { get; set; }
    public string? Pkcs11Library { get; set; }
    public bool ClientCertConfigured { get; set; }
    public bool TrustedCaConfigured { get; set; }
    public string SecretManagement { get; set; } = "";
    public CertExpiryConfig CertExpiryMonitoring { get; set; } = new();
    public RateLimitConfig RateLimiting { get; set; } = new();
}

public class CertExpiryConfig
{
    public int WarningDays { get; set; }
    public int CheckIntervalMinutes { get; set; }
}

public class RateLimitConfig
{
    public int SigningPerMinute { get; set; }
    public int ProvisioningPerMinute { get; set; }
}

public class CertificateInventorySection
{
    public int TotalCertificates { get; set; }
    public List<CertificateInfo> Certificates { get; set; } = [];
    public WorkerCertInfo[] SignServerWorkers { get; set; } = [];
}

public class CertificateInfo
{
    public string Name { get; set; } = "";
    public string? Subject { get; set; }
    public string? Issuer { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? NotBefore { get; set; }
    public DateTime? NotAfter { get; set; }
    public int? DaysRemaining { get; set; }
    public string? KeyAlgorithm { get; set; }
    public string? SignatureAlgorithm { get; set; }
    public string? Thumbprint { get; set; }
    public int KeySize { get; set; }
    public string StorageType { get; set; } = "";
    public string? FilePath { get; set; }
}

public class WorkerCertInfo
{
    public int WorkerId { get; set; }
    public string WorkerName { get; set; } = "";
    public string TokenType { get; set; } = "";
}

public class SecurityControlsSection
{
    public List<SecurityControl> Controls { get; set; } = [];
}

public record SecurityControl(
    string Id,
    string Category,
    string ControlName,
    string Description,
    bool Implemented,
    string Phase);

public class AccessControlMatrixSection
{
    public List<EndpointAccess> Endpoints { get; set; } = [];
    public List<ServiceAccess> ServiceToService { get; set; } = [];
}

public record EndpointAccess(string Path, string Method, string RequiredRole, string Description);
public record ServiceAccess(string Source, string Destination, string AuthMethod, string NetworkPath);

public class NetworkTopologySection
{
    public List<NetworkInfo> Networks { get; set; } = [];
    public List<ExposedPort> ExposedPorts { get; set; } = [];
    public List<ExposedPort> RemovedPorts { get; set; } = [];
}

public record NetworkInfo(string Name, string Driver, bool Internal, string[] Services, string Description);
public record ExposedPort(int Port, string Service, string Notes);

public class DataProtectionSection
{
    public EncryptionInfo EncryptionAtRest { get; set; } = new();
    public TransitEncryption EncryptionInTransit { get; set; } = new();
    public List<DataClass> DataClassification { get; set; } = [];
}

public class EncryptionInfo
{
    public string KeystoreEncryption { get; set; } = "";
    public string DatabaseEncryption { get; set; } = "";
    public string ObjectStorage { get; set; } = "";
    public string TokenStorage { get; set; } = "";
}

public class TransitEncryption
{
    public string ApiToSignServer { get; set; } = "";
    public string ApiToDatabase { get; set; } = "";
    public string ApiToRedis { get; set; } = "";
    public string ApiToMinIO { get; set; } = "";
}

public record DataClass(string DataType, string Classification, string Protection);

public class IncidentResponseSection
{
    public List<IncidentProcedure> Procedures { get; set; } = [];
}

public record IncidentProcedure(string Scenario, string Severity, string[] Steps);

public class PentestCapabilitiesSection
{
    public List<PentestTool> AvailableTools { get; set; } = [];
    public TestCoverage TestCoverage { get; set; } = new();
}

public record PentestTool(string Name, string Description, string Command, string[] Categories);

public class TestCoverage
{
    public string OWASPTop10 { get; set; } = "";
    public string SignServerSecurity { get; set; } = "";
    public string EJBCASecurity { get; set; } = "";
    public string SecurityHeaders { get; set; } = "";
    public string ContainerSecurity { get; set; } = "";
    public string CryptographicControls { get; set; } = "";
    public string[] ManualTestingRequired { get; set; } = [];
}

public class AuditTrailConfigSection
{
    public bool Enabled { get; set; }
    public List<string> LoggedEvents { get; set; } = [];
    public string CorrelationTracking { get; set; } = "";
    public string RetentionPolicy { get; set; } = "";
    public string[] LogDestinations { get; set; } = [];
}
