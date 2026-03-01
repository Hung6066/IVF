using System.Diagnostics.Metrics;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Vault operations metrics using System.Diagnostics.Metrics API.
/// Compatible with OpenTelemetry, Prometheus, and any .NET metrics exporter.
/// Follows Google 4 Golden Signals: latency, traffic, errors, saturation.
/// </summary>
public sealed class VaultMetrics
{
    public static readonly string MeterName = "IVF.Vault";

    private readonly Counter<long> _secretOperations;
    private readonly Counter<long> _encryptionOperations;
    private readonly Counter<long> _tokenValidations;
    private readonly Counter<long> _policyEvaluations;
    private readonly Counter<long> _zeroTrustEvaluations;
    private readonly Counter<long> _rotationOperations;
    private readonly Histogram<double> _encryptionDuration;
    private readonly Histogram<double> _secretAccessDuration;

    // Gauges via ObservableGauge — registered with callbacks
    private long _activeLeases;
    private long _activeDynamicCredentials;
    private long _activeRotationSchedules;
    private long _secretsCount;

    public VaultMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _secretOperations = meter.CreateCounter<long>(
            "vault.secret.operations",
            "operations",
            "Number of vault secret operations");

        _encryptionOperations = meter.CreateCounter<long>(
            "vault.encryption.operations",
            "operations",
            "Number of encryption/decryption operations");

        _tokenValidations = meter.CreateCounter<long>(
            "vault.token.validations",
            "validations",
            "Number of vault token validation attempts");

        _policyEvaluations = meter.CreateCounter<long>(
            "vault.policy.evaluations",
            "evaluations",
            "Number of vault policy evaluations");

        _zeroTrustEvaluations = meter.CreateCounter<long>(
            "vault.zerotrust.evaluations",
            "evaluations",
            "Number of zero trust access evaluations");

        _rotationOperations = meter.CreateCounter<long>(
            "vault.rotation.operations",
            "operations",
            "Number of secret rotation operations");

        _encryptionDuration = meter.CreateHistogram<double>(
            "vault.encryption.duration",
            "ms",
            "Duration of encryption/decryption operations in milliseconds");

        _secretAccessDuration = meter.CreateHistogram<double>(
            "vault.secret.access.duration",
            "ms",
            "Duration of secret access operations in milliseconds");

        meter.CreateObservableGauge(
            "vault.lease.active",
            () => _activeLeases,
            "leases",
            "Number of currently active leases");

        meter.CreateObservableGauge(
            "vault.dynamic_credential.active",
            () => _activeDynamicCredentials,
            "credentials",
            "Number of currently active dynamic credentials");

        meter.CreateObservableGauge(
            "vault.rotation.schedules.active",
            () => _activeRotationSchedules,
            "schedules",
            "Number of active rotation schedules");

        meter.CreateObservableGauge(
            "vault.secrets.total",
            () => _secretsCount,
            "secrets",
            "Total number of vault secrets");
    }

    // ─── Traffic (counters) ───

    public void RecordSecretOperation(string operation, string path)
        => _secretOperations.Add(1, new KeyValuePair<string, object?>("operation", operation),
                                     new KeyValuePair<string, object?>("path_prefix", GetPrefix(path)));

    public void RecordEncryptionOperation(string operation)
        => _encryptionOperations.Add(1, new KeyValuePair<string, object?>("operation", operation));

    public void RecordTokenValidation(string result)
        => _tokenValidations.Add(1, new KeyValuePair<string, object?>("result", result));

    public void RecordPolicyEvaluation(bool allowed)
        => _policyEvaluations.Add(1, new KeyValuePair<string, object?>("result", allowed ? "allow" : "deny"));

    public void RecordZeroTrustEvaluation(bool allowed, string? reason = null)
        => _zeroTrustEvaluations.Add(1,
            new KeyValuePair<string, object?>("result", allowed ? "allow" : "deny"),
            new KeyValuePair<string, object?>("reason", reason ?? "unknown"));

    public void RecordRotation(bool success)
        => _rotationOperations.Add(1, new KeyValuePair<string, object?>("result", success ? "success" : "failure"));

    // ─── Latency (histograms) ───

    public void RecordEncryptionDuration(double milliseconds, string operation)
        => _encryptionDuration.Record(milliseconds, new KeyValuePair<string, object?>("operation", operation));

    public void RecordSecretAccessDuration(double milliseconds, string operation)
        => _secretAccessDuration.Record(milliseconds, new KeyValuePair<string, object?>("operation", operation));

    // ─── Saturation (gauge updates) ───

    public void SetActiveLeases(long count) => _activeLeases = count;
    public void SetActiveDynamicCredentials(long count) => _activeDynamicCredentials = count;
    public void SetActiveRotationSchedules(long count) => _activeRotationSchedules = count;
    public void SetSecretsCount(long count) => _secretsCount = count;

    private static string GetPrefix(string path)
    {
        var idx = path.IndexOf('/');
        return idx > 0 ? path[..idx] : path;
    }
}
