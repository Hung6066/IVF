using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IVF.API.Contracts;
using IVF.API.Services;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IVF.API.Endpoints;

/// <summary>
/// SSO/OIDC federation endpoints for external identity provider authentication.
/// Supports Google, Microsoft Entra ID, and any standard OIDC provider.
/// Uses Authorization Code Flow with PKCE (RFC 7636).
/// </summary>
public static class SsoEndpoints
{
    // Cache OIDC discovery documents (thread-safe, 6h TTL)
    private static readonly ConcurrentDictionary<string, (OidcDiscovery Doc, DateTime CachedAt)> _discoveryCache = new();
    private static readonly ConcurrentDictionary<string, (JsonWebKeySet Keys, DateTime CachedAt)> _jwksCache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public static void MapSsoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/sso").WithTags("SSO/OIDC");

        // ─── List configured SSO providers (public, unauthenticated) ───
        group.MapGet("/providers", (IConfiguration config) =>
        {
            var providers = GetProviders(config);
            return Results.Ok(providers
                .Where(p => p.Enabled)
                .Select(p => new
                {
                    p.Id,
                    p.DisplayName,
                    p.IconUrl,
                    p.ClientId,
                    Scopes = string.Join(" ", p.Scopes)
                }));
        });

        // ─── Generate OIDC authorization URL with PKCE ───
        group.MapGet("/{providerId}/authorize-url", async (
            string providerId,
            string redirectUri,
            string codeChallenge,
            string state,
            string? nonce,
            IConfiguration config,
            IHttpClientFactory httpFactory) =>
        {
            var provider = GetProvider(config, providerId);
            if (provider is null)
                return Results.NotFound(new { error = "SSO provider not found or disabled" });

            if (!IsAllowedRedirectUri(config, redirectUri))
                return Results.BadRequest(new { error = "Invalid redirect URI" });

            var client = httpFactory.CreateClient();
            var discovery = await GetDiscoveryDocAsync(client, provider.Authority);
            if (discovery is null)
                return Results.Json(new { error = "Failed to reach identity provider" }, statusCode: 502);

            var scopes = Uri.EscapeDataString(string.Join(" ", provider.Scopes));
            var url = $"{discovery.AuthorizationEndpoint}" +
                $"?client_id={Uri.EscapeDataString(provider.ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&response_type=code" +
                $"&scope={scopes}" +
                $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
                $"&code_challenge_method=S256" +
                $"&state={Uri.EscapeDataString(state)}" +
                "&prompt=select_account";

            if (!string.IsNullOrEmpty(nonce))
                url += $"&nonce={Uri.EscapeDataString(nonce)}";

            return Results.Ok(new { authorizeUrl = url });
        });

        // ─── Exchange authorization code for IVF JWT (token endpoint) ───
        group.MapPost("/{providerId}/token", async (
            string providerId,
            SsoTokenRequest request,
            IConfiguration config,
            IUserRepository userRepo,
            IUnitOfWork uow,
            ISecurityEventService securityEvents,
            IvfDbContext db,
            IHttpClientFactory httpFactory,
            HttpContext httpContext) =>
        {
            var provider = GetProvider(config, providerId);
            if (provider is null)
                return Results.NotFound(new { error = "SSO provider not found or disabled" });

            if (!IsAllowedRedirectUri(config, request.RedirectUri))
                return Results.BadRequest(new { error = "Invalid redirect URI" });

            var ipAddress = AuthEndpoints.GetClientIp(httpContext);
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();

            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            // 1. Fetch OIDC discovery document
            var discovery = await GetDiscoveryDocAsync(client, provider.Authority);
            if (discovery is null)
            {
                await LogSsoEventAsync(securityEvents, null, null, ipAddress, userAgent, false,
                    "Failed to fetch OIDC discovery document", providerId, httpContext.TraceIdentifier);
                return Results.Json(new { error = "Failed to reach identity provider" }, statusCode: 502);
            }

            // 2. Exchange authorization code for tokens at IdP
            var tokenResponse = await ExchangeCodeAsync(client, discovery.TokenEndpoint, provider, request);
            if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.IdToken))
            {
                await LogSsoEventAsync(securityEvents, null, null, ipAddress, userAgent, false,
                    "Token exchange failed at identity provider", providerId, httpContext.TraceIdentifier);
                return Results.BadRequest(new { error = "Failed to exchange authorization code" });
            }

            // 3. Validate ID token (signature, issuer, audience, expiry)
            var idTokenClaims = await ValidateIdTokenAsync(client, discovery, provider, tokenResponse.IdToken);
            if (idTokenClaims is null)
            {
                await LogSsoEventAsync(securityEvents, null, null, ipAddress, userAgent, false,
                    "ID token validation failed", providerId, httpContext.TraceIdentifier);
                return Results.Json(new { error = "Invalid identity token" }, statusCode: 401);
            }

            // 4. Extract claims from validated ID token
            var sub = idTokenClaims.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? idTokenClaims.FindFirstValue("sub");
            var email = idTokenClaims.FindFirstValue(ClaimTypes.Email)
                ?? idTokenClaims.FindFirstValue("email");
            var name = idTokenClaims.FindFirstValue("name")
                ?? idTokenClaims.FindFirstValue(ClaimTypes.GivenName)
                ?? email;
            var picture = idTokenClaims.FindFirstValue("picture");

            if (string.IsNullOrEmpty(sub))
            {
                await LogSsoEventAsync(securityEvents, null, null, ipAddress, userAgent, false,
                    "ID token missing subject claim", providerId, httpContext.TraceIdentifier);
                return Results.Json(new { error = "Invalid identity token: missing subject" }, statusCode: 401);
            }

            // 5. Look up existing external login link
            var externalLogin = await db.Set<UserExternalLogin>()
                .FirstOrDefaultAsync(x => x.Provider == provider.Id && x.ProviderKey == sub);

            User? user = null;

            if (externalLogin is not null)
            {
                // Known SSO user — load linked account
                user = await userRepo.GetByIdAsync(externalLogin.UserId);
                if (user is null || !user.IsActive)
                {
                    await LogSsoEventAsync(securityEvents, user?.Id, user?.Username, ipAddress, userAgent, false,
                        "Linked user account not found or disabled", providerId, httpContext.TraceIdentifier);
                    return Results.Json(new { error = "Account is disabled. Contact your administrator." }, statusCode: 403);
                }

                externalLogin.RecordLogin();
                externalLogin.UpdateProfile(email, name, picture);
            }
            else if (provider.AutoProvision && !string.IsNullOrEmpty(email))
            {
                // Auto-provision: check if user already exists with this email
                user = await userRepo.GetByUsernameAsync(email);
                if (user is not null)
                {
                    // Link existing user to this SSO provider
                    externalLogin = UserExternalLogin.Create(user.Id, provider.Id, sub, email, name);
                    db.Set<UserExternalLogin>().Add(externalLogin);
                }
                else
                {
                    // Create new user (SSO users have no password)
                    var tenantId = provider.DefaultTenantId ?? Guid.Empty;
                    var role = provider.DefaultRole ?? "Doctor";
                    user = User.Create(
                        username: email,
                        passwordHash: "",
                        fullName: name ?? email,
                        role: role,
                        tenantId: tenantId);

                    await userRepo.AddAsync(user);
                    externalLogin = UserExternalLogin.Create(user.Id, provider.Id, sub, email, name);
                    db.Set<UserExternalLogin>().Add(externalLogin);
                }
            }
            else
            {
                await LogSsoEventAsync(securityEvents, null, null, ipAddress, userAgent, false,
                    $"No linked account and auto-provision disabled (provider={providerId}, sub={sub})",
                    providerId, httpContext.TraceIdentifier);
                return Results.Json(new { error = "No linked account found. Contact your administrator." }, statusCode: 403);
            }

            // 6. Generate IVF JWT + refresh token (same as password login)
            var token = AuthEndpoints.GenerateJwtToken(user, config, amr: "external");
            var refreshToken = AuthEndpoints.GenerateRefreshToken();

            var tokenFamily = httpContext.RequestServices.GetRequiredService<RefreshTokenFamilyService>();
            tokenFamily.RegisterToken(user.Id, refreshToken, null);

            user.UpdateRefreshToken(AuthEndpoints.HashRefreshToken(refreshToken), DateTime.UtcNow.AddDays(7));
            await userRepo.UpdateAsync(user);
            await uow.SaveChangesAsync();

            // 7. Log successful SSO login
            await LogSsoEventAsync(securityEvents, user.Id, user.Username, ipAddress, userAgent, true,
                null, providerId, httpContext.TraceIdentifier);

            AuthEndpoints.SetAuthCookie(httpContext, token);
            return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
        });
    }

    // ─── OIDC Discovery ───

    private static async Task<OidcDiscovery?> GetDiscoveryDocAsync(HttpClient client, string authority)
    {
        var key = authority.TrimEnd('/');

        if (_discoveryCache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheDuration)
            return cached.Doc;

        try
        {
            var discoveryUrl = $"{key}/.well-known/openid-configuration";
            var response = await client.GetAsync(discoveryUrl);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Deserialize<OidcDiscovery>(json);
            if (doc is null || string.IsNullOrEmpty(doc.TokenEndpoint))
                return null;

            _discoveryCache[key] = (doc, DateTime.UtcNow);
            return doc;
        }
        catch
        {
            return null;
        }
    }

    // ─── Token Exchange ───

    private static async Task<OidcTokenResponse?> ExchangeCodeAsync(
        HttpClient client, string tokenEndpoint, SsoProviderConfig provider, SsoTokenRequest request)
    {
        try
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = request.Code,
                ["redirect_uri"] = request.RedirectUri,
                ["client_id"] = provider.ClientId,
                ["client_secret"] = provider.ClientSecret,
                ["code_verifier"] = request.CodeVerifier
            };

            var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form));
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OidcTokenResponse>(json);
        }
        catch
        {
            return null;
        }
    }

    // ─── ID Token Validation ───

    private static async Task<ClaimsPrincipal?> ValidateIdTokenAsync(
        HttpClient client, OidcDiscovery discovery, SsoProviderConfig provider, string idToken)
    {
        try
        {
            // Fetch JWKS for signature verification
            var jwks = await GetJwksAsync(client, discovery.JwksUri);
            if (jwks is null)
                return null;

            var handler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = discovery.Issuer,
                ValidateAudience = true,
                ValidAudience = provider.ClientId,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = jwks.GetSigningKeys(),
                ClockSkew = TimeSpan.FromMinutes(2),
                NameClaimType = "name",
                RoleClaimType = ClaimTypes.Role
            };

            var principal = handler.ValidateToken(idToken, validationParams, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<JsonWebKeySet?> GetJwksAsync(HttpClient client, string jwksUri)
    {
        if (string.IsNullOrEmpty(jwksUri))
            return null;

        if (_jwksCache.TryGetValue(jwksUri, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheDuration)
            return cached.Keys;

        try
        {
            var json = await client.GetStringAsync(jwksUri);
            var jwks = new JsonWebKeySet(json);
            _jwksCache[jwksUri] = (jwks, DateTime.UtcNow);
            return jwks;
        }
        catch
        {
            return null;
        }
    }

    // ─── Configuration ───

    private static List<SsoProviderConfig> GetProviders(IConfiguration config)
    {
        return config.GetSection("Sso:Providers").Get<List<SsoProviderConfig>>() ?? [];
    }

    private static SsoProviderConfig? GetProvider(IConfiguration config, string providerId)
    {
        return GetProviders(config).FirstOrDefault(p =>
            string.Equals(p.Id, providerId, StringComparison.OrdinalIgnoreCase) && p.Enabled);
    }

    private static bool IsAllowedRedirectUri(IConfiguration config, string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
            return false;

        // Development: allow localhost
        if (uri.Host is "localhost" or "127.0.0.1")
            return true;

        // Production: check against allowed origins
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var origin = $"{uri.Scheme}://{uri.Authority}";
        return allowedOrigins.Any(a => string.Equals(a, origin, StringComparison.OrdinalIgnoreCase))
            || (uri.Scheme == "https" && uri.Host.EndsWith(".natra.site", StringComparison.OrdinalIgnoreCase));
    }

    // ─── Security Event Logging ───

    private static async Task LogSsoEventAsync(
        ISecurityEventService securityEvents,
        Guid? userId, string? username,
        string ipAddress, string? userAgent,
        bool isSuccess, string? failureReason,
        string providerId, string correlationId)
    {
        await securityEvents.LogEventAsync(SecurityEvent.Create(
            eventType: isSuccess
                ? SecurityEventTypes.ExternalLoginSuccess
                : SecurityEventTypes.ExternalLoginFailed,
            severity: isSuccess ? "Info" : "Medium",
            userId: userId,
            username: username,
            ipAddress: ipAddress,
            userAgent: userAgent,
            requestPath: $"/api/auth/sso/{providerId}/token",
            requestMethod: "POST",
            responseStatusCode: isSuccess ? 200 : 401,
            correlationId: correlationId,
            details: JsonSerializer.Serialize(new
            {
                provider = providerId,
                failureReason
            })));
    }
}

// ─── Configuration Models ───

public sealed class SsoProviderConfig
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = ["openid", "profile", "email"];
    public bool Enabled { get; set; }
    public bool AutoProvision { get; set; }
    public string? DefaultRole { get; set; } = "Doctor";
    public Guid? DefaultTenantId { get; set; }
}

// ─── OIDC Protocol DTOs ───

public sealed class OidcDiscovery
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; set; } = string.Empty;

    [JsonPropertyName("userinfo_endpoint")]
    public string? UserinfoEndpoint { get; set; }

    [JsonPropertyName("end_session_endpoint")]
    public string? EndSessionEndpoint { get; set; }
}

public sealed class OidcTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; set; }
}

// ─── Request DTO ───

public record SsoTokenRequest(string Code, string RedirectUri, string CodeVerifier);
