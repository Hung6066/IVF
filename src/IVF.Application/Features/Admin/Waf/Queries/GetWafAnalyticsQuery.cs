using IVF.Application.Common;
using MediatR;

namespace IVF.Application.Features.Admin.Waf.Queries;

public record GetWafAnalyticsQuery : IRequest<Result<WafAnalyticsDto>>;

public class WafAnalyticsDto
{
    public int TotalRequests { get; set; }
    public int BlockedRequests { get; set; }
    public float BlockRate { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<WafEventDto> RecentEvents { get; set; } = [];
}

public class WafEventDto
{
    public required string RuleId { get; set; }
    public required string Action { get; set; }
    public required string ClientIp { get; set; }
    public required DateTime Timestamp { get; set; }
}

public class GetWafAnalyticsQueryHandler : IRequestHandler<GetWafAnalyticsQuery, Result<WafAnalyticsDto>>
{
    public Task<Result<WafAnalyticsDto>> Handle(GetWafAnalyticsQuery request, CancellationToken cancellationToken)
    {
        // Mock data — integrate with actual WAF provider (Cloudflare, etc.) in production
        var analytics = new WafAnalyticsDto
        {
            TotalRequests = 15842,
            BlockedRequests = 234,
            BlockRate = 1.48f,
            LastUpdated = DateTime.UtcNow,
            RecentEvents = new List<WafEventDto>
            {
                new() { RuleId = "SQL_INJECTION", Action = "BLOCK", ClientIp = "203.0.113.45", Timestamp = DateTime.UtcNow.AddSeconds(-30) },
                new() { RuleId = "XSS_ATTACK", Action = "LOG", ClientIp = "198.51.100.88", Timestamp = DateTime.UtcNow.AddSeconds(-60) },
                new() { RuleId = "RATE_LIMIT", Action = "CHALLENGE", ClientIp = "192.0.2.77", Timestamp = DateTime.UtcNow.AddSeconds(-120) }
            }
        };

        return Task.FromResult(Result<WafAnalyticsDto>.Success(analytics));
    }
}
