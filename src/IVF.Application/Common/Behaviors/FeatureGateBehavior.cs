using System.Reflection;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using MediatR;

namespace IVF.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that enforces feature gating.
/// Checks [RequiresFeature] attributes on requests and throws FeatureNotEnabledException
/// if the tenant does not have the required feature enabled.
/// </summary>
public class FeatureGateBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ITenantLimitService _limitService;

    public FeatureGateBehavior(ITenantLimitService limitService)
    {
        _limitService = limitService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var attributes = typeof(TRequest).GetCustomAttributes<RequiresFeatureAttribute>();
        foreach (var attr in attributes)
        {
            await _limitService.EnsureFeatureEnabledAsync(attr.FeatureCode, cancellationToken);
        }

        return await next();
    }
}
