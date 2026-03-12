using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using MediatR;

namespace IVF.Application.Features.DomainManagement.Queries;

// ── Get Tenant Domains ──────────────────────────────────────────────────────
public record GetTenantDomainsQuery : IRequest<Result<List<TenantDomainInfo>>>;

public class GetTenantDomainsHandler : IRequestHandler<GetTenantDomainsQuery, Result<List<TenantDomainInfo>>>
{
    private readonly ICaddyConfigService _caddyConfigService;

    public GetTenantDomainsHandler(ICaddyConfigService caddyConfigService)
    {
        _caddyConfigService = caddyConfigService;
    }

    public async Task<Result<List<TenantDomainInfo>>> Handle(GetTenantDomainsQuery request, CancellationToken ct)
    {
        var domains = await _caddyConfigService.GetTenantDomainsAsync(ct);
        return Result<List<TenantDomainInfo>>.Success(domains);
    }
}

// ── Preview Generated Caddyfile ─────────────────────────────────────────────
public record GetCaddyfilePreviewQuery : IRequest<Result<string>>;

public class GetCaddyfilePreviewHandler : IRequestHandler<GetCaddyfilePreviewQuery, Result<string>>
{
    private readonly ICaddyConfigService _caddyConfigService;

    public GetCaddyfilePreviewHandler(ICaddyConfigService caddyConfigService)
    {
        _caddyConfigService = caddyConfigService;
    }

    public async Task<Result<string>> Handle(GetCaddyfilePreviewQuery request, CancellationToken ct)
    {
        var caddyfile = await _caddyConfigService.GenerateCaddyfileAsync(ct);
        return Result<string>.Success(caddyfile);
    }
}

// ── Get Current Caddy Config (runtime JSON) ─────────────────────────────────
public record GetCurrentCaddyConfigQuery : IRequest<Result<string>>;

public class GetCurrentCaddyConfigHandler : IRequestHandler<GetCurrentCaddyConfigQuery, Result<string>>
{
    private readonly ICaddyConfigService _caddyConfigService;

    public GetCurrentCaddyConfigHandler(ICaddyConfigService caddyConfigService)
    {
        _caddyConfigService = caddyConfigService;
    }

    public async Task<Result<string>> Handle(GetCurrentCaddyConfigQuery request, CancellationToken ct)
    {
        var config = await _caddyConfigService.GetCurrentConfigAsync(ct);
        return config is not null
            ? Result<string>.Success(config)
            : Result<string>.Failure("Không thể kết nối Caddy Admin API");
    }
}
