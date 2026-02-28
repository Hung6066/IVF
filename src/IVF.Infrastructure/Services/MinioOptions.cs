namespace IVF.Infrastructure.Services;

/// <summary>
/// MinIO configuration options
/// </summary>
public class MinioOptions
{
    public const string SectionName = "MinIO";

    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin123";
    public bool UseSSL { get; set; } = false;
    public string DocumentsBucket { get; set; } = "ivf-documents";
    public string SignedPdfsBucket { get; set; } = "ivf-signed-pdfs";
    public string MedicalImagesBucket { get; set; } = "ivf-medical-images";
    public string BackupsBucket { get; set; } = "ivf-backups";
    public string BaseUrl { get; set; } = "http://localhost:9000";

    /// <summary>Path to trusted CA certificate PEM for validating MinIO TLS. If empty, uses system trust store.</summary>
    public string? TrustedCaCertPath { get; set; }
}
