using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.ZeroTrust.Queries;

// Response DTO
public record ZTPolicyResponse(
    Guid Id,
    string Action,
    string? RequiredAuthLevel,
    string? MaxAllowedRisk,
    bool RequireTrustedDevice,
    bool RequireFreshSession,
    bool BlockAnomaly,
    bool RequireGeoFence,
    string? AllowedCountries,
    bool BlockVpnTor,
    bool AllowBreakGlassOverride,
    bool IsActive,
    Guid? UpdatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// Get All ZT Policies
public record GetAllZTPoliciesQuery() : IRequest<List<ZTPolicyResponse>>;

public class GetAllZTPoliciesQueryHandler : IRequestHandler<GetAllZTPoliciesQuery, List<ZTPolicyResponse>>
{
    private readonly IZeroTrustService _ztService;

    public GetAllZTPoliciesQueryHandler(IZeroTrustService ztService)
    {
        _ztService = ztService;
    }

    public async Task<List<ZTPolicyResponse>> Handle(GetAllZTPoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = await _ztService.GetAllPoliciesAsync(cancellationToken);

        return policies.Select(p => new ZTPolicyResponse(
            p.Id, p.Action, p.RequiredAuthLevel, p.MaxAllowedRisk,
            p.RequireTrustedDevice, p.RequireFreshSession, p.BlockAnomaly,
            p.RequireGeoFence, p.AllowedCountries, p.BlockVpnTor,
            p.AllowBreakGlassOverride, p.IsActive, p.UpdatedBy,
            p.CreatedAt, p.UpdatedAt)).ToList();
    }
}

// Check ZT Access
public record CheckZTAccessQuery(ZTVaultAction Action, ZTAccessContext Context) : IRequest<ZTAccessDecision>;

public class CheckZTAccessQueryHandler : IRequestHandler<CheckZTAccessQuery, ZTAccessDecision>
{
    private readonly IZeroTrustService _ztService;

    public CheckZTAccessQueryHandler(IZeroTrustService ztService)
    {
        _ztService = ztService;
    }

    public async Task<ZTAccessDecision> Handle(CheckZTAccessQuery request, CancellationToken cancellationToken)
    {
        var ztRequest = new CheckZTAccessRequest(request.Action, request.Context);
        return await _ztService.CheckVaultAccessAsync(ztRequest, cancellationToken);
    }
}
