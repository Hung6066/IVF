using System.Collections.Concurrent;
using System.Diagnostics;
using IVF.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IVF.API.Services;

/// <summary>
/// Runs the collect-evidence.ps1 PowerShell script and streams output via SignalR.
/// </summary>
public sealed class EvidenceCollectorService(
    IHubContext<EvidenceHub> hubContext,
    ILogger<EvidenceCollectorService> logger,
    IWebHostEnvironment env)
{
    private string ProjectDir => Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", ".."));
    private string ScriptPath => Path.Combine(ProjectDir, "scripts", "collect-evidence.ps1");

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _running = new();
    private readonly ConcurrentDictionary<string, OperationProgress> _progress = new();

    public record CollectRequest(string[] Categories, bool SkipApi = false);

    public record CollectResult(string OperationId, string Status, int TotalCategories);

    public record OperationProgress(int Completed, int Total, string[] Categories, string? CurrentCategory, string[] CompletedCategories);

    public async Task<CollectResult> StartCollectionAsync(CollectRequest request, string? jwtToken)
    {
        var operationId = $"evidence_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString()[..8]}";

        if (!File.Exists(ScriptPath))
        {
            await SendLog(operationId, "ERROR", $"Script not found: {ScriptPath}");
            return new CollectResult(operationId, "Failed", 0);
        }

        var cts = new CancellationTokenSource();
        _running[operationId] = cts;
        _progress[operationId] = new OperationProgress(0, request.Categories.Length, request.Categories, null, []);

        _ = Task.Run(async () =>
        {
            try
            {
                await SendLog(operationId, "INFO", "═══ Bắt đầu thu thập bằng chứng ═══");
                await SendStatus(operationId, "Running");
                await SendProgress(operationId);

                var args = BuildArguments(request, jwtToken);
                await SendLog(operationId, "INFO", $"Categories: {string.Join(", ", request.Categories)}");
                await SendLog(operationId, "INFO", $"Skip API: {request.SkipApi}");

                var pwsh = FindPowerShell();
                if (pwsh == null)
                {
                    await SendLog(operationId, "ERROR", "PowerShell not found. Install PowerShell 7+ (pwsh).");
                    await SendStatus(operationId, "Failed");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = pwsh,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" {args}",
                    WorkingDirectory = ProjectDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    await SendLog(operationId, "ERROR", "Failed to start PowerShell process");
                    await SendStatus(operationId, "Failed");
                    return;
                }

                var ct = cts.Token;
                ct.Register(() =>
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                    catch { /* ignore */ }
                });

                var stdoutTask = ReadStreamAsync(process.StandardOutput, operationId, ct);
                var stderrTask = ReadStreamAsync(process.StandardError, operationId, ct);

                await Task.WhenAll(stdoutTask, stderrTask);
                await process.WaitForExitAsync(ct);

                if (ct.IsCancellationRequested)
                {
                    await SendLog(operationId, "WARN", "Thu thập bị hủy bởi người dùng");
                    await SendStatus(operationId, "Cancelled");
                }
                else if (process.ExitCode == 0)
                {
                    await SendLog(operationId, "OK", "═══ Thu thập bằng chứng hoàn tất ═══");
                    await SendStatus(operationId, "Completed");
                }
                else
                {
                    await SendLog(operationId, "ERROR", $"Script exited with code {process.ExitCode}");
                    await SendStatus(operationId, "Failed");
                }
            }
            catch (OperationCanceledException)
            {
                await SendLog(operationId, "WARN", "Thu thập bị hủy");
                await SendStatus(operationId, "Cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Evidence collection failed: {OpId}", operationId);
                await SendLog(operationId, "ERROR", $"Lỗi: {ex.Message}");
                await SendStatus(operationId, "Failed");
            }
            finally
            {
                _running.TryRemove(operationId, out _);
                // Keep progress for 5 min after completion for late-joining clients
                _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(t => _progress.TryRemove(operationId, out var removed));
            }
        });

        return new CollectResult(operationId, "Running", request.Categories.Length);
    }

    public bool CancelCollection(string operationId)
    {
        if (_running.TryGetValue(operationId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    public string[] GetRunningOperations() => [.. _running.Keys];

    public OperationProgress? GetProgress(string operationId)
    {
        _progress.TryGetValue(operationId, out var progress);
        return progress;
    }

    private string BuildArguments(CollectRequest request, string? jwtToken)
    {
        var args = new List<string>();

        if (request.Categories.Length > 0)
        {
            var cats = string.Join(",", request.Categories.Select(c => $"\"{c}\""));
            args.Add($"-Categories @({cats})");
        }

        if (request.SkipApi)
        {
            args.Add("-SkipApi");
        }
        else if (!string.IsNullOrEmpty(jwtToken))
        {
            args.Add($"-Token \"{jwtToken}\"");
        }

        return string.Join(" ", args);
    }

    private static string? FindPowerShell()
    {
        // Prefer pwsh (PowerShell 7+), fall back to powershell.exe
        foreach (var name in new[] { "pwsh", "powershell" })
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "-Version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
                if (p?.ExitCode == 0) return name;
            }
            catch { /* not found */ }
        }

        // Try absolute paths
        var paths = new[]
        {
            @"C:\Program Files\PowerShell\7\pwsh.exe",
            @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
        };
        return paths.FirstOrDefault(File.Exists);
    }

    private async Task ReadStreamAsync(StreamReader reader, string operationId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Track progress from script output
            await TrackProgress(operationId, line);

            var level = ParseLevel(line);
            await SendLog(operationId, level, line);
        }
    }

    private static readonly string[] CategoryHeaders =
        ["ACCESS CONTROL", "INCIDENT RESPONSE", "TRAINING", "CHANGE MANAGEMENT", "ENCRYPTION", "BACKUP", "VENDOR", "POLICY VERSIONS"];

    private static readonly Dictionary<string, string> HeaderToCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ACCESS CONTROL"] = "access_control",
        ["INCIDENT RESPONSE"] = "incident_response",
        ["TRAINING"] = "training",
        ["CHANGE MANAGEMENT"] = "change_management",
        ["ENCRYPTION"] = "encryption",
        ["BACKUP"] = "backup",
        ["VENDOR"] = "vendor",
        ["POLICY VERSIONS"] = "policy_versions",
    };

    private async Task TrackProgress(string operationId, string line)
    {
        if (!_progress.TryGetValue(operationId, out var progress)) return;

        // Detect "=== CATEGORY_NAME ===" header → new category started
        foreach (var header in CategoryHeaders)
        {
            if (line.Contains($"=== {header} ===", StringComparison.OrdinalIgnoreCase))
            {
                // If there was a previous current category, mark it as completed
                if (progress.CurrentCategory != null &&
                    !progress.CompletedCategories.Contains(progress.CurrentCategory))
                {
                    var completed = progress.CompletedCategories.Append(progress.CurrentCategory).ToArray();
                    progress = progress with
                    {
                        Completed = completed.Length,
                        CompletedCategories = completed,
                        CurrentCategory = HeaderToCategory.GetValueOrDefault(header, header.ToLower()),
                    };
                }
                else
                {
                    progress = progress with
                    {
                        CurrentCategory = HeaderToCategory.GetValueOrDefault(header, header.ToLower()),
                    };
                }

                _progress[operationId] = progress;
                await SendProgress(operationId);
                return;
            }
        }

        // Detect "evidence collected" → category done
        if (line.Contains("evidence collected", StringComparison.OrdinalIgnoreCase) &&
            progress.CurrentCategory != null &&
            !progress.CompletedCategories.Contains(progress.CurrentCategory))
        {
            var completed = progress.CompletedCategories.Append(progress.CurrentCategory).ToArray();
            progress = progress with
            {
                Completed = completed.Length,
                CompletedCategories = completed,
            };
            _progress[operationId] = progress;
            await SendProgress(operationId);
        }
    }

    private static string ParseLevel(string line)
    {
        if (line.Contains("[+]") || line.Contains("Exported:") || line.Contains("Saved:")) return "OK";
        if (line.Contains("[!]") || line.Contains("WARNING")) return "WARN";
        if (line.Contains("[x]") || line.Contains("ERROR") || line.Contains("Failed:")) return "ERROR";
        if (line.Contains("===") || line.Contains("═══")) return "HEADER";
        return "INFO";
    }

    private async Task SendLog(string operationId, string level, string message)
    {
        try
        {
            await hubContext.Clients.Group(operationId).SendAsync("LogLine", new
            {
                operationId,
                timestamp = DateTime.UtcNow,
                level,
                message
            });
        }
        catch { /* SignalR send failure — non-critical */ }
    }

    private async Task SendStatus(string operationId, string status)
    {
        try
        {
            await hubContext.Clients.Group(operationId).SendAsync("StatusChanged", new
            {
                operationId,
                status,
                timestamp = DateTime.UtcNow
            });
        }
        catch { /* non-critical */ }
    }

    private async Task SendProgress(string operationId)
    {
        if (!_progress.TryGetValue(operationId, out var progress)) return;
        try
        {
            await hubContext.Clients.Group(operationId).SendAsync("ProgressChanged", new
            {
                operationId,
                progress.Completed,
                progress.Total,
                percentage = progress.Total > 0 ? (int)Math.Round(100.0 * progress.Completed / progress.Total) : 0,
                progress.CurrentCategory,
                progress.CompletedCategories,
            });
        }
        catch { /* non-critical */ }
    }
}
