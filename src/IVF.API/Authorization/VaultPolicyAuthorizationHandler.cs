using IVF.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace IVF.API.Authorization;

/// <summary>
/// ASP.NET authorization requirement for vault policy-based access control.
/// Apply to endpoints: .RequireAuthorization(new VaultPolicyRequirement("secrets/**", "read"))
/// </summary>
public class VaultPolicyRequirement : IAuthorizationRequirement
{
    public string ResourcePath { get; }
    public string Capability { get; }

    public VaultPolicyRequirement(string resourcePath, string capability)
    {
        ResourcePath = resourcePath;
        Capability = capability;
    }
}

/// <summary>
/// Handles vault policy authorization requirements.
/// Evaluates the current user's assigned vault policies (or vault token policies)
/// against the required resource path and capability.
/// </summary>
public class VaultPolicyAuthorizationHandler : AuthorizationHandler<VaultPolicyRequirement>
{
    private readonly IVaultPolicyEvaluator _evaluator;
    private readonly ILogger<VaultPolicyAuthorizationHandler> _logger;

    public VaultPolicyAuthorizationHandler(
        IVaultPolicyEvaluator evaluator,
        ILogger<VaultPolicyAuthorizationHandler> logger)
    {
        _evaluator = evaluator;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VaultPolicyRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            return; // Not authenticated → fail
        }

        var evaluation = await _evaluator.EvaluateAsync(
            requirement.ResourcePath, requirement.Capability);

        if (evaluation.Allowed)
        {
            _logger.LogDebug("Vault policy authorized: {Path}/{Capability} via {Policy}",
                requirement.ResourcePath, requirement.Capability, evaluation.MatchedPolicy);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogDebug("Vault policy denied: {Path}/{Capability} — {Reason}",
                requirement.ResourcePath, requirement.Capability, evaluation.Reason);
        }
    }
}

/// <summary>
/// Extension methods for vault policy authorization.
/// </summary>
public static class VaultPolicyAuthorizationExtensions
{
    /// <summary>
    /// Adds vault policy authorization services.
    /// </summary>
    public static IServiceCollection AddVaultPolicyAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, VaultPolicyAuthorizationHandler>();
        return services;
    }

    /// <summary>
    /// Requires a vault policy with the specified path and capability for this endpoint.
    /// Example: .RequireVaultPolicy("patients/**", "read")
    /// </summary>
    public static TBuilder RequireVaultPolicy<TBuilder>(
        this TBuilder builder, string resourcePath, string capability)
        where TBuilder : IEndpointConventionBuilder
    {
        var policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new VaultPolicyRequirement(resourcePath, capability))
            .Build();
        return builder.RequireAuthorization(policy);
    }
}
