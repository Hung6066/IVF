using MediatR;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace IVF.Application.Common.Behaviors;

/// <summary>
/// Marker interface for commands that require Zero Trust validation
/// </summary>
public interface IZeroTrustProtected
{
    ZTVaultAction RequiredAction { get; }
}

/// <summary>
/// MediatR pipeline behavior that enforces Zero Trust policies on sensitive operations
/// </summary>
public class ZeroTrustBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IZeroTrustService _ztService;
    private readonly ILogger<ZeroTrustBehavior<TRequest, TResponse>> _logger;

    public ZeroTrustBehavior(IZeroTrustService ztService, ILogger<ZeroTrustBehavior<TRequest, TResponse>> logger)
    {
        _ztService = ztService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IZeroTrustProtected ztRequest)
            return await next();

        var context = new ZTAccessContext(
            UserId: "system",
            DeviceId: "server",
            IpAddress: "127.0.0.1",
            Country: "VN",
            CurrentAuthLevel: AuthLevel.Session,
            LastPasswordVerification: null,
            IsVpn: false,
            IsTor: false,
            HasActiveAnomaly: false
        );

        var decision = await _ztService.CheckVaultAccessAsync(
            new CheckZTAccessRequest(ztRequest.RequiredAction, context),
            cancellationToken);

        if (!decision.Allowed)
        {
            _logger.LogWarning("Zero Trust denied {Action}: {Reason}", ztRequest.RequiredAction, decision.Reason);
            throw new UnauthorizedAccessException($"Zero Trust access denied: {decision.Reason}");
        }

        return await next();
    }
}
