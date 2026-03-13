using IVF.Application.Common;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Waf.Queries;

// ═══════════════════════════════════════════════════════════════════════════════════════
// GET WAF RULES QUERY
// ═══════════════════════════════════════════════════════════════════════════════════════

public record GetWafRulesQuery(WafRuleGroup? Group = null) : IRequest<List<WafRuleListDto>>;

// ═══════════════════════════════════════════════════════════════════════════════════════
// GET WAF RULE BY ID QUERY
// ═══════════════════════════════════════════════════════════════════════════════════════

public record GetWafRuleByIdQuery(Guid Id) : IRequest<WafRuleListDto?>;

// ═══════════════════════════════════════════════════════════════════════════════════════
// GET WAF EVENTS QUERY (paged)
// ═══════════════════════════════════════════════════════════════════════════════════════

public record GetWafEventsQuery(
    int Page = 1,
    int PageSize = 50,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? Ip = null,
    WafRuleGroup? RuleGroup = null,
    WafAction? Action = null
) : IRequest<PagedResult<WafEventDto>>;

// ═══════════════════════════════════════════════════════════════════════════════════════
// GET WAF ANALYTICS QUERY
// ═══════════════════════════════════════════════════════════════════════════════════════

public record GetWafAnalyticsQuery : IRequest<WafAnalyticsDto>;

// ═══════════════════════════════════════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════════════════════════════════════

public record WafRuleListDto(
    Guid Id,
    string Name,
    string? Description,
    int Priority,
    bool IsEnabled,
    string RuleGroup,
    bool IsManaged,
    string Action,
    string MatchType,
    bool NegateMatch,
    string? Expression,
    List<string>? UriPathPatterns,
    List<string>? QueryStringPatterns,
    List<string>? HeaderPatterns,
    List<string>? BodyPatterns,
    List<string>? Methods,
    List<string>? IpCidrList,
    List<string>? CountryCodes,
    List<string>? UserAgentPatterns,
    int? RateLimitRequests,
    int? RateLimitWindowSeconds,
    string? BlockResponseMessage,
    long HitCount,
    string? CreatedBy,
    string? LastModifiedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record WafEventDto(
    Guid Id,
    Guid? WafRuleId,
    string RuleName,
    string RuleGroup,
    string Action,
    string ClientIp,
    string? Country,
    string RequestPath,
    string RequestMethod,
    string? QueryString,
    string? UserAgent,
    string? MatchedPattern,
    string? MatchedValue,
    int? ResponseStatusCode,
    string? CorrelationId,
    double ProcessingTimeMs,
    DateTime CreatedAt);

public record WafAnalyticsDto(
    int TotalEvents,
    int BlockedCount,
    int ChallengedCount,
    int LoggedCount,
    int RateLimitedCount,
    double BlockRate,
    List<TopIpDto> TopBlockedIps,
    List<TopRuleDto> TopTriggeredRules,
    List<HourlyBreakdownDto> HourlyBreakdown);

public record TopIpDto(string Ip, int Count);
public record TopRuleDto(string RuleName, int Count);
public record HourlyBreakdownDto(DateTime Hour, int Count);
