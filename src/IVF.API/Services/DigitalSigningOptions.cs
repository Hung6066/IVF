namespace IVF.API.Services;

/// <summary>
/// Type of cryptographic token used by SignServer workers.
/// Determines how private keys are stored and accessed.
/// </summary>
public enum CryptoTokenType
{
    /// <summary>
    /// PKCS#12 file-based keystore. Simple but keys can be extracted.
    /// Default for development and Phase 1-3.
    /// </summary>
    P12,

    /// <summary>
    /// PKCS#11 hardware/software security module (SoftHSM2 or HSM).
    /// FIPS 140-2 Level 1 compliant. Keys cannot be extracted.
    /// Requires SoftHSM2 or hardware HSM. Phase 4.
    /// </summary>
    PKCS11
}

/// <summary>
/// Configuration options for the digital signing integration with SignServer/EJBCA.
/// Bind from appsettings.json section "DigitalSigning".
/// </summary>
public class DigitalSigningOptions
{
    public const string SectionName = "DigitalSigning";

    /// <summary>
    /// Enable or disable digital signing globally.
    /// When false, PDFs are returned unsigned.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Base URL of SignServer REST API (e.g., http://signserver:8080/signserver).
    /// </summary>
    public string SignServerUrl { get; set; } = "http://localhost:9080/signserver";

    /// <summary>
    /// Docker container name for SignServer CLI access.
    /// Used by admin endpoints to query worker status/config via docker exec.
    /// </summary>
    public string SignServerContainerName { get; set; } = "ivf-signserver";

    /// <summary>
    /// Name of the SignServer PDF signing worker (e.g., "PDFSigner").
    /// </summary>
    public string WorkerName { get; set; } = "PDFSigner";

    /// <summary>
    /// Optional worker ID (alternative to WorkerName). If set, takes priority.
    /// </summary>
    public int? WorkerId { get; set; }

    /// <summary>
    /// HTTP timeout for SignServer requests in seconds (default 30s).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Default signature reason (e.g., "Xác nhận báo cáo y tế IVF").
    /// </summary>
    public string DefaultReason { get; set; } = "Xác nhận báo cáo y tế IVF";

    /// <summary>
    /// Default signature location (e.g., "IVF Clinic").
    /// </summary>
    public string DefaultLocation { get; set; } = "IVF Clinic";

    /// <summary>
    /// Default contact info embedded in signature.
    /// </summary>
    public string DefaultContactInfo { get; set; } = "support@ivf-clinic.vn";

    /// <summary>
    /// EJBCA admin URL for certificate management links.
    /// </summary>
    public string EjbcaUrl { get; set; } = "https://localhost:8443/ejbca";

    /// <summary>
    /// Whether to skip TLS certificate validation for SignServer (dev only).
    /// MUST be false in production.
    /// </summary>
    public bool SkipTlsValidation { get; set; } = true;

    /// <summary>
    /// Optional client certificate path for mutual TLS with SignServer.
    /// Required when RequireMtls=true.
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Optional client certificate password (direct value).
    /// Prefer ClientCertificatePasswordFile for production.
    /// </summary>
    public string? ClientCertificatePassword { get; set; }

    /// <summary>
    /// Path to file containing client certificate password (Docker Secret).
    /// Takes precedence over ClientCertificatePassword.
    /// Example: /run/secrets/api_cert_password
    /// </summary>
    public string? ClientCertificatePasswordFile { get; set; }

    /// <summary>
    /// Path to trusted CA certificate chain (PEM format).
    /// Used to validate SignServer's TLS certificate.
    /// </summary>
    public string? TrustedCaCertPath { get; set; }

    /// <summary>
    /// Require mutual TLS for SignServer communication.
    /// When true, ClientCertificatePath must be configured.
    /// </summary>
    public bool RequireMtls { get; set; } = false;

    /// <summary>
    /// Enable detailed audit logging of signing operations.
    /// Logs worker name, document hash, timestamp, result.
    /// </summary>
    public bool EnableAuditLogging { get; set; } = false;

    /// <summary>
    /// Whether to add a visible signature stamp on the PDF (page number, position).
    /// </summary>
    public bool AddVisibleSignature { get; set; } = true;

    /// <summary>
    /// Page number for visible signature (0 = last page, 1 = first page).
    /// </summary>
    public int VisibleSignaturePage { get; set; } = 0;

    /// <summary>
    /// Number of days before certificate expiry to start warning.
    /// The certificate expiry monitor uses this threshold.
    /// </summary>
    public int CertExpiryWarningDays { get; set; } = 30;

    /// <summary>
    /// How often (in minutes) to check certificate expiry.
    /// Default: 60 minutes (1 hour).
    /// </summary>
    public int CertExpiryCheckIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum signing requests per minute per user.
    /// Applied to /process and test-sign endpoints.
    /// Default: 30 per minute.
    /// </summary>
    public int SigningRateLimitPerMinute { get; set; } = 30;

    // ─── TSA (Timestamp Authority) for PAdES-LTV ───

    /// <summary>
    /// Name of the SignServer TimeStampSigner worker.
    /// When set, PDFSigner workers use TSA_WORKER property for RFC 3161 timestamps.
    /// </summary>
    public string? TsaWorkerName { get; set; }

    // ─── OCSP Configuration ───

    /// <summary>
    /// OCSP responder URL for certificate revocation checking.
    /// Typically the EJBCA built-in OCSP endpoint.
    /// Example: https://ejbca:8443/ejbca/publicweb/status/ocsp
    /// </summary>
    public string? OcspResponderUrl { get; set; }

    // ─── Phase 4: PKCS#11 / SoftHSM2 Configuration ───

    /// <summary>
    /// Type of cryptographic token for new worker provisioning.
    /// P12 = PKCS#12 file-based (default); PKCS11 = SoftHSM2 or hardware HSM.
    /// </summary>
    public CryptoTokenType CryptoTokenType { get; set; } = CryptoTokenType.P12;

    /// <summary>
    /// PKCS#11 shared library name registered in SignServer.
    /// Used when CryptoTokenType = PKCS11.
    /// Common values: "SOFTHSM" (SoftHSM2), "LUNASA" (Thales Luna), "UTIMACO" (Utimaco).
    /// </summary>
    public string Pkcs11SharedLibraryName { get; set; } = "SOFTHSM";

    /// <summary>
    /// PKCS#11 slot/token label for key operations.
    /// For SoftHSM2, this is the token label set during initialization.
    /// </summary>
    public string Pkcs11SlotLabel { get; set; } = "SignServerToken";

    /// <summary>
    /// PKCS#11 user PIN for token authentication (direct value).
    /// Prefer Pkcs11PinFile for production (Docker Secret).
    /// </summary>
    public string? Pkcs11Pin { get; set; }

    /// <summary>
    /// Path to file containing PKCS#11 PIN (Docker Secret).
    /// Takes precedence over Pkcs11Pin.
    /// Example: /run/secrets/softhsm_pin
    /// </summary>
    public string? Pkcs11PinFile { get; set; }

    /// <summary>
    /// Resolves the PKCS#11 PIN from file or direct value.
    /// Docker Secret file takes precedence.
    /// </summary>
    public string? ResolvePkcs11Pin()
    {
        if (!string.IsNullOrEmpty(Pkcs11PinFile) && File.Exists(Pkcs11PinFile))
            return File.ReadAllText(Pkcs11PinFile).Trim();
        return Pkcs11Pin;
    }

    /// <summary>
    /// Resolves the client certificate password from file or direct value.
    /// Docker Secrets file takes precedence.
    /// </summary>
    public string? ResolveClientCertificatePassword()
    {
        if (!string.IsNullOrEmpty(ClientCertificatePasswordFile) &&
            File.Exists(ClientCertificatePasswordFile))
        {
            return File.ReadAllText(ClientCertificatePasswordFile).Trim();
        }
        return ClientCertificatePassword;
    }

    /// <summary>
    /// Validates production security configuration.
    /// Throws if RequireMtls=true but certificates are not configured.
    /// </summary>
    public void ValidateProduction()
    {
        if (!Enabled) return;

        if (RequireMtls)
        {
            if (string.IsNullOrEmpty(ClientCertificatePath))
                throw new InvalidOperationException(
                    "DigitalSigning.ClientCertificatePath is required when RequireMtls=true");

            if (!File.Exists(ClientCertificatePath))
                throw new InvalidOperationException(
                    $"Client certificate not found: {ClientCertificatePath}");

            if (string.IsNullOrEmpty(ResolveClientCertificatePassword()))
                throw new InvalidOperationException(
                    "Client certificate password is required. Set ClientCertificatePassword or ClientCertificatePasswordFile.");
        }

        if (SkipTlsValidation && RequireMtls)
            throw new InvalidOperationException(
                "SkipTlsValidation cannot be true when RequireMtls is enabled. " +
                "Configure TrustedCaCertPath for custom CA validation.");

        if (SignServerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && RequireMtls)
            throw new InvalidOperationException(
                "SignServerUrl must use HTTPS when RequireMtls is enabled.");

        // PKCS#11 validation
        if (CryptoTokenType == CryptoTokenType.PKCS11)
        {
            if (string.IsNullOrEmpty(Pkcs11SharedLibraryName))
                throw new InvalidOperationException(
                    "Pkcs11SharedLibraryName is required when CryptoTokenType=PKCS11.");
            if (string.IsNullOrEmpty(ResolvePkcs11Pin()))
                throw new InvalidOperationException(
                    "PKCS#11 PIN is required. Set Pkcs11Pin or Pkcs11PinFile.");
        }
    }
}
