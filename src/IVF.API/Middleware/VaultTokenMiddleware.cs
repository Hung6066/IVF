using System.Security.Claims;
using IVF.Application.Common.Interfaces;

namespace IVF.API.Middleware;

/// <summary>
/// Middleware that authenticates requests using an X-Vault-Token header.
/// If a valid vault token is present, creates a ClaimsPrincipal with token policies.
/// Falls through to JWT authentication if no vault token is provided.
/// </summary>
public class VaultTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<VaultTokenMiddleware> _logger;

    private const string VaultTokenHeader = "X-Vault-Token";

    public VaultTokenMiddleware(RequestDelegate next, ILogger<VaultTokenMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IVaultTokenValidator tokenValidator)
    {
        if (context.Request.Headers.TryGetValue(VaultTokenHeader, out var tokenValue) &&
            !string.IsNullOrWhiteSpace(tokenValue))
        {
            var rawToken = tokenValue.ToString();
            var result = await tokenValidator.ValidateTokenAsync(rawToken);

            if (result is not null)
            {
                // Create claims from vault token
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, result.TokenId.ToString()),
                    new(ClaimTypes.Name, result.DisplayName ?? result.Accessor),
                    new("vault_accessor", result.Accessor),
                    new("vault_token_type", result.TokenType),
                    new("auth_method", "vault_token")
                };

                // Add each policy as a role claim
                foreach (var policy in result.Policies)
                {
                    claims.Add(new Claim(ClaimTypes.Role, policy));
                }

                var identity = new ClaimsIdentity(claims, "VaultToken");
                context.User = new ClaimsPrincipal(identity);

                _logger.LogDebug("Request authenticated via vault token {Accessor}", result.Accessor);
            }
            else
            {
                _logger.LogDebug("Invalid vault token presented");
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for vault token middleware registration.
/// </summary>
public static class VaultTokenMiddlewareExtensions
{
    /// <summary>
    /// Adds vault token authentication middleware. Place before UseAuthorization().
    /// Requests with a valid X-Vault-Token header are authenticated with token policies.
    /// Requests without X-Vault-Token fall through to JWT authentication.
    /// </summary>
    public static IApplicationBuilder UseVaultTokenAuth(this IApplicationBuilder app)
    {
        return app.UseMiddleware<VaultTokenMiddleware>();
    }
}
