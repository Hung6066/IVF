using IVF.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace IVF.API.Hubs;

/// <summary>
/// Custom authorization filter for SignalR hubs that validates API keys from desktop clients.
/// Uses centralized IApiKeyValidator for consistent validation.
/// </summary>
public class ApiKeyAuthorizationFilter : IHubFilter
{
    private readonly IApiKeyValidator _apiKeyValidator;
    private readonly ILogger<ApiKeyAuthorizationFilter> _logger;

    public ApiKeyAuthorizationFilter(IApiKeyValidator apiKeyValidator, ILogger<ApiKeyAuthorizationFilter> logger)
    {
        _apiKeyValidator = apiKeyValidator;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var httpContext = invocationContext.Context.GetHttpContext();

        // Check if this is an authenticated user (JWT or API key via middleware)
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            return await next(invocationContext);
        }

        // Check for API key in query string (desktop client — fallback for WebSocket connections)
        var apiKey = httpContext?.Request.Query["apiKey"].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Connection attempt without API key or authentication from {ConnectionId}",
                invocationContext.Context.ConnectionId);
            throw new HubException("API key required for desktop clients");
        }

        var result = await _apiKeyValidator.ValidateAsync(apiKey);

        if (result is null)
        {
            _logger.LogWarning("Invalid API key attempt from {ConnectionId}", invocationContext.Context.ConnectionId);
            throw new HubException("Invalid API key");
        }

        _logger.LogInformation("Desktop client authenticated with API key: {ConnectionId}",
            invocationContext.Context.ConnectionId);

        return await next(invocationContext);
    }
}
