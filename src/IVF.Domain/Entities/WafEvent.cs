using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// High-volume WAF audit log — immutable, separate from SecurityEvent for performance.
/// Records every WAF rule match (block, challenge, rate-limit, log).
/// </summary>
public class WafEvent : BaseEntity
{
    public Guid? WafRuleId { get; private set; }
    public string RuleName { get; private set; } = string.Empty;
    public WafRuleGroup RuleGroup { get; private set; }
    public WafAction Action { get; private set; }
    public string ClientIp { get; private set; } = string.Empty;
    public string? Country { get; private set; }
    public string RequestPath { get; private set; } = string.Empty;
    public string RequestMethod { get; private set; } = string.Empty;
    public string? QueryString { get; private set; }
    public string? UserAgent { get; private set; }
    public string? MatchedPattern { get; private set; }
    public string? MatchedValue { get; private set; } // Truncated to 500 chars
    public int? ResponseStatusCode { get; private set; }
    public string? Headers { get; private set; } // JSONB
    public string? CorrelationId { get; private set; }
    public double ProcessingTimeMs { get; private set; }

    private WafEvent() { }

    public static WafEvent Create(
        Guid? wafRuleId,
        string ruleName,
        WafRuleGroup ruleGroup,
        WafAction action,
        string clientIp,
        string? country,
        string requestPath,
        string requestMethod,
        string? queryString,
        string? userAgent,
        string? matchedPattern,
        string? matchedValue,
        int? responseStatusCode,
        string? headers,
        string? correlationId,
        double processingTimeMs)
    {
        return new WafEvent
        {
            WafRuleId = wafRuleId,
            RuleName = ruleName,
            RuleGroup = ruleGroup,
            Action = action,
            ClientIp = clientIp,
            Country = country,
            RequestPath = requestPath,
            RequestMethod = requestMethod,
            QueryString = queryString,
            UserAgent = userAgent,
            MatchedPattern = matchedPattern,
            MatchedValue = matchedValue?.Length > 500 ? matchedValue[..500] : matchedValue,
            ResponseStatusCode = responseStatusCode,
            Headers = headers,
            CorrelationId = correlationId,
            ProcessingTimeMs = processingTimeMs
        };
    }
}
