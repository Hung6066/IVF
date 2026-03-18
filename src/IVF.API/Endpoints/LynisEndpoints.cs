using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IVF.API.Endpoints;

public static class LynisEndpoints
{
    private const string LynisPrefix = "system/lynis/";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly Regex HostnamePattern = new(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled);

    public static void MapLynisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/lynis")
            .WithTags("Lynis Security Audit")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/reports", ListReports).WithName("ListLynisReports");
        group.MapGet("/reports/{hostname}/{date}", GetReport).WithName("GetLynisReport");
        group.MapGet("/reports/{hostname}/latest", GetLatestReport).WithName("GetLatestLynisReport");
        group.MapGet("/hosts", ListHosts).WithName("ListLynisHosts");
        group.MapPost("/scan", TriggerScan).WithName("TriggerLynisScan");
        group.MapGet("/scan/{hostname}/status", GetScanStatus).WithName("GetLynisScanStatus");
    }

    // GET /api/admin/lynis/hosts — list all hosts that have Lynis reports
    private static async Task<IResult> ListHosts(
        IObjectStorageService storage,
        CancellationToken ct)
    {
        try
        {
            var objects = await storage.ListObjectsAsync(
                StorageBuckets.Documents,
                prefix: LynisPrefix,
                recursive: false,
                ct: ct);

            var hosts = objects
                .Where(o => o.Key.EndsWith('/'))
                .Select(o => o.Key.TrimEnd('/').Split('/').Last())
                .OrderBy(h => h)
                .ToList();

            return Results.Ok(new { hosts });
        }
        catch (Exception)
        {
            return Results.Ok(new { hosts = Array.Empty<string>() });
        }
    }

    // GET /api/admin/lynis/reports — list all reports across all hosts
    private static async Task<IResult> ListReports(
        IObjectStorageService storage,
        string? hostname,
        CancellationToken ct)
    {
        try
        {
            var prefix = string.IsNullOrEmpty(hostname)
                ? LynisPrefix
                : $"{LynisPrefix}{hostname}/";

            var objects = await storage.ListObjectsAsync(
                StorageBuckets.Documents,
                prefix: prefix,
                recursive: true,
                ct: ct);

            var reports = objects
                .Where(o => o.Key.EndsWith(".json") && !o.Key.EndsWith("latest.json"))
                .OrderByDescending(o => o.LastModified)
                .Select(o =>
                {
                    var parts = o.Key.Split('/');
                    var host = parts.Length >= 4 ? parts[2] : "unknown";
                    var filename = parts.Last();
                    var date = filename.Replace("lynis-", "").Replace(".json", "");
                    return new
                    {
                        hostname = host,
                        date,
                        key = o.Key,
                        size = o.Size,
                        lastModified = o.LastModified
                    };
                })
                .ToList();

            return Results.Ok(new { total = reports.Count, reports });
        }
        catch (Exception ex)
        {
            return Results.Ok(new { total = 0, reports = Array.Empty<object>(), error = ex.Message });
        }
    }

    // GET /api/admin/lynis/reports/{hostname}/{date} — report detail
    private static async Task<IResult> GetReport(
        string hostname,
        string date,
        IObjectStorageService storage,
        CancellationToken ct)
    {
        var key = $"{LynisPrefix}{hostname}/lynis-{date}.json";
        return await FetchReportJson(storage, key, ct);
    }

    // GET /api/admin/lynis/reports/{hostname}/latest — latest report
    private static async Task<IResult> GetLatestReport(
        string hostname,
        IObjectStorageService storage,
        CancellationToken ct)
    {
        var key = $"{LynisPrefix}{hostname}/latest.json";
        return await FetchReportJson(storage, key, ct);
    }

    private static async Task<IResult> FetchReportJson(
        IObjectStorageService storage,
        string objectKey,
        CancellationToken ct)
    {
        try
        {
            var stream = await storage.DownloadAsync(StorageBuckets.Documents, objectKey, ct);
            if (stream is null)
                return Results.NotFound(new { error = "Lynis report not found", key = objectKey });

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync(ct);
            var report = JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);
            return Results.Ok(report);
        }
        catch (Exception ex)
        {
            return Results.NotFound(new { error = ex.Message, key = objectKey });
        }
    }

    // POST /api/admin/lynis/scan — write a trigger file that the VPS polling service detects
    private static async Task<IResult> TriggerScan(
        TriggerScanRequest request,
        IObjectStorageService storage,
        CancellationToken ct)
    {
        var hostname = request?.Hostname?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(hostname))
            return Results.BadRequest(new { error = "Hostname là bắt buộc" });

        if (!HostnamePattern.IsMatch(hostname))
            return Results.BadRequest(new { error = "Hostname không hợp lệ" });

        var triggerKey = $"{LynisPrefix}{hostname}/scan-trigger.json";
        var payload = JsonSerializer.Serialize(new
        {
            requested_at = DateTime.UtcNow.ToString("O"),
            hostname
        });

        var bytes = Encoding.UTF8.GetBytes(payload);
        using var stream = new MemoryStream(bytes);
        await storage.UploadAsync(
            StorageBuckets.Documents, triggerKey, stream,
            "application/json", bytes.Length, ct: ct);

        return Results.Ok(new { message = "Đã gửi yêu cầu quét", hostname, status = "scanning" });
    }

    // GET /api/admin/lynis/scan/{hostname}/status — check whether a scan trigger is pending
    private static async Task<IResult> GetScanStatus(
        string hostname,
        IObjectStorageService storage,
        CancellationToken ct)
    {
        if (!HostnamePattern.IsMatch(hostname))
            return Results.BadRequest(new { error = "Hostname không hợp lệ" });

        var triggerKey = $"{LynisPrefix}{hostname}/scan-trigger.json";
        try
        {
            var stream = await storage.DownloadAsync(StorageBuckets.Documents, triggerKey, ct);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync(ct);
                var trigger = JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);
                return Results.Ok(new { status = "scanning", hostname, trigger });
            }
        }
        catch { /* trigger file not found = idle */ }

        return Results.Ok(new { status = "idle", hostname });
    }
}

internal sealed record TriggerScanRequest(string Hostname);
