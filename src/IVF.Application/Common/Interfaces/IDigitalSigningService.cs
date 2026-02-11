namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Service interface for digitally signing PDF documents via SignServer/EJBCA.
/// </summary>
public interface IDigitalSigningService
{
    /// <summary>
    /// Sign a PDF document using the configured SignServer worker.
    /// </summary>
    /// <param name="pdfBytes">Original unsigned PDF bytes</param>
    /// <param name="metadata">Optional metadata for the signature (reason, location, contact)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Signed PDF bytes</returns>
    Task<byte[]> SignPdfAsync(byte[] pdfBytes, SigningMetadata? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sign a PDF with a specific SignServer worker and optional visible handwritten signature image.
    /// Used for per-user signing where each user has their own certificate and drawn signature.
    /// </summary>
    /// <param name="pdfBytes">Original unsigned PDF bytes</param>
    /// <param name="workerName">SignServer worker name (per-user worker)</param>
    /// <param name="metadata">Signature metadata</param>
    /// <param name="signatureImagePng">PNG bytes of handwritten signature image to embed as visible stamp (null = no visible image)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Signed PDF bytes (with visible signature image overlay if provided)</returns>
    Task<byte[]> SignPdfWithUserAsync(
        byte[] pdfBytes,
        string workerName,
        SigningMetadata? metadata = null,
        byte[]? signatureImagePng = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the signing service is available and properly configured.
    /// </summary>
    Task<SigningHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether digital signing is enabled in configuration.
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Metadata attached to the digital signature.
/// </summary>
public record SigningMetadata(
    string? Reason = null,
    string? Location = null,
    string? ContactInfo = null,
    string? SignerName = null
);

/// <summary>
/// Health status of the signing infrastructure.
/// </summary>
public record SigningHealthStatus(
    bool IsHealthy,
    bool SignServerReachable,
    bool WorkerConfigured,
    string? ErrorMessage = null,
    string? SignServerVersion = null,
    string? CertificateSubject = null,
    DateTime? CertificateExpiry = null
);
