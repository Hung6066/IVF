using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace IVF.API.Services;

/// <summary>
/// Background service that monitors certificate expiry for mTLS client certificates
/// and SignServer TLS certificates. Logs warnings when certificates are approaching
/// expiry and errors when they are critically close.
///
/// Runs every hour (configurable via CertExpiryCheckIntervalMinutes).
/// </summary>
public sealed class CertificateExpiryMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CertificateExpiryMonitorService> _logger;

    public CertificateExpiryMonitorService(
        IServiceProvider serviceProvider,
        ILogger<CertificateExpiryMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Latest certificate status snapshot for the security-status endpoint.
    /// </summary>
    public CertificateExpiryStatus? LatestStatus { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app to finish starting
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        _logger.LogInformation("Certificate expiry monitor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckCertificateExpiryAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during certificate expiry check");
            }

            using var scope = _serviceProvider.CreateScope();
            var opts = scope.ServiceProvider.GetRequiredService<IOptions<DigitalSigningOptions>>().Value;
            var interval = TimeSpan.FromMinutes(opts.CertExpiryCheckIntervalMinutes);

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task CheckCertificateExpiryAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<DigitalSigningOptions>>().Value;

        if (!opts.Enabled)
        {
            LatestStatus = null;
            return;
        }

        var warningDays = opts.CertExpiryWarningDays;
        var results = new List<CertExpiryInfo>();

        // 1. Check API client certificate
        if (!string.IsNullOrEmpty(opts.ClientCertificatePath) && File.Exists(opts.ClientCertificatePath))
        {
            try
            {
                var password = opts.ResolveClientCertificatePassword();
                using var cert = X509CertificateLoader.LoadPkcs12FromFile(opts.ClientCertificatePath, password);
                var daysRemaining = (cert.NotAfter - DateTime.UtcNow).TotalDays;
                var info = new CertExpiryInfo(
                    Name: "API Client Certificate (mTLS)",
                    Subject: cert.Subject,
                    SerialNumber: cert.SerialNumber,
                    NotBefore: cert.NotBefore,
                    NotAfter: cert.NotAfter,
                    DaysRemaining: (int)daysRemaining,
                    Status: GetExpiryLevel(daysRemaining, warningDays));

                results.Add(info);
                LogCertExpiry(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read API client certificate at {Path}", opts.ClientCertificatePath);
                results.Add(new CertExpiryInfo(
                    Name: "API Client Certificate (mTLS)",
                    Subject: "ERROR: Cannot read",
                    SerialNumber: null,
                    NotBefore: DateTime.MinValue,
                    NotAfter: DateTime.MinValue,
                    DaysRemaining: -1,
                    Status: "error"));
            }
        }

        // 2. Check trusted CA certificate
        if (!string.IsNullOrEmpty(opts.TrustedCaCertPath) && File.Exists(opts.TrustedCaCertPath))
        {
            try
            {
                using var cert = X509CertificateLoader.LoadCertificateFromFile(opts.TrustedCaCertPath);
                var daysRemaining = (cert.NotAfter - DateTime.UtcNow).TotalDays;
                var info = new CertExpiryInfo(
                    Name: "Trusted CA Certificate",
                    Subject: cert.Subject,
                    SerialNumber: cert.SerialNumber,
                    NotBefore: cert.NotBefore,
                    NotAfter: cert.NotAfter,
                    DaysRemaining: (int)daysRemaining,
                    Status: GetExpiryLevel(daysRemaining, warningDays));

                results.Add(info);
                LogCertExpiry(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read CA certificate at {Path}", opts.TrustedCaCertPath);
            }
        }

        // 3. Check SignServer TLS certificate (remote)
        try
        {
            var tlsCertInfo = await CheckRemoteTlsCertAsync(opts, ct);
            if (tlsCertInfo != null)
            {
                results.Add(tlsCertInfo);
                LogCertExpiry(tlsCertInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check SignServer TLS certificate");
        }

        // Calculate overall status
        var minDays = results.Where(r => r.DaysRemaining >= 0).Select(r => r.DaysRemaining).DefaultIfEmpty(int.MaxValue).Min();
        var overall = minDays switch
        {
            <= 0 => "expired",
            <= 7 => "critical",
            _ when minDays <= warningDays => "warning",
            _ => "healthy"
        };

        LatestStatus = new CertificateExpiryStatus(
            OverallStatus: overall,
            MinDaysRemaining: minDays == int.MaxValue ? null : minDays,
            LastChecked: DateTime.UtcNow,
            Certificates: results);

        _logger.LogInformation(
            "Certificate expiry check complete: Status={Status}, MinDaysRemaining={MinDays}, CertsChecked={Count}",
            overall, minDays == int.MaxValue ? "N/A" : minDays, results.Count);
    }

    private async Task<CertExpiryInfo?> CheckRemoteTlsCertAsync(DigitalSigningOptions opts, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(opts.SignServerUrl) || !opts.SignServerUrl.StartsWith("https://"))
            return null;

        using var handler = new HttpClientHandler();
        X509Certificate2? serverCert = null;

        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            if (cert != null)
                serverCert = X509CertificateLoader.LoadCertificate(cert.GetRawCertData());
            return true; // Accept any cert for monitoring purposes
        };

        // Attach client cert for mTLS
        if (!string.IsNullOrEmpty(opts.ClientCertificatePath) && File.Exists(opts.ClientCertificatePath))
        {
            var password = opts.ResolveClientCertificatePassword();
            handler.ClientCertificates.Add(
                X509CertificateLoader.LoadPkcs12FromFile(opts.ClientCertificatePath, password));
        }

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

        try
        {
            var healthUrl = $"{opts.SignServerUrl.TrimEnd('/')}/healthcheck/signserverhealth";
            await client.GetAsync(healthUrl, ct);
        }
        catch
        {
            // Connection errors are expected if SignServer is down,
            // but we may still have captured the TLS cert
        }

        if (serverCert == null) return null;

        var daysRemaining = (serverCert.NotAfter - DateTime.UtcNow).TotalDays;
        var warningDays = opts.CertExpiryWarningDays;

        return new CertExpiryInfo(
            Name: "SignServer TLS Certificate",
            Subject: serverCert.Subject,
            SerialNumber: serverCert.SerialNumber,
            NotBefore: serverCert.NotBefore,
            NotAfter: serverCert.NotAfter,
            DaysRemaining: (int)daysRemaining,
            Status: GetExpiryLevel(daysRemaining, warningDays));
    }

    private void LogCertExpiry(CertExpiryInfo info)
    {
        if (info.DaysRemaining <= 0)
        {
            _logger.LogCritical(
                "CERTIFICATE EXPIRED: {Name} — Subject={Subject}, Expired={NotAfter}",
                info.Name, info.Subject, info.NotAfter);
        }
        else if (info.DaysRemaining <= 7)
        {
            _logger.LogError(
                "CERTIFICATE CRITICAL: {Name} — expires in {Days} days (Subject={Subject}, NotAfter={NotAfter})",
                info.Name, info.DaysRemaining, info.Subject, info.NotAfter);
        }
        else if (info.DaysRemaining <= 30)
        {
            _logger.LogWarning(
                "CERTIFICATE WARNING: {Name} — expires in {Days} days (Subject={Subject}, NotAfter={NotAfter})",
                info.Name, info.DaysRemaining, info.Subject, info.NotAfter);
        }
        else
        {
            _logger.LogDebug(
                "Certificate OK: {Name} — expires in {Days} days",
                info.Name, info.DaysRemaining);
        }
    }

    private static string GetExpiryLevel(double daysRemaining, int warningDays)
    {
        return daysRemaining switch
        {
            <= 0 => "expired",
            <= 7 => "critical",
            _ when daysRemaining <= warningDays => "warning",
            _ => "healthy"
        };
    }
}

/// <summary>
/// Snapshot of certificate expiry status across all monitored certificates.
/// </summary>
public record CertificateExpiryStatus(
    string OverallStatus,
    int? MinDaysRemaining,
    DateTime LastChecked,
    List<CertExpiryInfo> Certificates);

/// <summary>
/// Expiry information for a single certificate.
/// </summary>
public record CertExpiryInfo(
    string Name,
    string Subject,
    string? SerialNumber,
    DateTime NotBefore,
    DateTime NotAfter,
    int DaysRemaining,
    string Status);
