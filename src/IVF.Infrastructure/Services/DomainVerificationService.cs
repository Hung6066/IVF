using DnsClient;
using DnsClient.Protocol;
using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

public class DomainVerificationService : IDomainVerificationService
{
    private readonly ILogger<DomainVerificationService> _logger;
    private static readonly string PlatformCname = "app.ivf.clinic";

    public DomainVerificationService(ILogger<DomainVerificationService> logger)
    {
        _logger = logger;
    }

    public async Task<DomainVerificationResult> VerifyDomainAsync(
        string domain, string expectedToken, CancellationToken ct = default)
    {
        var client = new LookupClient(new LookupClientOptions
        {
            UseCache = false,
            Timeout = TimeSpan.FromSeconds(10),
            Retries = 2
        });

        var cnameVerified = false;
        string? cnameTarget = null;
        var txtRecords = new List<string>();
        var txtVerified = false;

        // 1. Check CNAME record — domain should point to our platform
        try
        {
            var cnameResult = await client.QueryAsync(domain, QueryType.CNAME, cancellationToken: ct);
            var cname = cnameResult.Answers.CnameRecords().FirstOrDefault();
            if (cname is not null)
            {
                cnameTarget = cname.CanonicalName.Value.TrimEnd('.');
                cnameVerified = cnameTarget.Equals(PlatformCname, StringComparison.OrdinalIgnoreCase);
                _logger.LogInformation("CNAME for {Domain}: {Target} (verified: {Verified})", domain, cnameTarget, cnameVerified);
            }
            else
            {
                // Also check A record — might point directly to our IP
                var aResult = await client.QueryAsync(domain, QueryType.A, cancellationToken: ct);
                if (aResult.Answers.ARecords().Any())
                {
                    cnameVerified = true; // A record exists, domain resolves
                    cnameTarget = aResult.Answers.ARecords().First().Address.ToString();
                    _logger.LogInformation("A record for {Domain}: {IP}", domain, cnameTarget);
                }
            }
        }
        catch (DnsResponseException ex)
        {
            _logger.LogWarning(ex, "CNAME lookup failed for {Domain}", domain);
        }

        // 2. Check TXT record at _ivf-verify.<domain> for ownership verification
        var txtHost = $"_ivf-verify.{domain}";
        try
        {
            var txtResult = await client.QueryAsync(txtHost, QueryType.TXT, cancellationToken: ct);
            foreach (var txt in txtResult.Answers.TxtRecords())
            {
                foreach (var value in txt.Text)
                {
                    txtRecords.Add(value);
                    if (value.Equals(expectedToken, StringComparison.OrdinalIgnoreCase))
                    {
                        txtVerified = true;
                    }
                }
            }
            _logger.LogInformation("TXT records for {Host}: [{Records}] (verified: {Verified})",
                txtHost, string.Join(", ", txtRecords), txtVerified);
        }
        catch (DnsResponseException ex)
        {
            _logger.LogWarning(ex, "TXT lookup failed for {Host}", txtHost);
        }

        return new DomainVerificationResult(cnameVerified, txtVerified, cnameTarget, txtRecords);
    }
}
