using IVF.API.Services;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Waf.Commands;
using IVF.Application.Features.Waf.Queries;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class WafEndpoints
{
    public static void MapWafEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/waf")
            .WithTags("WAF Management")
            .RequireAuthorization("AdminOnly");

        // ─── Application-Level WAF Rules ───
        group.MapGet("/rules", GetWafRules).WithName("GetWafRules");
        group.MapGet("/rules/{id:guid}", GetWafRuleById).WithName("GetWafRuleById");
        group.MapPost("/rules", CreateWafRule).WithName("CreateWafRule");
        group.MapPut("/rules/{id:guid}", UpdateWafRule).WithName("UpdateWafRule");
        group.MapDelete("/rules/{id:guid}", DeleteWafRule).WithName("DeleteWafRule");
        group.MapPut("/rules/{id:guid}/toggle", ToggleWafRule).WithName("ToggleWafRule");

        // ─── WAF Events & Analytics ───
        group.MapGet("/events", GetWafEvents).WithName("GetAppWafEvents");
        group.MapGet("/analytics", GetWafAnalytics).WithName("GetWafAnalytics");
        group.MapPost("/cache/invalidate", InvalidateWafCache).WithName("InvalidateWafCache");

        // ─── Cloudflare Edge WAF (read-only) ───
        group.MapGet("/cloudflare/status", GetCloudflareWafStatus).WithName("GetCloudflareWafStatus");
        group.MapGet("/cloudflare/events", GetCloudflareWafEvents).WithName("GetCloudflareWafEvents");
    }

    // ─── Application WAF Rule CRUD ───

    private static async Task<IResult> GetWafRules(
        IvfDbContext db,
        WafRuleGroup? group,
        CancellationToken ct)
    {
        var query = db.WafRules.AsNoTracking().AsQueryable();

        if (group.HasValue)
            query = query.Where(r => r.RuleGroup == group.Value);

        var rules = await query
            .OrderBy(r => r.Priority)
            .Select(r => MapToDto(r))
            .ToListAsync(ct);

        return Results.Ok(rules);
    }

    private static async Task<IResult> GetWafRuleById(
        Guid id,
        IvfDbContext db,
        CancellationToken ct)
    {
        var rule = await db.WafRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return Results.NotFound(new { error = "WAF rule not found" });

        return Results.Ok(MapToDto(rule));
    }

    private static async Task<IResult> CreateWafRule(
        CreateWafRuleCommand command,
        IvfDbContext db,
        IWafService wafService,
        CancellationToken ct)
    {
        var rule = WafRule.Create(
            command.Name, command.Description, command.Priority,
            command.RuleGroup, command.Action, isManaged: false, command.CreatedBy);

        rule.SetMatchConditions(
            command.UriPathPatterns, command.QueryStringPatterns,
            command.HeaderPatterns, command.BodyPatterns,
            command.Methods, command.IpCidrList,
            command.CountryCodes, command.UserAgentPatterns,
            command.MatchType, command.NegateMatch, command.Expression);

        if (command.Action == WafAction.RateLimit)
            rule.SetAction(command.Action, command.RateLimitRequests, command.RateLimitWindowSeconds, command.BlockResponseMessage);
        else if (command.BlockResponseMessage is not null)
            rule.SetAction(command.Action, blockResponseMessage: command.BlockResponseMessage);

        db.WafRules.Add(rule);
        await db.SaveChangesAsync(ct);
        await wafService.InvalidateCacheAsync(ct);

        return Results.Created($"/api/admin/waf/rules/{rule.Id}", MapToDto(rule));
    }

    private static async Task<IResult> UpdateWafRule(
        Guid id,
        UpdateWafRuleCommand command,
        IvfDbContext db,
        IWafService wafService,
        CancellationToken ct)
    {
        var rule = await db.WafRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return Results.NotFound(new { error = "WAF rule not found" });

        if (rule.IsManaged)
            return Results.BadRequest(new { error = "Managed rules cannot be edited. Use toggle to enable/disable." });

        rule.Update(command.Name, command.Description, command.Priority, command.ModifiedBy);
        rule.SetMatchConditions(
            command.UriPathPatterns, command.QueryStringPatterns,
            command.HeaderPatterns, command.BodyPatterns,
            command.Methods, command.IpCidrList,
            command.CountryCodes, command.UserAgentPatterns,
            command.MatchType, command.NegateMatch, command.Expression);
        rule.SetAction(command.Action, command.RateLimitRequests, command.RateLimitWindowSeconds, command.BlockResponseMessage);

        await db.SaveChangesAsync(ct);
        await wafService.InvalidateCacheAsync(ct);

        return Results.Ok(MapToDto(rule));
    }

    private static async Task<IResult> DeleteWafRule(
        Guid id,
        IvfDbContext db,
        IWafService wafService,
        CancellationToken ct)
    {
        var rule = await db.WafRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return Results.NotFound(new { error = "WAF rule not found" });

        if (rule.IsManaged)
            return Results.BadRequest(new { error = "Managed rules cannot be deleted. Use toggle to disable." });

        rule.MarkAsDeleted();
        await db.SaveChangesAsync(ct);
        await wafService.InvalidateCacheAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> ToggleWafRule(
        Guid id,
        ToggleWafRuleCommand command,
        IvfDbContext db,
        IWafService wafService,
        CancellationToken ct)
    {
        var rule = await db.WafRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return Results.NotFound(new { error = "WAF rule not found" });

        if (command.Enable) rule.Enable(); else rule.Disable();

        await db.SaveChangesAsync(ct);
        await wafService.InvalidateCacheAsync(ct);

        return Results.Ok(new { id = rule.Id, isEnabled = rule.IsEnabled });
    }

    // ─── WAF Events (paged) ───

    private static async Task<IResult> GetWafEvents(
        IvfDbContext db,
        int? page,
        int? pageSize,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? ip,
        WafRuleGroup? ruleGroup,
        WafAction? action,
        CancellationToken ct)
    {
        var p = Math.Max(1, page ?? 1);
        var ps = Math.Clamp(pageSize ?? 50, 1, 200);

        var query = db.WafEvents.AsNoTracking().AsQueryable();

        if (dateFrom.HasValue) query = query.Where(e => e.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(e => e.CreatedAt <= dateTo.Value);
        if (!string.IsNullOrEmpty(ip)) query = query.Where(e => e.ClientIp == ip);
        if (ruleGroup.HasValue) query = query.Where(e => e.RuleGroup == ruleGroup.Value);
        if (action.HasValue) query = query.Where(e => e.Action == action.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(e => new WafEventDto(
                e.Id, e.WafRuleId, e.RuleName, e.RuleGroup.ToString(), e.Action.ToString(),
                e.ClientIp, e.Country, e.RequestPath, e.RequestMethod,
                e.QueryString, e.UserAgent, e.MatchedPattern, e.MatchedValue,
                e.ResponseStatusCode, e.CorrelationId, e.ProcessingTimeMs, e.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new { items, totalCount, page = p, pageSize = ps });
    }

    // ─── WAF Analytics (24h) ───

    private static async Task<IResult> GetWafAnalytics(IvfDbContext db, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddHours(-24);

        var events = await db.WafEvents.AsNoTracking()
            .Where(e => e.CreatedAt >= since)
            .ToListAsync(ct);

        var totalEvents = events.Count;
        var blockedCount = events.Count(e => e.Action == WafAction.Block);
        var challengedCount = events.Count(e => e.Action == WafAction.Challenge);
        var loggedCount = events.Count(e => e.Action == WafAction.Log);
        var rateLimitedCount = events.Count(e => e.Action == WafAction.RateLimit);
        var blockRate = totalEvents > 0 ? (double)blockedCount / totalEvents * 100 : 0;

        var topBlockedIps = events
            .Where(e => e.Action == WafAction.Block)
            .GroupBy(e => e.ClientIp)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new { ip = g.Key, count = g.Count() })
            .ToList();

        var topTriggeredRules = events
            .GroupBy(e => e.RuleName)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new { ruleName = g.Key, count = g.Count() })
            .ToList();

        var hourlyBreakdown = events
            .GroupBy(e => new DateTime(e.CreatedAt.Year, e.CreatedAt.Month, e.CreatedAt.Day, e.CreatedAt.Hour, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new { hour = g.Key, count = g.Count() })
            .ToList();

        return Results.Ok(new
        {
            totalEvents,
            blockedCount,
            challengedCount,
            loggedCount,
            rateLimitedCount,
            blockRate = Math.Round(blockRate, 2),
            topBlockedIps,
            topTriggeredRules,
            hourlyBreakdown
        });
    }

    // ─── Cache Invalidation ───

    private static async Task<IResult> InvalidateWafCache(IWafService wafService, CancellationToken ct)
    {
        await wafService.InvalidateCacheAsync(ct);
        return Results.Ok(new { message = "WAF cache invalidated" });
    }

    // ─── Cloudflare Edge WAF (existing, moved to sub-path) ───

    private static async Task<IResult> GetCloudflareWafStatus(CloudflareWafService wafService, CancellationToken ct)
    {
        var status = await wafService.GetStatusAsync(ct);
        if (!status.Configured)
            return Results.Json(
                new { error = "Cloudflare WAF not configured. Set Cloudflare:ApiToken and Cloudflare:ZoneId." },
                statusCode: StatusCodes.Status503ServiceUnavailable);

        return Results.Ok(status);
    }

    private static async Task<IResult> GetCloudflareWafEvents(
        CloudflareWafService wafService,
        int? limit,
        CancellationToken ct)
    {
        var events = await wafService.GetRecentEventsAsync(limit ?? 50, ct);
        return Results.Ok(events);
    }

    // ─── Mapping helper ───

    private static WafRuleListDto MapToDto(WafRule r) => new(
        r.Id, r.Name, r.Description, r.Priority, r.IsEnabled,
        r.RuleGroup.ToString(), r.IsManaged, r.Action.ToString(),
        r.MatchType.ToString(), r.NegateMatch, r.Expression,
        r.UriPathPatterns, r.QueryStringPatterns, r.HeaderPatterns, r.BodyPatterns,
        r.Methods, r.IpCidrList, r.CountryCodes, r.UserAgentPatterns,
        r.RateLimitRequests, r.RateLimitWindowSeconds, r.BlockResponseMessage,
        r.HitCount, r.CreatedBy, r.LastModifiedBy, r.CreatedAt, r.UpdatedAt);
}
