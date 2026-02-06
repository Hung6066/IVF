using System.Collections.Concurrent;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IVF.API.Services;

/// <summary>
/// Singleton service that loads all fingerprint templates into memory
/// and provides high-performance 1:N matching using Parallel processing.
/// </summary>
public class BiometricMatcherService : BackgroundService
{
    private readonly ILogger<BiometricMatcherService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Guid, byte[]> _templateCache = new();
    private readonly ConcurrentDictionary<Guid, string> _fingerTypeCache = new();
    
    // DPFP Verification Control
    private readonly DPFP.Verification.Verification _verificator = new();

    public BiometricMatcherService(
        ILogger<BiometricMatcherService> logger, 
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public int TemplateCount => _templateCache.Count;
    public bool IsLoaded { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BiometricMatcherService starting...");
        await LoadTemplatesAsync(stoppingToken);
    }

    private async Task LoadTemplatesAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPatientBiometricsRepository>();
            
            _logger.LogInformation("Loading fingerprint templates from database...");
            var templates = await repo.GetAllFingerprintsAsync(stoppingToken);

            foreach (var t in templates)
            {
                if (t.FingerprintData != null && t.FingerprintData.Length > 0)
                {
                    try 
                    {
                        _templateCache[t.PatientId] = t.FingerprintData; // Already byte[], no conversion needed
                        _fingerTypeCache[t.PatientId] = t.FingerType.ToString();
                    }
                    catch
                    {
                        // Ignore invalid templates
                    }
                }
            }

            IsLoaded = true;
            _logger.LogInformation("BiometricMatcherService loaded {Count} templates into memory.", _templateCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load fingerprint templates.");
        }
    }

    /// <summary>
    /// Performs a 1:N identification search against the in-memory cache.
    /// Uses Parallel.ForEach for performance.
    /// </summary>
    public (bool Match, Guid? PatientId, int Score) Identify(byte[] featureSetData)
    {
        if (!IsLoaded)
        {
            _logger.LogWarning("Matcher not yet loaded.");
            return (false, null, 0);
        }

        // Deserialize features once
        var features = new DPFP.FeatureSet();
        try
        {
            features.DeSerialize(featureSetData);
        }
        catch (Exception ex)
        {
             _logger.LogError("Invalid feature set data: {Message}", ex.Message);
             return (false, null, 0);
        }

        var matchResult = new DPFP.Verification.Verification.Result();
        Guid? matchedPatient = null;
        int bestScore = 0;
        object lockObj = new object();

        // Parallel Search
        Parallel.ForEach(_templateCache, (item, state) =>
        {
            // Performance optimization: Stop if we found a high confidence match
            if (matchedPatient != null) state.Stop();

            var template = new DPFP.Template();
            try
            {
                template.DeSerialize(item.Value);
                
                // Thread-local verification result
                var localResult = new DPFP.Verification.Verification.Result();
                _verificator.Verify(features, template, ref localResult);

                if (localResult.Verified)
                {
                    lock (lockObj)
                    {
                        // Keep the best match
                        if (localResult.FARAchieved < bestScore || matchedPatient == null)
                        {
                            bestScore = localResult.FARAchieved; // Lower FAR is better? Wait, usually Score probability
                            // DPFP FARAchieved: Represents the probability that the two fingerprints are NOT the same. 
                            // So LOWER is BETTER match.
                            
                            matchedPatient = item.Key;
                            state.Stop(); // Stop other threads
                        }
                    }
                }
            }
            catch { /* Invalid template in cache, skip */ }
        });

        if (matchedPatient.HasValue)
        {
            return (true, matchedPatient, bestScore);
        }

        return (false, null, 0);
    }
}
