using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IVF.API.Services;

/// <summary>
/// No-op stub for biometric matching on non-Windows platforms (e.g. MacOS, Linux)
/// where DPFP libraries are not available.
/// </summary>
public class StubBiometricMatcherService : BackgroundService, IBiometricMatcher
{
    private readonly ILogger<StubBiometricMatcherService> _logger;

    public StubBiometricMatcherService(ILogger<StubBiometricMatcherService> logger)
    {
        _logger = logger;
    }

    public bool IsLoaded => true; // Always "loaded" so we don't block requests, just fail gracefully

    public int TemplateCount => 0;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StubBiometricMatcherService started (Non-Windows mode). Fingerprint matching is disabled.");
        return Task.CompletedTask;
    }

    public (bool Match, Guid? PatientId, int Score) Identify(byte[] featureSetData)
    {
        _logger.LogWarning("Identify called on Stub matcher. Returning no match.");
        return (false, null, 0);
    }

    public Task SyncToRedis(Guid patientId, FingerprintType fingerType, byte[] fingerprintData)
    {
        _logger.LogWarning("SyncToRedis called on Stub matcher. Ignoring.");
        return Task.CompletedTask;
    }
}
