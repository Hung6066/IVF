using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IVF.API.Contracts;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Services;
using Microsoft.IdentityModel.Tokens;

namespace IVF.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (
            LoginRequest request,
            IUserRepository userRepo,
            IUnitOfWork uow,
            IConfiguration config,
            ISecurityEventService securityEvents,
            IThreatDetectionService threatDetection,
            IDeviceFingerprintService deviceFingerprint,
            IAdaptiveSessionService sessionService,
            HttpContext httpContext) =>
        {
            var ipAddress = GetClientIp(httpContext);
            var userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();

            // 1. Pre-auth threat assessment
            var securityContext = new RequestSecurityContext(
                UserId: null,
                Username: request.Username,
                IpAddress: ipAddress,
                UserAgent: userAgent,
                DeviceFingerprint: null,
                Country: null,
                City: null,
                RequestPath: "/api/auth/login",
                RequestMethod: "POST",
                SessionId: null,
                CorrelationId: httpContext.TraceIdentifier,
                Timestamp: DateTime.UtcNow);

            var assessment = await threatDetection.AssessRequestAsync(securityContext);

            // Block if critical risk
            if (assessment.ShouldBlock)
            {
                await securityEvents.LogEventAsync(SecurityEvent.Create(
                    eventType: SecurityEventTypes.LoginFailed,
                    severity: "Critical",
                    username: request.Username,
                    ipAddress: ipAddress,
                    userAgent: userAgent,
                    requestPath: "/api/auth/login",
                    requestMethod: "POST",
                    responseStatusCode: 403,
                    riskScore: assessment.RiskScore,
                    isBlocked: true,
                    correlationId: httpContext.TraceIdentifier,
                    details: assessment.BlockReason));

                return Results.Json(new { error = "Access denied", code = "ZT_BLOCKED" }, statusCode: 403);
            }

            // 2. Authenticate
            var user = await userRepo.GetByUsernameAsync(request.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                // Record failed attempt for brute force detection
                if (threatDetection is ThreatDetectionService tds)
                    tds.RecordFailedAttempt(request.Username);

                await securityEvents.LogEventAsync(SecurityEvent.Create(
                    eventType: SecurityEventTypes.LoginFailed,
                    severity: "Medium",
                    username: request.Username,
                    ipAddress: ipAddress,
                    userAgent: userAgent,
                    requestPath: "/api/auth/login",
                    requestMethod: "POST",
                    responseStatusCode: 401,
                    correlationId: httpContext.TraceIdentifier));

                // Check if brute force threshold reached
                var isBruteForce = await threatDetection.DetectBruteForceAsync(request.Username);
                if (isBruteForce)
                {
                    await securityEvents.LogEventAsync(SecurityEvent.Create(
                        eventType: SecurityEventTypes.LoginBruteForce,
                        severity: "High",
                        username: request.Username,
                        ipAddress: ipAddress,
                        correlationId: httpContext.TraceIdentifier,
                        details: $"{{\"attempts\":\"threshold_exceeded\"}}"));
                }

                return Results.Unauthorized();
            }

            // 3. Clear brute force counter on success
            if (threatDetection is ThreatDetectionService tdsSuccess)
                tdsSuccess.ClearFailedAttempts(request.Username);

            // 4. Generate device fingerprint
            var deviceSignals = new DeviceSignals(
                UserAgent: userAgent,
                AcceptLanguage: httpContext.Request.Headers.AcceptLanguage.FirstOrDefault(),
                AcceptEncoding: httpContext.Request.Headers.AcceptEncoding.FirstOrDefault(),
                IpAddress: ipAddress,
                ScreenResolution: null,
                Timezone: null,
                Platform: null,
                CookiesEnabled: null,
                DoNotTrack: null,
                ClientHints: httpContext.Request.Headers["Sec-CH-UA"].FirstOrDefault());

            var fingerprint = await deviceFingerprint.RegisterDeviceAsync(user.Id, deviceSignals);

            // 5. Check device trust
            var deviceTrust = await deviceFingerprint.CheckDeviceTrustAsync(user.Id, fingerprint);

            // 6. Create adaptive session
            var sessionCtx = new RequestSecurityContext(
                UserId: user.Id,
                Username: user.Username,
                IpAddress: ipAddress,
                UserAgent: userAgent,
                DeviceFingerprint: fingerprint,
                Country: null,
                City: null,
                RequestPath: "/api/auth/login",
                RequestMethod: "POST",
                SessionId: null,
                CorrelationId: httpContext.TraceIdentifier,
                Timestamp: DateTime.UtcNow);

            var session = await sessionService.CreateSessionAsync(user.Id, sessionCtx);

            // 7. Generate tokens
            var token = GenerateJwtToken(user, config, fingerprint, session.SessionId);
            var refreshToken = GenerateRefreshToken();
            user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
            await userRepo.UpdateAsync(user);
            await uow.SaveChangesAsync();

            // 8. Log successful login
            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.LoginSuccess,
                severity: "Info",
                userId: user.Id,
                username: user.Username,
                ipAddress: ipAddress,
                userAgent: userAgent,
                deviceFingerprint: fingerprint,
                requestPath: "/api/auth/login",
                requestMethod: "POST",
                responseStatusCode: 200,
                riskScore: assessment.RiskScore,
                correlationId: httpContext.TraceIdentifier,
                sessionId: session.SessionId,
                details: System.Text.Json.JsonSerializer.Serialize(new
                {
                    deviceTrust = deviceTrust.TrustLevel.ToString(),
                    riskLevel = assessment.RiskLevel.ToString(),
                    isNewDevice = !deviceTrust.IsKnown
                })));

            return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
        });

        group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            IUserRepository userRepo,
            IUnitOfWork uow,
            IConfiguration config,
            ISecurityEventService securityEvents,
            HttpContext httpContext) =>
        {
            var user = await userRepo.GetByRefreshTokenAsync(request.RefreshToken);
            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                await securityEvents.LogEventAsync(SecurityEvent.Create(
                    eventType: SecurityEventTypes.TokenRevoked,
                    severity: "Medium",
                    ipAddress: GetClientIp(httpContext),
                    requestPath: "/api/auth/refresh",
                    requestMethod: "POST",
                    responseStatusCode: 401,
                    correlationId: httpContext.TraceIdentifier));

                return Results.Unauthorized();
            }

            var token = GenerateJwtToken(user, config);
            var refreshToken = GenerateRefreshToken();
            user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
            await userRepo.UpdateAsync(user);
            await uow.SaveChangesAsync();

            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.TokenRefresh,
                severity: "Info",
                userId: user.Id,
                username: user.Username,
                ipAddress: GetClientIp(httpContext),
                correlationId: httpContext.TraceIdentifier));

            return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, IUserRepository userRepo) =>
        {
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            var user = await userRepo.GetByIdAsync(Guid.Parse(userId));
            return user == null ? Results.NotFound() : Results.Ok(UserDto.FromEntity(user));
        }).RequireAuthorization();

        // Get current user's permissions
        group.MapGet("/me/permissions", async (ClaimsPrincipal principal, IUserPermissionRepository permRepo) =>
        {
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            var permissions = await permRepo.GetByUserIdAsync(Guid.Parse(userId));
            return Results.Ok(permissions.Select(p => p.PermissionCode));
        }).RequireAuthorization();
    }

    private static string GenerateJwtToken(User user, IConfiguration config, string? deviceFingerprint = null, string? sessionId = null)
    {
        var jwtSettings = config.GetSection("JwtSettings");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
        var tokenHandler = new JwtSecurityTokenHandler();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("department", user.Department ?? ""),
            new("jti", Guid.NewGuid().ToString()), // Unique token ID for revocation tracking
        };

        // Zero Trust: Bind token to device and session
        if (!string.IsNullOrEmpty(deviceFingerprint))
            claims.Add(new Claim("device_fingerprint", deviceFingerprint));
        if (!string.IsNullOrEmpty(sessionId))
            claims.Add(new Claim("session_id", sessionId));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(60),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            var firstIp = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(firstIp))
                return firstIp;
        }
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
