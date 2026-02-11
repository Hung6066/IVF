namespace IVF.API.Services;

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
    /// </summary>
    public bool SkipTlsValidation { get; set; } = true;

    /// <summary>
    /// Optional client certificate path for mutual TLS with SignServer.
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Optional client certificate password.
    /// </summary>
    public string? ClientCertificatePassword { get; set; }

    /// <summary>
    /// Whether to add a visible signature stamp on the PDF (page number, position).
    /// </summary>
    public bool AddVisibleSignature { get; set; } = true;

    /// <summary>
    /// Page number for visible signature (0 = last page, 1 = first page).
    /// </summary>
    public int VisibleSignaturePage { get; set; } = 0;
}
