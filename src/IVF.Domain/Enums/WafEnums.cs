namespace IVF.Domain.Enums;

/// <summary>
/// Action to take when a WAF rule matches a request.
/// </summary>
public enum WafAction
{
    Log = 0,
    Block = 1,
    Challenge = 2,
    RateLimit = 3,
    AllowBypass = 4
}

/// <summary>
/// Logical grouping for WAF rules — inspired by Cloudflare managed rulesets.
/// </summary>
public enum WafRuleGroup
{
    Custom = 0,
    OwaspCore = 1,
    BotManagement = 2,
    ProtocolEnforcement = 3
}

/// <summary>
/// How multiple match conditions within a rule are combined.
/// </summary>
public enum WafMatchType
{
    Any = 0,
    All = 1
}
