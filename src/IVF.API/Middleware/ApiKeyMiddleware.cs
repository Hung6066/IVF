using System.Security.Claims;
using IVF.Application.Common.Interfaces;

namespace IVF.API.Middleware;

/// <summary>
/// Middleware that authenticates requests using an X-API-Key header or apiKey query parameter.
/// If a valid API key is present, creates a ClaimsPrincipal with key metadata.
/// Falls through to JWT/VaultToken authentication if no API key is provided.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    private const string ApiKeyHeader = "X-API-Key";
    private const string ApiKeyQuery = "apiKey";

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyValidator apiKeyValidator)
    {
        // Skip if already authenticated (e.g., by VaultTokenMiddleware)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        var rawKey = context.Request.Headers[ApiKeyHeader].FirstOrDefault()
                  ?? context.Request.Query[ApiKeyQuery].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(rawKey))
        {
            var result = await apiKeyValidator.ValidateAsync(rawKey, context.RequestAborted);

            if (result is not null)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, result.KeyName),
                    new("auth_method", "api_key"),
                    new("api_key_source", result.Source),
                    new("service_name", result.ServiceName)
                };

                if (result.KeyId.HasValue)
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, result.KeyId.Value.ToString()));
                if (result.Environment is not null)
                    claims.Add(new Claim("api_key_environment", result.Environment));
                if (result.KeyPrefix is not null)
                    claims.Add(new Claim("api_key_prefix", result.KeyPrefix));

                claims.Add(new Claim("api_key_version", result.Version.ToString()));

                var identity = new ClaimsIdentity(claims, "ApiKey");
                context.User = new ClaimsPrincipal(identity);

                _logger.LogDebug("Request authenticated via API key: {KeyName} ({Source})", result.KeyName, result.Source);
            }
            else
            {
                _logger.LogDebug("Invalid API key presented");
            }
        }

        await _next(context);
    }
}

public static class ApiKeyMiddlewareExtensions
{
    /// <summary>
    /// Adds API key authentication middleware. Place before UseAuthentication().
    /// Requests with a valid X-API-Key header or apiKey query param are authenticated.
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyMiddleware>();
    }
}
