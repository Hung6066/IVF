using Microsoft.AspNetCore.SignalR;

namespace IVF.API.Hubs;

/// <summary>
/// Custom authorization filter for SignalR hubs that validates API keys from desktop clients.
/// </summary>
public class ApiKeyAuthorizationFilter : IHubFilter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyAuthorizationFilter> _logger;

    public ApiKeyAuthorizationFilter(IConfiguration configuration, ILogger<ApiKeyAuthorizationFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var httpContext = invocationContext.Context.GetHttpContext();
        
        // Check if this is an authenticated user (Angular UI with JWT)
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            // Allow authenticated users
            return await next(invocationContext);
        }

        // Check for API key in query string (desktop client)
        var apiKey = httpContext?.Request.Query["apiKey"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Connection attempt without API key or authentication from {ConnectionId}", 
                invocationContext.Context.ConnectionId);
            throw new HubException("API key required for desktop clients");
        }

        // Validate API key
        var validApiKeys = _configuration.GetSection("DesktopClients:ApiKeys").Get<string[]>() ?? Array.Empty<string>();
        
        if (!validApiKeys.Contains(apiKey))
        {
            _logger.LogWarning("Invalid API key attempt from {ConnectionId}", invocationContext.Context.ConnectionId);
            throw new HubException("Invalid API key");
        }

        _logger.LogInformation("Desktop client authenticated with API key: {ConnectionId}", 
            invocationContext.Context.ConnectionId);

        return await next(invocationContext);
    }
}
