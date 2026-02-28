using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using MediatR;
using FluentValidation;

namespace IVF.Application.Features.ZeroTrust.Commands;

// Update ZT Policy
public record UpdateZTPolicyCommand(
    ZTVaultAction Action,
    AuthLevel RequiredAuthLevel,
    RiskLevel MaxAllowedRisk,
    bool RequireTrustedDevice,
    bool RequireFreshSession,
    bool BlockAnomaly,
    bool RequireGeoFence,
    string? AllowedCountries,
    bool BlockVpnTor,
    bool AllowBreakGlassOverride,
    Guid UpdatedBy) : IRequest<bool>;

public class UpdateZTPolicyCommandHandler : IRequestHandler<UpdateZTPolicyCommand, bool>
{
    private readonly IZeroTrustService _ztService;

    public UpdateZTPolicyCommandHandler(IZeroTrustService ztService)
    {
        _ztService = ztService;
    }

    public async Task<bool> Handle(UpdateZTPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = Domain.Entities.ZTPolicy.Create(
            request.Action.ToString(),
            request.RequiredAuthLevel.ToString(),
            request.MaxAllowedRisk.ToString(),
            request.RequireTrustedDevice,
            request.RequireFreshSession,
            request.BlockAnomaly,
            request.RequireGeoFence,
            request.AllowedCountries,
            request.BlockVpnTor,
            request.AllowBreakGlassOverride);

        return await _ztService.UpdatePolicyAsync(request.Action, policy, cancellationToken);
    }
}

public class UpdateZTPolicyCommandValidator : AbstractValidator<UpdateZTPolicyCommand>
{
    public UpdateZTPolicyCommandValidator()
    {
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.RequiredAuthLevel).IsInEnum();
        RuleFor(x => x.MaxAllowedRisk).IsInEnum();
        RuleFor(x => x.UpdatedBy).NotEmpty();
    }
}
