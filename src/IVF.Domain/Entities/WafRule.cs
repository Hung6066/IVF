using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Application-level WAF rule — database-driven, admin-configurable.
/// Managed rules (IsManaged=true) are seeded and cannot have patterns edited by admin.
/// </summary>
public class WafRule : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Priority { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public WafRuleGroup RuleGroup { get; private set; }
    public bool IsManaged { get; private set; }

    // Match conditions (stored as JSONB)
    public List<string>? UriPathPatterns { get; private set; }
    public List<string>? QueryStringPatterns { get; private set; }
    public List<string>? HeaderPatterns { get; private set; }
    public List<string>? BodyPatterns { get; private set; }
    public List<string>? Methods { get; private set; }
    public List<string>? IpCidrList { get; private set; }
    public List<string>? CountryCodes { get; private set; }
    public List<string>? UserAgentPatterns { get; private set; }

    // Match logic
    public WafMatchType MatchType { get; private set; } = WafMatchType.Any;
    public bool NegateMatch { get; private set; }
    public string? Expression { get; private set; } // Display-only Cloudflare-like expression

    // Action
    public WafAction Action { get; private set; } = WafAction.Block;
    public int? RateLimitRequests { get; private set; }
    public int? RateLimitWindowSeconds { get; private set; }
    public string? BlockResponseMessage { get; private set; }

    // Stats
    public long HitCount { get; private set; }

    // Audit
    public string? CreatedBy { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private WafRule() { }

    public static WafRule Create(
        string name,
        string? description,
        int priority,
        WafRuleGroup ruleGroup,
        WafAction action,
        bool isManaged = false,
        string? createdBy = null)
    {
        return new WafRule
        {
            Name = name,
            Description = description,
            Priority = priority,
            RuleGroup = ruleGroup,
            Action = action,
            IsManaged = isManaged,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy
        };
    }

    public void SetMatchConditions(
        List<string>? uriPathPatterns = null,
        List<string>? queryStringPatterns = null,
        List<string>? headerPatterns = null,
        List<string>? bodyPatterns = null,
        List<string>? methods = null,
        List<string>? ipCidrList = null,
        List<string>? countryCodes = null,
        List<string>? userAgentPatterns = null,
        WafMatchType matchType = WafMatchType.Any,
        bool negateMatch = false,
        string? expression = null)
    {
        UriPathPatterns = uriPathPatterns;
        QueryStringPatterns = queryStringPatterns;
        HeaderPatterns = headerPatterns;
        BodyPatterns = bodyPatterns;
        Methods = methods;
        IpCidrList = ipCidrList;
        CountryCodes = countryCodes;
        UserAgentPatterns = userAgentPatterns;
        MatchType = matchType;
        NegateMatch = negateMatch;
        Expression = expression;
        SetUpdated();
    }

    public void SetAction(
        WafAction action,
        int? rateLimitRequests = null,
        int? rateLimitWindowSeconds = null,
        string? blockResponseMessage = null)
    {
        Action = action;
        RateLimitRequests = rateLimitRequests;
        RateLimitWindowSeconds = rateLimitWindowSeconds;
        BlockResponseMessage = blockResponseMessage;
        SetUpdated();
    }

    public void Update(string name, string? description, int priority, string? modifiedBy)
    {
        Name = name;
        Description = description;
        Priority = priority;
        LastModifiedBy = modifiedBy;
        SetUpdated();
    }

    public void Enable()
    {
        IsEnabled = true;
        SetUpdated();
    }

    public void Disable()
    {
        IsEnabled = false;
        SetUpdated();
    }

    public void IncrementHitCount()
    {
        HitCount++;
    }
}
