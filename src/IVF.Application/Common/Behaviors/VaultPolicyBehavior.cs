using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IVF.Application.Common.Behaviors;

/// <summary>
/// Marker interface for MediatR requests that require vault policy authorization.
/// Commands/queries implementing this will be checked against the user's vault policies.
/// </summary>
public interface IVaultPolicyProtected
{
    /// <summary>
    /// Resource path for policy evaluation (e.g., "patients/records", "secrets/config").
    /// Supports * (single segment) and ** (any depth) matching against VaultPolicy.PathPattern.
    /// </summary>
    string ResourcePath { get; }

    /// <summary>
    /// Required capability: "read", "create", "update", "delete", "list", or "sudo".
    /// </summary>
    string RequiredCapability { get; }
}

/// <summary>
/// MediatR pipeline behavior that enforces vault policy authorization.
/// Runs before the handler. Throws UnauthorizedAccessException if denied.
/// Admin role bypasses all checks.
/// </summary>
public class VaultPolicyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IVaultPolicyEvaluator _evaluator;
    private readonly ICurrentUserService _currentUser;
    private readonly ISecurityEventPublisher _securityEvents;
    private readonly ILogger<VaultPolicyBehavior<TRequest, TResponse>> _logger;

    public VaultPolicyBehavior(
        IVaultPolicyEvaluator evaluator,
        ICurrentUserService currentUser,
        ISecurityEventPublisher securityEvents,
        ILogger<VaultPolicyBehavior<TRequest, TResponse>> logger)
    {
        _evaluator = evaluator;
        _currentUser = currentUser;
        _securityEvents = securityEvents;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IVaultPolicyProtected policyRequest)
            return await next();

        var evaluation = await _evaluator.EvaluateAsync(
            policyRequest.ResourcePath,
            policyRequest.RequiredCapability,
            cancellationToken);

        if (!evaluation.Allowed)
        {
            _logger.LogWarning(
                "Vault policy denied: user={User}, path={Path}, capability={Capability}, reason={Reason}",
                _currentUser.Username ?? "unknown",
                policyRequest.ResourcePath,
                policyRequest.RequiredCapability,
                evaluation.Reason);

            await _securityEvents.PublishAsync(new VaultSecurityEvent
            {
                EventType = "vault.policy.denied",
                Severity = SecuritySeverity.High,
                Source = "VaultPolicyBehavior",
                Action = policyRequest.RequiredCapability,
                UserId = _currentUser.UserId?.ToString(),
                ResourceType = "VaultPolicy",
                ResourceId = policyRequest.ResourcePath,
                Outcome = "deny",
                Reason = evaluation.Reason
            }, cancellationToken);

            throw new UnauthorizedAccessException(
                $"Vault policy denied: {evaluation.Reason}");
        }

        _logger.LogDebug(
            "Vault policy granted: user={User}, path={Path}, capability={Capability}, policy={Policy}",
            _currentUser.Username ?? "unknown",
            policyRequest.ResourcePath,
            policyRequest.RequiredCapability,
            evaluation.MatchedPolicy);

        return await next();
    }
}
