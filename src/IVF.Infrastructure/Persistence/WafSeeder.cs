using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Idempotent seeder for managed WAF rulesets — OWASP Core, Bot Management, Protocol Enforcement.
/// </summary>
public static class WafSeeder
{
    public static async Task SeedAsync(IvfDbContext db, ILogger? logger = null)
    {
        var existingCount = await db.WafRules.IgnoreQueryFilters().CountAsync(r => r.IsManaged);
        if (existingCount > 0)
        {
            logger?.LogDebug("WAF managed rules already seeded ({Count} rules), skipping", existingCount);
            return;
        }

        var rules = new List<WafRule>();

        // ─── OWASP Core Ruleset (priority 100-109) ───

        rules.Add(CreateManaged("OWASP: SQL Injection", "Detects common SQL injection patterns",
            100, WafRuleGroup.OwaspCore, WafAction.Block,
            queryStringPatterns: SqlInjectionPatterns(),
            bodyPatterns: SqlInjectionPatterns(),
            expression: "(http.request.uri.query contains \"UNION\" or http.request.body contains \"' OR 1=1\")"));

        rules.Add(CreateManaged("OWASP: XSS", "Detects cross-site scripting attempts",
            101, WafRuleGroup.OwaspCore, WafAction.Block,
            queryStringPatterns: XssPatterns(),
            bodyPatterns: XssPatterns(),
            expression: "(http.request.uri.query contains \"<script\" or http.request.body contains \"onerror=\")"));

        rules.Add(CreateManaged("OWASP: Path Traversal", "Detects directory traversal attempts",
            102, WafRuleGroup.OwaspCore, WafAction.Block,
            uriPathPatterns: ["\\.\\.[\\/\\\\]", "%2e%2e[\\/\\\\%]", "\\.\\.\\.\\.\\/"],
            queryStringPatterns: ["\\.\\.[\\/\\\\]", "%2e%2e[\\/\\\\%]"],
            expression: "(http.request.uri.path contains \"../\" or http.request.uri.query contains \"%2e%2e\")"));

        rules.Add(CreateManaged("OWASP: Command Injection", "Detects OS command injection",
            103, WafRuleGroup.OwaspCore, WafAction.Block,
            queryStringPatterns: CommandInjectionPatterns(),
            bodyPatterns: CommandInjectionPatterns(),
            expression: "(http.request.body contains \"; ls\" or http.request.body contains \"| cat /etc/passwd\")"));

        rules.Add(CreateManaged("OWASP: LDAP Injection", "Detects LDAP injection patterns",
            104, WafRuleGroup.OwaspCore, WafAction.Block,
            queryStringPatterns: ["[)(|*\\\\].*[)(|*\\\\]", "\\x00", "\\x0a", "\\x0d"],
            bodyPatterns: ["[)(|*\\\\].*[)(|*\\\\]"],
            expression: "(http.request.body matches LDAP injection patterns)"));

        rules.Add(CreateManaged("OWASP: Remote File Inclusion", "Detects RFI via URL parameters",
            105, WafRuleGroup.OwaspCore, WafAction.Block,
            queryStringPatterns: ["(?i)(https?|ftp|php|data):\\/\\/", "(?i)\\binclude\\s*\\(", "(?i)\\brequire\\s*\\("],
            expression: "(http.request.uri.query contains \"http://\" or http.request.uri.query contains \"ftp://\")"));

        rules.Add(CreateManaged("OWASP: Local File Inclusion", "Detects LFI patterns",
            106, WafRuleGroup.OwaspCore, WafAction.Block,
            queryStringPatterns: ["(?i)(?:\\/etc\\/(?:passwd|shadow|hosts))", "(?i)(?:proc\\/self\\/)", "(?i)(?:windows\\\\system32)"],
            uriPathPatterns: ["(?i)(?:\\/etc\\/(?:passwd|shadow|hosts))", "(?i)(?:proc\\/self\\/)"],
            expression: "(http.request.uri contains \"/etc/passwd\" or http.request.uri contains \"proc/self\")"));

        rules.Add(CreateManaged("OWASP: Protocol Violations", "Detects HTTP protocol abuse",
            107, WafRuleGroup.OwaspCore, WafAction.Block,
            headerPatterns: ["(?i)chunked.*chunked", "(?i)content-length:.*content-length:"],
            expression: "(http.request.headers contain duplicate Transfer-Encoding or Content-Length)"));

        rules.Add(CreateManaged("OWASP: PHP/Java Injection", "Detects PHP/Java code injection",
            108, WafRuleGroup.OwaspCore, WafAction.Block,
            queryStringPatterns: ["(?i)<\\?php", "(?i)\\beval\\s*\\(", "(?i)\\bexec\\s*\\(", "(?i)java\\.lang\\.Runtime"],
            bodyPatterns: ["(?i)<\\?php", "(?i)\\beval\\s*\\(", "(?i)\\bexec\\s*\\(", "(?i)java\\.lang\\.Runtime"],
            expression: "(http.request.body contains \"<?php\" or http.request.body contains \"eval(\")"));

        rules.Add(CreateManaged("OWASP: SSRF", "Detects server-side request forgery patterns",
            109, WafRuleGroup.OwaspCore, WafAction.Block,
            queryStringPatterns: ["(?i)(?:127\\.0\\.0\\.1|localhost|0\\.0\\.0\\.0|\\[::1\\]|169\\.254\\.169\\.254|metadata\\.google)"],
            bodyPatterns: ["(?i)(?:127\\.0\\.0\\.1|localhost|0\\.0\\.0\\.0|\\[::1\\]|169\\.254\\.169\\.254|metadata\\.google)"],
            expression: "(http.request.body contains \"169.254.169.254\" or http.request.body contains \"localhost\")"));

        // ─── Bot Management (priority 200-203) ───

        rules.Add(CreateManaged("Bot: Known Bad Bots", "Blocks known malicious bot user agents",
            200, WafRuleGroup.BotManagement, WafAction.Block,
            userAgentPatterns: [
                "(?i)(?:sqlmap|nikto|nessus|openvas|masscan|zgrab|gobuster|dirbuster|wpscan|nuclei)",
                "(?i)(?:havij|sqlninja|bbqsql|jsql|mole|pangolin)"
            ],
            expression: "(http.request.headers[\"User-Agent\"] contains known attack tools)"));

        rules.Add(CreateManaged("Bot: Empty User Agent", "Challenges requests with no User-Agent",
            201, WafRuleGroup.BotManagement, WafAction.Challenge,
            userAgentPatterns: ["^$"],
            expression: "(not http.request.headers[\"User-Agent\"])"));

        rules.Add(CreateManaged("Bot: Scanner Tools", "Blocks web scanner signatures",
            202, WafRuleGroup.BotManagement, WafAction.Block,
            userAgentPatterns: [
                "(?i)(?:burpsuite|zaproxy|acunetix|netsparker|appspider|qualys|w3af|arachni)",
                "(?i)(?:crawler|spider|scraper)(?!.*(?:google|bing|yahoo|duckduckgo|baidu))"
            ],
            expression: "(http.request.headers[\"User-Agent\"] contains scanner signatures)"));

        rules.Add(CreateManaged("Bot: Headless Browsers", "Challenges headless browser signatures",
            203, WafRuleGroup.BotManagement, WafAction.Challenge,
            userAgentPatterns: ["(?i)(?:phantomjs|headlesschrome|puppeteer|playwright|selenium|webdriver)"],
            expression: "(http.request.headers[\"User-Agent\"] contains headless browser indicators)"));

        // ─── Protocol Enforcement (priority 300-302) ───

        rules.Add(CreateManaged("Protocol: Non-Standard Methods", "Blocks unusual HTTP methods",
            300, WafRuleGroup.ProtocolEnforcement, WafAction.Block,
            methods: ["TRACE", "TRACK", "CONNECT", "PROPFIND", "PROPPATCH", "MKCOL", "COPY", "MOVE", "LOCK", "UNLOCK"],
            expression: "(http.request.method in {\"TRACE\" \"TRACK\" \"CONNECT\" \"PROPFIND\"})"));

        rules.Add(CreateManaged("Protocol: Suspicious Content-Type", "Blocks unusual content types",
            301, WafRuleGroup.ProtocolEnforcement, WafAction.Block,
            headerPatterns: [
                "(?i)content-type:\\s*(?:text\\/xml|application\\/x-www-form-urlencoded.*charset=(?!utf-8))",
                "(?i)content-type:\\s*(?:multipart\\/mixed|application\\/x-shellscript)"
            ],
            expression: "(http.request.headers[\"Content-Type\"] contains suspicious MIME types)"));

        rules.Add(CreateManaged("Protocol: Oversized Body", "Rate-limits oversized request bodies",
            302, WafRuleGroup.ProtocolEnforcement, WafAction.Log,
            headerPatterns: ["(?i)content-length:\\s*[1-9]\\d{7,}"], // > 10MB
            expression: "(http.request.headers[\"Content-Length\"] > 10485760)"));

        db.WafRules.AddRange(rules);
        await db.SaveChangesAsync();

        logger?.LogInformation("Seeded {Count} managed WAF rules", rules.Count);
    }

    private static WafRule CreateManaged(
        string name, string description, int priority,
        WafRuleGroup group, WafAction action,
        List<string>? uriPathPatterns = null,
        List<string>? queryStringPatterns = null,
        List<string>? headerPatterns = null,
        List<string>? bodyPatterns = null,
        List<string>? methods = null,
        List<string>? ipCidrList = null,
        List<string>? countryCodes = null,
        List<string>? userAgentPatterns = null,
        string? expression = null)
    {
        var rule = WafRule.Create(name, description, priority, group, action, isManaged: true, createdBy: "System");
        rule.SetMatchConditions(
            uriPathPatterns, queryStringPatterns, headerPatterns, bodyPatterns,
            methods, ipCidrList, countryCodes, userAgentPatterns,
            WafMatchType.Any, false, expression);
        return rule;
    }

    private static List<string> SqlInjectionPatterns() =>
    [
        "(?i)(?:'\\s*(?:or|and)\\s+['\"]?\\d+['\"]?\\s*=\\s*['\"]?\\d+)",
        "(?i)(?:union\\s+(?:all\\s+)?select)",
        "(?i)(?:select\\s+.*from\\s+information_schema)",
        "(?i)(?:insert\\s+into\\s+.*values)",
        "(?i)(?:delete\\s+from)",
        "(?i)(?:drop\\s+(?:table|database|schema))",
        "(?i)(?:;\\s*(?:exec|execute|xp_))",
        "(?i)(?:benchmark\\s*\\(|sleep\\s*\\(|waitfor\\s+delay)",
        "(?i)(?:0x[0-9a-f]+)",
        "(?i)(?:char\\s*\\(\\s*\\d+\\s*\\))"
    ];

    private static List<string> XssPatterns() =>
    [
        "(?i)(?:<script[^>]*>)",
        "(?i)(?:javascript\\s*:)",
        "(?i)(?:on(?:error|load|click|mouseover|focus|blur|submit|change|input)\\s*=)",
        "(?i)(?:expression\\s*\\()",
        "(?i)(?:vbscript\\s*:)",
        "(?i)(?:data\\s*:\\s*text\\/html)",
        "(?i)(?:<\\s*(?:img|iframe|object|embed|svg|math|video|audio|source|link)[^>]*>)",
        "(?i)(?:document\\.(?:cookie|domain|write|location))",
        "(?i)(?:window\\.(?:location|open))",
        "(?i)(?:alert\\s*\\(|confirm\\s*\\(|prompt\\s*\\()"
    ];

    private static List<string> CommandInjectionPatterns() =>
    [
        "(?i)(?:[;|`]\\s*(?:ls|cat|rm|wget|curl|nc|bash|sh|python|perl|ruby|php))",
        "(?i)(?:\\$\\(|`[^`]+`)",
        "(?i)(?:\\/(?:etc|proc|var|tmp|usr)\\/)",
        "(?i)(?:&&\\s*(?:ls|cat|rm|wget|curl|nc|bash|sh))",
        "(?i)(?:\\|\\|\\s*(?:ls|cat|rm|wget|curl|nc|bash|sh))",
        "(?i)(?:>\\s*\\/(?:etc|tmp|var)\\/)"
    ];
}
