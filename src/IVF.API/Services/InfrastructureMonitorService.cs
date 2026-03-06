using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using IVF.Infrastructure.Persistence;

namespace IVF.API.Services;

/// <summary>
/// Monitors VPS infrastructure: CPU, RAM, disk, Docker Swarm services,
/// PostgreSQL replication, Redis, MinIO health — all from host metrics.
/// </summary>
public sealed class InfrastructureMonitorService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<InfrastructureMonitorService> logger)
{
    // ═══════════════════════════════════════════════════════════
    //  System Metrics (CPU, RAM, Disk)
    // ═══════════════════════════════════════════════════════════

    public async Task<VpsMetricsDto> GetVpsMetricsAsync(CancellationToken ct = default)
    {
        var cpuUsage = await GetCpuUsageAsync(ct);
        var memory = GetMemoryInfo();
        var disks = GetDiskInfo();
        var uptime = GetUptime();
        var hostname = Environment.MachineName;
        var os = RuntimeInformation.OSDescription;
        var cpuCount = Environment.ProcessorCount;

        return new VpsMetricsDto(
            Hostname: hostname,
            Os: os,
            CpuCount: cpuCount,
            CpuUsagePercent: cpuUsage,
            MemoryTotalBytes: memory.Total,
            MemoryUsedBytes: memory.Used,
            MemoryUsagePercent: memory.Total > 0 ? Math.Round((double)memory.Used / memory.Total * 100, 1) : 0,
            Disks: disks,
            UptimeSeconds: uptime,
            CollectedAt: DateTime.UtcNow);
    }

    private async Task<double> GetCpuUsageAsync(CancellationToken ct)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Read /proc/stat twice with 500ms interval
                var stat1 = await File.ReadAllLinesAsync("/proc/stat", ct);
                await Task.Delay(500, ct);
                var stat2 = await File.ReadAllLinesAsync("/proc/stat", ct);

                var cpu1 = ParseCpuLine(stat1[0]);
                var cpu2 = ParseCpuLine(stat2[0]);

                var totalDiff = cpu2.Total - cpu1.Total;
                var idleDiff = cpu2.Idle - cpu1.Idle;

                return totalDiff > 0 ? Math.Round((1.0 - (double)idleDiff / totalDiff) * 100, 1) : 0;
            }

            // Windows fallback — use Process CPU time
            var proc = Process.GetCurrentProcess();
            var startCpu = proc.TotalProcessorTime;
            var startTime = DateTime.UtcNow;
            await Task.Delay(500, ct);
            proc.Refresh();
            var endCpu = proc.TotalProcessorTime;
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var cpuUsed = (endCpu - startCpu).TotalMilliseconds;
            return Math.Round(cpuUsed / (Environment.ProcessorCount * elapsed) * 100, 1);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read CPU usage");
            return -1;
        }
    }

    private static (long Total, long Idle) ParseCpuLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // cpu user nice system idle iowait irq softirq steal
        var values = parts.Skip(1).Select(long.Parse).ToArray();
        var total = values.Sum();
        var idle = values.Length > 3 ? values[3] : 0;
        return (total, idle);
    }

    private static (long Total, long Used) GetMemoryInfo()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                long totalKb = 0, availableKb = 0;
                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:"))
                        totalKb = ParseMemValue(line);
                    else if (line.StartsWith("MemAvailable:"))
                        availableKb = ParseMemValue(line);
                }
                return (totalKb * 1024, (totalKb - availableKb) * 1024);
            }

            // Windows/other — use GC info + Process
            var proc = Process.GetCurrentProcess();
            var gcInfo = GC.GetGCMemoryInfo();
            return (gcInfo.TotalAvailableMemoryBytes, proc.WorkingSet64);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static long ParseMemValue(string line)
    {
        var parts = line.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return 0;
        var numStr = parts[1].Replace("kB", "").Trim();
        return long.TryParse(numStr, out var val) ? val : 0;
    }

    private static List<DiskInfoDto> GetDiskInfo()
    {
        var result = new List<DiskInfoDto>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType is DriveType.Ram or DriveType.Unknown) continue;
                // Skip tiny/virtual filesystems
                if (drive.TotalSize < 100_000_000) continue;

                result.Add(new DiskInfoDto(
                    MountPoint: drive.Name,
                    TotalBytes: drive.TotalSize,
                    UsedBytes: drive.TotalSize - drive.AvailableFreeSpace,
                    UsagePercent: Math.Round((double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100, 1),
                    FileSystem: drive.DriveFormat));
            }
        }
        catch { /* ignore */ }
        return result;
    }

    private static long GetUptime()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists("/proc/uptime"))
            {
                var content = File.ReadAllText("/proc/uptime").Trim();
                var upSec = content.Split(' ')[0];
                return (long)double.Parse(upSec);
            }
            return (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
        }
        catch { return 0; }
    }

    // ═══════════════════════════════════════════════════════════
    //  Docker Service Info (reads from Docker socket)
    // ═══════════════════════════════════════════════════════════

    // Cache whether this node is a Swarm manager (checked once, valid for service lifetime)
    private bool? _isSwarmManager;

    private async Task<bool> IsSwarmManagerAsync(CancellationToken ct)
    {
        if (_isSwarmManager.HasValue) return _isSwarmManager.Value;

        var result = await RunCommandAsync("docker", "info --format \"{{.Swarm.ControlAvailable}}\"", ct);
        _isSwarmManager = result.ExitCode == 0 && result.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

        if (!_isSwarmManager.Value)
            logger.LogInformation("This node is not a Swarm manager — Swarm management queries will be skipped");

        return _isSwarmManager.Value;
    }

    public async Task<List<SwarmServiceDto>> GetSwarmServicesAsync(CancellationToken ct = default)
    {
        try
        {
            if (!await IsSwarmManagerAsync(ct)) return [];

            // Use docker CLI to list services (works without Docker SDK dependency)
            var result = await RunCommandAsync(
                "docker", "service ls --format \"{{.ID}}|{{.Name}}|{{.Mode}}|{{.Replicas}}|{{.Image}}|{{.Ports}}\"", ct);

            if (result.ExitCode != 0)
            {
                logger.LogWarning("docker service ls failed: {Error}", result.Error);
                return [];
            }

            var services = new List<SwarmServiceDto>();
            foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 5) continue;

                var replicas = parts[3]; // "2/2" or "1/1"
                var replicaParts = replicas.Split('/');
                int.TryParse(replicaParts[0], out var running);
                int.TryParse(replicaParts.Length > 1 ? replicaParts[1] : "0", out var desired);

                services.Add(new SwarmServiceDto(
                    Id: parts[0].Trim(),
                    Name: parts[1].Trim(),
                    Mode: parts[2].Trim(),
                    RunningReplicas: running,
                    DesiredReplicas: desired,
                    Image: parts[3 + 1].Trim(),
                    Ports: parts.Length > 5 ? parts[5].Trim() : "",
                    Status: running >= desired ? "healthy" : running > 0 ? "degraded" : "down"));
            }
            return services;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get Swarm services");
            return [];
        }
    }

    public async Task<SwarmNodeDto[]> GetSwarmNodesAsync(CancellationToken ct = default)
    {
        try
        {
            if (!await IsSwarmManagerAsync(ct)) return [];

            var result = await RunCommandAsync(
                "docker", "node ls --format \"{{.ID}}|{{.Hostname}}|{{.Status}}|{{.Availability}}|{{.ManagerStatus}}|{{.EngineVersion}}\"", ct);

            if (result.ExitCode != 0) return [];

            return result.Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                {
                    var p = line.Split('|');
                    return new SwarmNodeDto(
                        Id: p[0].Trim().TrimEnd('*', ' '),
                        Hostname: p.Length > 1 ? p[1].Trim() : "",
                        Status: p.Length > 2 ? p[2].Trim() : "",
                        Availability: p.Length > 3 ? p[3].Trim() : "",
                        ManagerStatus: p.Length > 4 ? p[4].Trim() : "",
                        EngineVersion: p.Length > 5 ? p[5].Trim() : "");
                }).ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get Swarm nodes");
            return [];
        }
    }

    public async Task<ServiceScaleResult> ScaleServiceAsync(string serviceName, int replicas, CancellationToken ct = default)
    {
        if (replicas < 0 || replicas > 10)
            return new ServiceScaleResult(false, "Replicas phải từ 0 đến 10");

        // Validate service name (prevent command injection)
        if (!System.Text.RegularExpressions.Regex.IsMatch(serviceName, @"^[a-zA-Z0-9_\-]+$"))
            return new ServiceScaleResult(false, "Tên service không hợp lệ");

        try
        {
            var result = await RunCommandAsync("docker", $"service scale {serviceName}={replicas}", ct);
            return result.ExitCode == 0
                ? new ServiceScaleResult(true, $"Đã scale {serviceName} → {replicas} replicas")
                : new ServiceScaleResult(false, result.Error);
        }
        catch (Exception ex)
        {
            return new ServiceScaleResult(false, ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Node Management
    // ═══════════════════════════════════════════════════════════

    private static readonly System.Text.RegularExpressions.Regex SafeNameRegex =
        new(@"^[a-zA-Z0-9_\-\.]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Drain a node (migrate all tasks away), Pause, or re-Activate.</summary>
    public async Task<ServiceScaleResult> SetNodeAvailabilityAsync(string nodeId, string availability, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(nodeId))
            return new ServiceScaleResult(false, "Node ID không hợp lệ");

        availability = availability.ToLowerInvariant();
        if (availability is not ("active" or "drain" or "pause"))
            return new ServiceScaleResult(false, "Availability phải là active, drain, hoặc pause");

        var result = await RunCommandAsync("docker", $"node update --availability {availability} {nodeId}", ct);
        return result.ExitCode == 0
            ? new ServiceScaleResult(true, $"Node {nodeId} → {availability}")
            : new ServiceScaleResult(false, result.Error);
    }

    /// <summary>Promote a worker node to manager.</summary>
    public async Task<ServiceScaleResult> PromoteNodeAsync(string nodeId, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(nodeId))
            return new ServiceScaleResult(false, "Node ID không hợp lệ");

        var result = await RunCommandAsync("docker", $"node promote {nodeId}", ct);
        return result.ExitCode == 0
            ? new ServiceScaleResult(true, $"Node {nodeId} đã promote lên Manager")
            : new ServiceScaleResult(false, result.Error);
    }

    /// <summary>Demote a manager node to worker.</summary>
    public async Task<ServiceScaleResult> DemoteNodeAsync(string nodeId, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(nodeId))
            return new ServiceScaleResult(false, "Node ID không hợp lệ");

        var result = await RunCommandAsync("docker", $"node demote {nodeId}", ct);
        return result.ExitCode == 0
            ? new ServiceScaleResult(true, $"Node {nodeId} đã demote xuống Worker")
            : new ServiceScaleResult(false, result.Error);
    }

    /// <summary>Remove a drained/down node from the swarm.</summary>
    public async Task<ServiceScaleResult> RemoveNodeAsync(string nodeId, bool force, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(nodeId))
            return new ServiceScaleResult(false, "Node ID không hợp lệ");

        var args = force ? $"node rm --force {nodeId}" : $"node rm {nodeId}";
        var result = await RunCommandAsync("docker", args, ct);
        return result.ExitCode == 0
            ? new ServiceScaleResult(true, $"Đã xoá node {nodeId}")
            : new ServiceScaleResult(false, result.Error);
    }

    /// <summary>Add or update a label on a node (placement constraint).</summary>
    public async Task<ServiceScaleResult> SetNodeLabelAsync(string nodeId, string key, string value, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(nodeId) || !SafeNameRegex.IsMatch(key))
            return new ServiceScaleResult(false, "Node ID hoặc label key không hợp lệ");
        if (!SafeNameRegex.IsMatch(value))
            return new ServiceScaleResult(false, "Label value không hợp lệ");

        var result = await RunCommandAsync("docker", $"node update --label-add {key}={value} {nodeId}", ct);
        return result.ExitCode == 0
            ? new ServiceScaleResult(true, $"Node {nodeId}: {key}={value}")
            : new ServiceScaleResult(false, result.Error);
    }

    /// <summary>Remove a label from a node.</summary>
    public async Task<ServiceScaleResult> RemoveNodeLabelAsync(string nodeId, string key, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(nodeId) || !SafeNameRegex.IsMatch(key))
            return new ServiceScaleResult(false, "Node ID hoặc label key không hợp lệ");

        var result = await RunCommandAsync("docker", $"node update --label-rm {key} {nodeId}", ct);
        return result.ExitCode == 0
            ? new ServiceScaleResult(true, $"Đã xoá label {key} khỏi node {nodeId}")
            : new ServiceScaleResult(false, result.Error);
    }

    // ═══════════════════════════════════════════════════════════
    //  Service Update & Rollback
    // ═══════════════════════════════════════════════════════════

    /// <summary>Rolling update a service to a new image tag.</summary>
    public async Task<ServiceScaleResult> UpdateServiceImageAsync(string serviceName, string newImage, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(serviceName))
            return new ServiceScaleResult(false, "Tên service không hợp lệ");
        // Image format: repo:tag or repo/name:tag
        if (!System.Text.RegularExpressions.Regex.IsMatch(newImage, @"^[a-zA-Z0-9_\-\./:]+$"))
            return new ServiceScaleResult(false, "Image name không hợp lệ");

        var result = await RunCommandAsync("docker",
            $"service update --image {newImage} --update-parallelism 1 --update-delay 10s --update-failure-action rollback {serviceName}", ct);
        return result.ExitCode == 0
            ? new ServiceScaleResult(true, $"Đang update {serviceName} → {newImage}")
            : new ServiceScaleResult(false, result.Error);
    }

    /// <summary>Rollback a service to its previous configuration.</summary>
    public async Task<ServiceScaleResult> RollbackServiceAsync(string serviceName, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(serviceName))
            return new ServiceScaleResult(false, "Tên service không hợp lệ");

        var result = await RunCommandAsync("docker", $"service rollback {serviceName}", ct);
        return result.ExitCode == 0
            ? new ServiceScaleResult(true, $"Đang rollback {serviceName}")
            : new ServiceScaleResult(false, result.Error);
    }

    /// <summary>Force restart all replicas (re-pull image, restart containers).</summary>
    public async Task<ServiceScaleResult> ForceUpdateServiceAsync(string serviceName, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(serviceName))
            return new ServiceScaleResult(false, "Tên service không hợp lệ");

        var result = await RunCommandAsync("docker", $"service update --force {serviceName}", ct);
        return result.ExitCode == 0
            ? new ServiceScaleResult(true, $"Force restart {serviceName}")
            : new ServiceScaleResult(false, result.Error);
    }

    /// <summary>Get service tasks (container replicas) with state info.</summary>
    public async Task<List<ServiceTaskDto>> GetServiceTasksAsync(string serviceName, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(serviceName))
            return [];

        var result = await RunCommandAsync("docker",
            $"service ps {serviceName} --format \"{{{{.ID}}}}|{{{{.Name}}}}|{{{{.Node}}}}|{{{{.DesiredState}}}}|{{{{.CurrentState}}}}|{{{{.Error}}}}|{{{{.Ports}}}}\" --no-trunc", ct);

        if (result.ExitCode != 0) return [];

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                var p = line.Split('|');
                return new ServiceTaskDto(
                    Id: p[0].Trim(),
                    Name: p.Length > 1 ? p[1].Trim() : "",
                    Node: p.Length > 2 ? p[2].Trim() : "",
                    DesiredState: p.Length > 3 ? p[3].Trim() : "",
                    CurrentState: p.Length > 4 ? p[4].Trim() : "",
                    Error: p.Length > 5 ? p[5].Trim() : "",
                    Ports: p.Length > 6 ? p[6].Trim() : "");
            })
            .ToList();
    }

    /// <summary>Get recent logs from a service (last 100 lines).</summary>
    public async Task<ServiceLogsDto> GetServiceLogsAsync(string serviceName, int tailLines = 100, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(serviceName))
            return new ServiceLogsDto(serviceName, [], DateTime.UtcNow);

        if (tailLines is < 10 or > 1000)
            tailLines = 100;

        var result = await RunCommandAsync("docker",
            $"service logs --tail {tailLines} --timestamps --no-task-ids {serviceName}", ct);

        var lines = result.ExitCode == 0
            ? result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList()
            : [result.Error];

        return new ServiceLogsDto(serviceName, lines, DateTime.UtcNow);
    }

    /// <summary>Get detailed service inspect info (env, constraints, update config).</summary>
    public async Task<ServiceInspectDto?> InspectServiceAsync(string serviceName, CancellationToken ct = default)
    {
        if (!SafeNameRegex.IsMatch(serviceName))
            return null;

        var result = await RunCommandAsync("docker",
            $"service inspect {serviceName} --format \"{{{{.Spec.TaskTemplate.ContainerSpec.Image}}}}|{{{{.Spec.Mode.Replicated.Replicas}}}}|{{{{.Spec.UpdateConfig.Parallelism}}}}|{{{{.Spec.UpdateConfig.Delay}}}}|{{{{.Spec.UpdateConfig.FailureAction}}}}|{{{{.Spec.RollbackConfig.Parallelism}}}}|{{{{.Spec.TaskTemplate.Placement.Constraints}}}}|{{{{.CreatedAt}}}}|{{{{.UpdatedAt}}}}|{{{{.UpdateStatus.State}}}}|{{{{.UpdateStatus.Message}}}}\"", ct);

        if (result.ExitCode != 0) return null;

        var p = result.Output.Split('|');
        return new ServiceInspectDto(
            Image: p[0].Trim(),
            DesiredReplicas: int.TryParse(p.Length > 1 ? p[1].Trim() : "", out var r) ? r : 0,
            UpdateParallelism: int.TryParse(p.Length > 2 ? p[2].Trim() : "", out var up) ? up : 1,
            UpdateDelay: p.Length > 3 ? p[3].Trim() : "",
            FailureAction: p.Length > 4 ? p[4].Trim() : "pause",
            RollbackParallelism: int.TryParse(p.Length > 5 ? p[5].Trim() : "", out var rp) ? rp : 1,
            Constraints: p.Length > 6 ? p[6].Trim() : "",
            CreatedAt: p.Length > 7 ? p[7].Trim() : "",
            UpdatedAt: p.Length > 8 ? p[8].Trim() : "",
            UpdateState: p.Length > 9 ? p[9].Trim() : "",
            UpdateMessage: p.Length > 10 ? p[10].Trim() : "");
    }

    // ═══════════════════════════════════════════════════════════
    //  Swarm Events
    // ═══════════════════════════════════════════════════════════

    /// <summary>Get recent Swarm events (last N minutes).</summary>
    public async Task<List<SwarmEventDto>> GetRecentEventsAsync(int sinceMinutes = 15, CancellationToken ct = default)
    {
        if (sinceMinutes is < 1 or > 60) sinceMinutes = 15;

        var result = await RunCommandAsync("docker",
            $"events --since {sinceMinutes}m --until 0s --filter type=service --filter type=node --format \"{{{{.Time}}}}|{{{{.Type}}}}|{{{{.Action}}}}|{{{{.Actor.Attributes.name}}}}|{{{{.Actor.ID}}}}\"", ct);

        if (result.ExitCode != 0) return [];

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                var p = line.Split('|');
                return new SwarmEventDto(
                    Timestamp: p[0].Trim(),
                    Type: p.Length > 1 ? p[1].Trim() : "",
                    Action: p.Length > 2 ? p[2].Trim() : "",
                    Name: p.Length > 3 ? p[3].Trim() : "",
                    ActorId: p.Length > 4 ? p[4].Trim() : "");
            })
            .ToList();
    }

    // ═══════════════════════════════════════════════════════════
    //  Service Health Checks
    // ═══════════════════════════════════════════════════════════

    public async Task<InfraHealthDto> GetHealthStatusAsync(CancellationToken ct = default)
    {
        var checks = new List<HealthCheckItemDto>();

        // 1. PostgreSQL
        checks.Add(await CheckPostgresAsync(ct));

        // 2. Redis
        checks.Add(await CheckRedisAsync(ct));

        // 3. MinIO
        checks.Add(await CheckMinioAsync(ct));

        // 4. EJBCA
        checks.Add(await CheckHttpServiceAsync("EJBCA", "https://ejbca:8443/ejbca/publicweb/healthcheck/ejbcahealth", ct));

        // 5. SignServer
        checks.Add(await CheckHttpServiceAsync("SignServer", "https://signserver:8443/signserver/healthcheck/signserverhealth", ct));

        // When digital signing is disabled, EJBCA/SignServer failures don't affect overall status
        var signingEnabled = configuration.GetValue<bool>("DigitalSigning:Enabled");
        var overallStatus = checks
            .Where(c => signingEnabled || (c.Name != "EJBCA" && c.Name != "SignServer"))
            .All(c => c.Status == "healthy") ? "healthy"
            : checks.Any(c => c.Status == "down" && (signingEnabled || (c.Name != "EJBCA" && c.Name != "SignServer"))) ? "critical"
            : "degraded";

        return new InfraHealthDto(overallStatus, checks, DateTime.UtcNow);
    }

    private async Task<HealthCheckItemDto> CheckPostgresAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
            var sw = Stopwatch.StartNew();
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            sw.Stop();
            return new HealthCheckItemDto("PostgreSQL", "healthy", $"{sw.ElapsedMilliseconds}ms", null);
        }
        catch (Exception ex)
        {
            return new HealthCheckItemDto("PostgreSQL", "down", null, ex.Message);
        }
    }

    private async Task<HealthCheckItemDto> CheckRedisAsync(CancellationToken ct)
    {
        try
        {
            var redisConnStr = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            using var client = new System.Net.Sockets.TcpClient();
            var sw = Stopwatch.StartNew();
            var connectTask = client.ConnectAsync(redisConnStr.Split(':')[0], 6379, ct).AsTask();
            var completed = await Task.WhenAny(connectTask, Task.Delay(3000, ct));
            sw.Stop();
            if (completed == connectTask && client.Connected)
                return new HealthCheckItemDto("Redis", "healthy", $"{sw.ElapsedMilliseconds}ms", null);
            return new HealthCheckItemDto("Redis", "down", null, "Connection timeout");
        }
        catch (Exception ex)
        {
            return new HealthCheckItemDto("Redis", "down", null, ex.Message);
        }
    }

    private async Task<HealthCheckItemDto> CheckMinioAsync(CancellationToken ct)
    {
        try
        {
            var endpoint = configuration["MinIO:Endpoint"] ?? "localhost:9000";
            var useSsl = configuration.GetValue<bool>("MinIO:UseSSL");
            var scheme = useSsl ? "https" : "http";
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var sw = Stopwatch.StartNew();
            var response = await httpClient.GetAsync($"{scheme}://{endpoint}/minio/health/live", ct);
            sw.Stop();
            return response.IsSuccessStatusCode
                ? new HealthCheckItemDto("MinIO", "healthy", $"{sw.ElapsedMilliseconds}ms", null)
                : new HealthCheckItemDto("MinIO", "degraded", $"{sw.ElapsedMilliseconds}ms", $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return new HealthCheckItemDto("MinIO", "down", null, ex.Message);
        }
    }

    private static async Task<HealthCheckItemDto> CheckHttpServiceAsync(string name, string url, CancellationToken ct)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true // Internal services, self-signed
            };
            using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            var sw = Stopwatch.StartNew();
            var response = await httpClient.GetAsync(url, ct);
            sw.Stop();
            return response.IsSuccessStatusCode
                ? new HealthCheckItemDto(name, "healthy", $"{sw.ElapsedMilliseconds}ms", null)
                : new HealthCheckItemDto(name, "degraded", $"{sw.ElapsedMilliseconds}ms", $"HTTP {(int)response.StatusCode}");
        }
        catch
        {
            return new HealthCheckItemDto(name, "down", null, "Connection refused");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Alert Thresholds
    // ═══════════════════════════════════════════════════════════

    public async Task<List<AlertDto>> EvaluateAlertsAsync(CancellationToken ct = default)
    {
        var alerts = new List<AlertDto>();
        var metrics = await GetVpsMetricsAsync(ct);

        // CPU alert
        if (metrics.CpuUsagePercent > 90)
            alerts.Add(new AlertDto("critical", "CPU", $"CPU đang ở {metrics.CpuUsagePercent}% — quá tải", DateTime.UtcNow));
        else if (metrics.CpuUsagePercent > 75)
            alerts.Add(new AlertDto("warning", "CPU", $"CPU đang ở {metrics.CpuUsagePercent}%", DateTime.UtcNow));

        // RAM alert
        if (metrics.MemoryUsagePercent > 90)
            alerts.Add(new AlertDto("critical", "RAM", $"RAM đang ở {metrics.MemoryUsagePercent}% ({FormatBytes(metrics.MemoryUsedBytes)}/{FormatBytes(metrics.MemoryTotalBytes)})", DateTime.UtcNow));
        else if (metrics.MemoryUsagePercent > 80)
            alerts.Add(new AlertDto("warning", "RAM", $"RAM đang ở {metrics.MemoryUsagePercent}%", DateTime.UtcNow));

        // Disk alert
        foreach (var disk in metrics.Disks)
        {
            if (disk.UsagePercent > 90)
                alerts.Add(new AlertDto("critical", "Disk", $"Disk {disk.MountPoint} đang ở {disk.UsagePercent}%", DateTime.UtcNow));
            else if (disk.UsagePercent > 80)
                alerts.Add(new AlertDto("warning", "Disk", $"Disk {disk.MountPoint} đang ở {disk.UsagePercent}%", DateTime.UtcNow));
        }

        // Service health alerts
        var health = await GetHealthStatusAsync(ct);
        foreach (var check in health.Checks)
        {
            if (check.Status == "down")
                alerts.Add(new AlertDto("critical", check.Name, $"{check.Name} không hoạt động: {check.Error}", DateTime.UtcNow));
            else if (check.Status == "degraded")
                alerts.Add(new AlertDto("warning", check.Name, $"{check.Name} hoạt động chậm: {check.Error}", DateTime.UtcNow));
        }

        return alerts;
    }

    // ═══════════════════════════════════════════════════════════
    //  S3 Backup Management
    // ═══════════════════════════════════════════════════════════

    public async Task<S3StatusDto> GetS3StatusAsync(CancellationToken ct = default)
    {
        try
        {
            var factory = scopeFactory.CreateScope().ServiceProvider
                .GetRequiredService<CloudBackupProviderFactory>();
            var provider = await factory.GetProviderAsync(ct);

            var connected = await provider.TestConnectionAsync(ct);
            if (!connected)
                return new S3StatusDto(false, provider.ProviderName, [], 0, 0, null);

            var objects = await provider.ListAsync(ct);

            var totalSize = objects.Sum(o => o.SizeBytes);
            var latestBackup = objects.OrderByDescending(o => o.LastModified).FirstOrDefault();

            var buckets = objects
                .GroupBy(o => o.ObjectKey.Split('/').FirstOrDefault() ?? "root")
                .Select(g => new S3BucketSummaryDto(
                    Prefix: g.Key,
                    ObjectCount: g.Count(),
                    TotalSizeBytes: g.Sum(o => o.SizeBytes),
                    LatestModified: g.Max(o => o.LastModified)))
                .OrderByDescending(b => b.TotalSizeBytes)
                .ToList();

            return new S3StatusDto(
                Connected: true,
                ProviderName: provider.ProviderName,
                Buckets: buckets,
                TotalObjects: objects.Count,
                TotalSizeBytes: totalSize,
                LatestBackupAt: latestBackup?.LastModified);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get S3 status");
            return new S3StatusDto(false, "Unknown", [], 0, 0, null);
        }
    }

    public async Task<List<S3ObjectDto>> ListS3BackupsAsync(string? prefix, CancellationToken ct = default)
    {
        try
        {
            var factory = scopeFactory.CreateScope().ServiceProvider
                .GetRequiredService<CloudBackupProviderFactory>();
            var provider = await factory.GetProviderAsync(ct);
            var objects = await provider.ListAsync(ct);

            var filtered = string.IsNullOrEmpty(prefix)
                ? objects
                : objects.Where(o => o.ObjectKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

            return filtered.Select(o => new S3ObjectDto(
                Key: o.ObjectKey,
                FileName: o.FileName,
                SizeBytes: o.SizeBytes,
                LastModified: o.LastModified,
                ETag: o.ETag)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list S3 backups");
            return [];
        }
    }

    public async Task<S3UploadResultDto> UploadBackupToS3Async(string localFileName, CancellationToken ct = default)
    {
        try
        {
            var factory = scopeFactory.CreateScope().ServiceProvider
                .GetRequiredService<CloudBackupProviderFactory>();
            var provider = await factory.GetProviderAsync(ct);

            // Build local path safely
            var backupsDir = Path.Combine(
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")), "backups");

            // Validate filename to prevent path traversal
            if (Path.GetFileName(localFileName) != localFileName)
                return new S3UploadResultDto(false, null, "Tên file không hợp lệ");

            var localPath = Path.Combine(backupsDir, localFileName);
            if (!File.Exists(localPath))
                return new S3UploadResultDto(false, null, $"File không tồn tại: {localFileName}");

            var objectKey = $"daily/{localFileName}";
            var result = await provider.UploadAsync(localPath, objectKey, ct);

            return new S3UploadResultDto(true, result.ObjectKey, $"Đã upload {FormatBytes(result.SizeBytes)}");
        }
        catch (Exception ex)
        {
            return new S3UploadResultDto(false, null, ex.Message);
        }
    }

    public async Task<S3DownloadResultDto> DownloadFromS3Async(string objectKey, CancellationToken ct = default)
    {
        try
        {
            var factory = scopeFactory.CreateScope().ServiceProvider
                .GetRequiredService<CloudBackupProviderFactory>();
            var provider = await factory.GetProviderAsync(ct);

            var downloadDir = Path.Combine(
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")), "backups");

            var localPath = await provider.DownloadAsync(objectKey, downloadDir, ct);
            var fileInfo = new FileInfo(localPath);

            return new S3DownloadResultDto(true, fileInfo.Name, fileInfo.Length, "Tải về thành công");
        }
        catch (Exception ex)
        {
            return new S3DownloadResultDto(false, null, 0, ex.Message);
        }
    }

    public async Task<bool> DeleteS3ObjectAsync(string objectKey, CancellationToken ct = default)
    {
        try
        {
            var factory = scopeFactory.CreateScope().ServiceProvider
                .GetRequiredService<CloudBackupProviderFactory>();
            var provider = await factory.GetProviderAsync(ct);
            return await provider.DeleteAsync(objectKey, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete S3 object {Key}", objectKey);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private static async Task<(int ExitCode, string Output, string Error)> RunCommandAsync(
        string command, string arguments, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return (process.ExitCode, output.TrimEnd(), error.TrimEnd());
        }
        catch (Exception ex)
        {
            return (1, "", ex.Message);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

// ═══════════════════════════════════════════════════════════════
//  DTOs
// ═══════════════════════════════════════════════════════════════

public sealed record VpsMetricsDto(
    string Hostname,
    string Os,
    int CpuCount,
    double CpuUsagePercent,
    long MemoryTotalBytes,
    long MemoryUsedBytes,
    double MemoryUsagePercent,
    List<DiskInfoDto> Disks,
    long UptimeSeconds,
    DateTime CollectedAt);

public sealed record DiskInfoDto(
    string MountPoint,
    long TotalBytes,
    long UsedBytes,
    double UsagePercent,
    string FileSystem);

public sealed record SwarmServiceDto(
    string Id,
    string Name,
    string Mode,
    int RunningReplicas,
    int DesiredReplicas,
    string Image,
    string Ports,
    string Status);

public sealed record SwarmNodeDto(
    string Id,
    string Hostname,
    string Status,
    string Availability,
    string ManagerStatus,
    string EngineVersion);

public sealed record InfraHealthDto(
    string OverallStatus,
    List<HealthCheckItemDto> Checks,
    DateTime CheckedAt);

public sealed record HealthCheckItemDto(
    string Name,
    string Status,
    string? ResponseTime,
    string? Error);

public sealed record AlertDto(
    string Level,
    string Source,
    string Message,
    DateTime Timestamp);

public sealed record ServiceScaleResult(bool Success, string Message);

// S3 DTOs
public sealed record S3StatusDto(
    bool Connected,
    string ProviderName,
    List<S3BucketSummaryDto> Buckets,
    int TotalObjects,
    long TotalSizeBytes,
    DateTime? LatestBackupAt);

public sealed record S3BucketSummaryDto(
    string Prefix,
    int ObjectCount,
    long TotalSizeBytes,
    DateTime LatestModified);

public sealed record S3ObjectDto(
    string Key,
    string FileName,
    long SizeBytes,
    DateTime LastModified,
    string? ETag);

public sealed record S3UploadResultDto(bool Success, string? ObjectKey, string Message);
public sealed record S3DownloadResultDto(bool Success, string? FileName, long SizeBytes, string Message);

// Swarm Task/Container DTOs
public sealed record ServiceTaskDto(
    string Id,
    string Name,
    string Node,
    string DesiredState,
    string CurrentState,
    string Error,
    string Ports);

public sealed record ServiceLogsDto(
    string ServiceName,
    List<string> Lines,
    DateTime FetchedAt);

public sealed record ServiceInspectDto(
    string Image,
    int DesiredReplicas,
    int UpdateParallelism,
    string UpdateDelay,
    string FailureAction,
    int RollbackParallelism,
    string Constraints,
    string CreatedAt,
    string UpdatedAt,
    string UpdateState,
    string UpdateMessage);

public sealed record SwarmEventDto(
    string Timestamp,
    string Type,
    string Action,
    string Name,
    string ActorId);

public sealed record NodeAvailabilityRequest(string NodeId, string Availability);
public sealed record NodeLabelRequest(string NodeId, string Key, string Value);
public sealed record ServiceUpdateImageRequest(string ServiceName, string NewImage);
public sealed record ServiceNameRequest(string ServiceName);
