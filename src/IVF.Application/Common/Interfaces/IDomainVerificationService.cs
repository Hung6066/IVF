namespace IVF.Application.Common.Interfaces;

public interface IDomainVerificationService
{
    /// <summary>
    /// Verifies custom domain DNS configuration:
    /// 1. CNAME record pointing domain to our platform
    /// 2. TXT record at _ivf-verify.{domain} containing the verification token
    /// </summary>
    Task<DomainVerificationResult> VerifyDomainAsync(string domain, string expectedToken, CancellationToken ct = default);
}

public record DomainVerificationResult(
    bool CnameVerified,
    bool TxtVerified,
    string? CnameTarget,
    List<string> TxtRecords);
