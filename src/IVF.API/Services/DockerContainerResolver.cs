using System.Diagnostics;

namespace IVF.API.Services;

/// <summary>
/// Resolves Docker container IDs by service name for Swarm deployments.
/// In Swarm mode, containers are named {stack}_{service}.{slot}.{hash},
/// not the simple names used in docker-compose.
/// </summary>
public static class DockerContainerResolver
{
    /// <summary>
    /// Resolves the container ID for the PostgreSQL primary database.
    /// Tries Swarm naming (ivf_db.1) first, then legacy (ivf-db).
    /// </summary>
    public static Task<string?> ResolveDbContainerAsync(CancellationToken ct = default)
        => ResolveContainerAsync(["ivf_db.1", "ivf-db"], ct);

    /// <summary>
    /// Resolves a container ID by trying multiple name patterns in order.
    /// </summary>
    public static async Task<string?> ResolveContainerAsync(string[] namePatterns, CancellationToken ct = default)
    {
        foreach (var pattern in namePatterns)
        {
            var (exit, output) = await RunCommandAsync(
                $"docker ps -q -f name={pattern} --no-trunc", ct);
            if (exit == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var id = output.Trim().Split('\n')[0].Trim();
                if (id.Length > 0) return id;
            }
        }
        return null;
    }

    public static async Task<(int ExitCode, string Output)> RunCommandAsync(string command, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        var linked = timeoutCts.Token;

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linked);
            var stderrTask = process.StandardError.ReadToEndAsync(linked);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(linked);

            var stdout = stdoutTask.Result;
            var stderr = stderrTask.Result;
            var output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            return (process.ExitCode, output);
        }
        catch
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }
    }
}
