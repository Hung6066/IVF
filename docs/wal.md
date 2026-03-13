Plan to implement                                                                                               │
│                                                                                                                 │
│ Application-Level WAF Service (Cloudflare-inspired)                                                             │
│                                                                                                                 │
│ Context                                                                                                         │
│                                                                                                                 │
│ The IVF system already has scattered security checks across multiple middleware (ZeroTrustMiddleware,           │
│ SecurityEnforcementMiddleware, ThreatDetectionService) with hardcoded regex patterns. There's a read-only       │
│ CloudflareWafService that monitors the external Cloudflare edge WAF, but no database-driven, admin-configurable │
│  rule engine at the application level.                                                                          │
│                                                                                                                 │
│ This plan creates a unified WAF service with configurable rules (like Cloudflare custom rules + managed         │
│ rulesets), a dedicated WAF event log, admin CRUD endpoints, analytics, and real-time SignalR event              │
│ broadcasting.                                                                                                   │
│                                                                                                                 │
│ ---                                                                                                             │
│ New Files (12)                                                                                                  │
│                                                                                                                 │
│ Domain Layer                                                                                                    │
│                                                                                                                 │
│ 1. src/IVF.Domain/Entities/WafRule.cs — WAF rule entity                                                         │
│   - Extends BaseEntity. Fields: Name, Description, Priority, IsEnabled, RuleGroup, IsManaged                    │
│   - Match conditions (JSONB): UriPathPatterns, QueryStringPatterns, HeaderPatterns, BodyPatterns, Methods,      │
│ IpCidrList, CountryCodes, UserAgentPatterns                                                                     │
│   - MatchType (Any/All), NegateMatch, Expression (display-only Cloudflare-like expression)                      │
│   - Action (Block/Challenge/Log/RateLimit/AllowBypass), RateLimitRequests/WindowSeconds, BlockResponseMessage   │
│   - HitCount, CreatedBy, LastModifiedBy                                                                         │
│   - Factory Create(), mutation methods SetMatchConditions(), SetAction(), Update(), Enable(), Disable()         │
│   - Managed rules: admin can toggle enable/disable but cannot edit patterns                                     │
│ 2. src/IVF.Domain/Entities/WafEvent.cs — High-volume WAF audit log                                              │
│   - Extends BaseEntity. Immutable (no updates). Separate from SecurityEvent for performance                     │
│   - Fields: WafRuleId, RuleName, RuleGroup, Action, ClientIp, Country, RequestPath, RequestMethod, QueryString, │
│  UserAgent, MatchedPattern, MatchedValue (truncated 500 chars), ResponseStatusCode, Headers (JSONB),            │
│ CorrelationId, ProcessingTimeMs                                                                                 │
│ 3. src/IVF.Domain/Enums/WafEnums.cs — Enums: WafAction, WafRuleGroup, WafMatchType                              │
│                                                                                                                 │
│ Application Layer                                                                                               │
│                                                                                                                 │
│ 4. src/IVF.Application/Features/Waf/Commands/WafCommands.cs — CQRS commands + handlers                          │
│   - CreateWafRuleCommand → creates custom rule, returns WafRuleDto                                              │
│   - UpdateWafRuleCommand → updates custom rule (rejects IsManaged)                                              │
│   - DeleteWafRuleCommand → soft-deletes custom rule (rejects IsManaged)                                         │
│   - ToggleWafRuleCommand → enables/disables any rule                                                            │
│   - Each with FluentValidation validator, handler injecting IvfDbContext + IWafService (cache invalidation)     │
│ 5. src/IVF.Application/Features/Waf/Queries/WafQueries.cs — CQRS queries + DTOs                                 │
│   - GetWafRulesQuery (optional group filter) → List<WafRuleDto>                                                 │
│   - GetWafRuleByIdQuery → WafRuleDto?                                                                           │
│   - GetWafEventsQuery (paged, filter: dateFrom/dateTo/ip/ruleGroup/action) → paged result                       │
│   - GetWafAnalyticsQuery → WafAnalyticsDto (24h: total events, blocked/challenged/logged counts, block rate,    │
│ top blocked IPs, top triggered rules, hourly breakdown)                                                         │
│                                                                                                                 │
│ Infrastructure Layer                                                                                            │
│                                                                                                                 │
│ 6. src/IVF.Infrastructure/Services/WafService.cs — Core rule engine                                             │
│   - Implements IWafService. Injects IServiceScopeFactory, IDistributedCache, IMemoryCache                       │
│   - EvaluateRequestAsync(): loads cached rules, iterates by priority, evaluates match conditions (regex with 1s │
│  timeout, CIDR matching, exact/contains), first blocking match wins                                             │
│   - GetActiveRulesAsync(): Redis cache (5-min TTL) → memory fallback → DB query                                 │
│   - RecordEventAsync(): writes to singleton Channel<WafEventData> (fire-and-forget)                             │
│   - Rate limiting via Redis counters: key waf:rl:{ruleId}:{clientIp}                                            │
│ 7. src/IVF.Infrastructure/Services/WafEventChannel.cs — Singleton Channel<WafEventData> wrapper (bounded 10k,   │
│ drop oldest on overflow)                                                                                        │
│ 8. src/IVF.Infrastructure/Services/WafEventWriter.cs — BackgroundService that drains channel, batch-inserts to  │
│ DB (100 events or 2s flush interval)                                                                            │
│ 9. src/IVF.Infrastructure/Persistence/Configurations/WafRuleConfiguration.cs                                    │
│   - Table waf_rules. JSONB columns for pattern arrays. Composite index on (IsEnabled, Priority)                 │
│ 10. src/IVF.Infrastructure/Persistence/Configurations/WafEventConfiguration.cs                                  │
│   - Table waf_events. Indexes on ClientIp, WafRuleId, Action, CreatedAt, (Action+CreatedAt),                    │
│ (ClientIp+CreatedAt)                                                                                            │
│ 11. src/IVF.Infrastructure/Persistence/WafSeeder.cs — Idempotent seeder for managed rulesets                    │
│   - OWASP Core (priority 100-109): SQL injection, XSS, path traversal, command injection, LDAP injection, RFI,  │
│ LFI, protocol violations, PHP/Java injection, SSRF                                                              │
│   - Bot Management (priority 200-203): known bad bots, empty UA, scanner tools, headless browsers               │
│   - Protocol Enforcement (priority 300-302): non-standard HTTP methods, suspicious content-type, oversized      │
│ bodies                                                                                                          │
│                                                                                                                 │
│ API Layer                                                                                                       │
│                                                                                                                 │
│ 12. src/IVF.API/Middleware/WafMiddleware.cs — HTTP pipeline middleware                                          │
│   - Exempt paths: /health, /healthz, /hubs, /swagger, /favicon.ico                                              │
│   - Body inspection: POST/PUT/PATCH only, content-length < 1MB, uses EnableBuffering() + stream reset           │
│   - Builds WafRequestContext, calls IWafService.EvaluateRequestAsync()                                          │
│   - Block → 403 JSON response. Challenge → 403 with challengeRequired: true. RateLimit → 429                    │
│   - Response headers: X-WAF-Status, X-WAF-Rule                                                                  │
│   - SignalR broadcast block/challenge events to infra-monitoring group                                          │
│   - Extension: app.UseWaf()                                                                                     │
│                                                                                                                 │
│ ---                                                                                                             │
│ Modified Files (5)                                                                                              │
│                                                                                                                 │
│ 1. src/IVF.Application/Common/Interfaces/ISecurityServices.cs                                                   │
│                                                                                                                 │
│ Append IWafService interface + DTOs after existing interfaces (~line 260+):                                     │
│ - IWafService: EvaluateRequestAsync(), GetActiveRulesAsync(), InvalidateCacheAsync(), RecordEventAsync()        │
│ - Records: WafRequestContext, WafEvaluationResult, WafRuleCacheEntry, WafEventData                              │
│                                                                                                                 │
│ 2. src/IVF.Domain/Entities/SecurityEvent.cs                                                                     │
│                                                                                                                 │
│ Add 4 WAF event type constants to SecurityEventTypes class (after line 186):                                    │
│ WafBlocked = "WAF_BLOCKED"                                                                                      │
│ WafChallenged = "WAF_CHALLENGED"                                                                                │
│ WafRateLimited = "WAF_RATE_LIMITED"                                                                             │
│ WafBypassed = "WAF_BYPASSED"                                                                                    │
│                                                                                                                 │
│ 3. src/IVF.Infrastructure/Persistence/IvfDbContext.cs                                                           │
│                                                                                                                 │
│ Add 2 DbSets after the Enterprise Security section (~line 152):                                                 │
│ // Application-Level WAF                                                                                        │
│ public DbSet<WafRule> WafRules => Set<WafRule>();                                                               │
│ public DbSet<WafEvent> WafEvents => Set<WafEvent>();                                                            │
│                                                                                                                 │
│ 4. src/IVF.API/Endpoints/WafEndpoints.cs                                                                        │
│                                                                                                                 │
│ Expand from 2 Cloudflare-only endpoints to full WAF admin API:                                                  │
│ - GET /api/admin/waf/rules — list rules (optional ?group filter)                                                │
│ - GET /api/admin/waf/rules/{id} — get single rule                                                               │
│ - POST /api/admin/waf/rules — create custom rule                                                                │
│ - PUT /api/admin/waf/rules/{id} — update custom rule                                                            │
│ - DELETE /api/admin/waf/rules/{id} — soft-delete custom rule                                                    │
│ - PUT /api/admin/waf/rules/{id}/toggle — enable/disable any rule                                                │
│ - GET /api/admin/waf/events — paginated WAF events with filters                                                 │
│ - GET /api/admin/waf/analytics — 24h WAF analytics dashboard                                                    │
│ - POST /api/admin/waf/cache/invalidate — force cache refresh                                                    │
│ - GET /api/admin/waf/cloudflare/status — existing Cloudflare status (moved to sub-path)                         │
│ - GET /api/admin/waf/cloudflare/events — existing Cloudflare events (moved to sub-path)                         │
│ - Authorization: RequireAuthorization("AdminOnly")                                                              │
│                                                                                                                 │
│ 5. src/IVF.API/Program.cs                                                                                       │
│                                                                                                                 │
│ Three changes:                                                                                                  │
│ - Service registration (~line 411): Register WafEventChannel (singleton), IWafService/WafService (scoped),      │
│ WafEventWriter (hosted)                                                                                         │
│ - Middleware pipeline (line 694): Insert app.UseWaf() BEFORE app.UseRateLimiter() — WAF is the first security   │
│ gate                                                                                                            │
│ - Seeder (~line 809): Add await WafSeeder.SeedAsync(db);                                                        │
│                                                                                                                 │
│ ---                                                                                                             │
│ Middleware Pipeline Order (after change)                                                                        │
│                                                                                                                 │
│ CORS → CorrelationId → Serilog → ExceptionHandler → SecurityHeaders                                             │
│ → WAF (NEW) → RateLimiter → SecurityEnforcement → VaultToken → ApiKey                                           │
│ → Authentication → Authorization → TenantResolution → LogContext                                                │
│ → ConsentEnforcement → TokenBinding → ZeroTrust → ApiCallLogging                                                │
│                                                                                                                 │
│ ---                                                                                                             │
│ Implementation Order                                                                                            │
│                                                                                                                 │
│ 1. Domain: WafRule, WafEvent, WafEnums, SecurityEventTypes additions                                            │
│ 2. Application: IWafService interface + DTOs in ISecurityServices.cs                                            │
│ 3. Application: WafCommands.cs, WafQueries.cs                                                                   │
│ 4. Infrastructure: WafRuleConfiguration, WafEventConfiguration, IvfDbContext DbSets                             │
│ 5. Infrastructure: WafEventChannel, WafService, WafEventWriter                                                  │
│ 6. Infrastructure: WafSeeder                                                                                    │
│ 7. API: WafMiddleware                                                                                           │
│ 8. API: WafEndpoints (rewrite)                                                                                  │
│ 9. API: Program.cs wiring (services, middleware, seeder)                                                        │
│ 10. EF Core migration: dotnet ef migrations add AddApplicationWaf                                               │
│                                                                                                                 │
│ ---                                                                                                             │
│ Verification                                                                                                    │
│                                                                                                                 │
│ 1. dotnet build — no compile errors                                                                             │
│ 2. dotnet ef migrations add AddApplicationWaf --project src/IVF.Infrastructure --startup-project src/IVF.API —  │
│ migration generates successfully                                                                                │
│ 3. dotnet run --project src/IVF.API — app starts, WafSeeder seeds 17 managed rules                              │
│ 4. GET /api/admin/waf/rules — returns seeded managed rulesets                                                   │
│ 5. POST /api/admin/waf/rules — create a custom rule, verify it appears in list                                  │
│ 6. PUT /api/admin/waf/rules/{id}/toggle — disable a managed rule, verify cache invalidation                     │
│ 7. Send a request with SQL injection pattern (e.g., ?q=1 UNION SELECT) — verify 403 WAF_BLOCKED response        │
│ 8. GET /api/admin/waf/events — verify WAF event was recorded                                                    │
│ 9. GET /api/admin/waf/analytics — verify analytics aggregation  