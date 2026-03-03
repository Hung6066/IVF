using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fido2NetLib;
using Fido2NetLib.Objects;
using IVF.API.Contracts;
using IVF.API.Services;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using IVF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OtpNet;

namespace IVF.API.Endpoints;

public static class AuthEndpoints
{
    // Pending MFA tokens: mfaToken -> (userId, username, ipAddress, userAgent, expiry)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, MfaPendingInfo> _pendingMfa = new();

    // Fido2 serialization options (same as AdvancedSecurityEndpoints)
    private static readonly JsonSerializerOptions _fidoJsonOptions = new(JsonSerializerDefaults.Web);

    // Pending passkey assertions for login
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, AssertionOptions> _loginPendingAssertions = new();

    private record MfaPendingInfo(Guid UserId, string Username, string IpAddress, string? UserAgent, DateTime ExpiresAt, string MfaMethod);

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
            IvfDbContext db,
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

                    return Results.Json(new
                    {
                        error = "Tài khoản đã bị tạm khóa do đăng nhập sai nhiều lần",
                        code = "BRUTE_FORCE_LOCKED"
                    }, statusCode: 429);
                }

                // Record failed login to enterprise history
                await RecordEnterpriseLoginAsync(httpContext.RequestServices,
                    user, ipAddress, userAgent, "password", false, failureReason: "invalid_credentials");

                return Results.Json(new
                {
                    error = "Sai tên đăng nhập hoặc mật khẩu",
                    code = "INVALID_CREDENTIALS"
                }, statusCode: 401);
            }

            // 3. Check account lockout
            var activeLockout = await db.AccountLockouts.FirstOrDefaultAsync(l =>
                l.UserId == user.Id && !l.IsDeleted && l.UnlocksAt > DateTime.UtcNow);
            if (activeLockout is not null)
            {
                await securityEvents.LogEventAsync(SecurityEvent.Create(
                    eventType: SecurityEventTypes.LoginFailed,
                    severity: "High",
                    userId: user.Id,
                    username: user.Username,
                    ipAddress: ipAddress,
                    userAgent: userAgent,
                    requestPath: "/api/auth/login",
                    requestMethod: "POST",
                    responseStatusCode: 403,
                    correlationId: httpContext.TraceIdentifier,
                    details: $"{{\"reason\":\"account_locked\",\"lockedAt\":\"{activeLockout.LockedAt:O}\",\"unlocksAt\":\"{activeLockout.UnlocksAt:O}\",\"lockReason\":\"{activeLockout.Reason}\"}}"));

                return Results.Json(new
                {
                    error = "Tài khoản đã bị khóa",
                    code = "ACCOUNT_LOCKED",
                    reason = activeLockout.Reason,
                    unlocksAt = activeLockout.UnlocksAt
                }, statusCode: 403);
            }

            // 4. Clear brute force counter on success
            if (threatDetection is ThreatDetectionService tdsSuccess)
                tdsSuccess.ClearFailedAttempts(request.Username);

            // 4.5 Check MFA requirement
            var mfaSettings = await db.UserMfaSettings.FirstOrDefaultAsync(m =>
                m.UserId == user.Id && m.IsMfaEnabled && !m.IsDeleted);
            if (mfaSettings is not null)
            {
                var mfaToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
                _pendingMfa[mfaToken] = new MfaPendingInfo(
                    user.Id, user.Username, ipAddress, userAgent,
                    DateTime.UtcNow.AddMinutes(5), mfaSettings.MfaMethod);

                return Results.Json(new
                {
                    code = "MFA_REQUIRED",
                    mfaToken,
                    mfaMethod = mfaSettings.MfaMethod,
                    user = UserDto.FromEntity(user)
                }, statusCode: 200);
            }

            // 5. Generate device fingerprint
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

            // 6. Check device trust
            var deviceTrust = await deviceFingerprint.CheckDeviceTrustAsync(user.Id, fingerprint);

            // 7. Create adaptive session
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

            // 8. Generate tokens
            var token = GenerateJwtToken(user, config, fingerprint, session.SessionId);
            var refreshToken = GenerateRefreshToken();

            // Register initial token in family for reuse detection
            var tokenFamily = httpContext.RequestServices.GetRequiredService<RefreshTokenFamilyService>();
            tokenFamily.RegisterToken(user.Id, refreshToken, null);

            user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
            await userRepo.UpdateAsync(user);
            await uow.SaveChangesAsync();

            // 9. Log successful login
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

            // 10. Record enterprise login history & session
            await RecordEnterpriseLoginAsync(httpContext.RequestServices, user, ipAddress, userAgent, "password", true,
                assessment.RiskScore, session.SessionId, token);

            return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
        });

        group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            IUserRepository userRepo,
            IUnitOfWork uow,
            IConfiguration config,
            ISecurityEventService securityEvents,
            RefreshTokenFamilyService tokenFamily,
            HttpContext httpContext) =>
        {
            // 1. Validate refresh token family (reuse attack detection)
            var familyValidation = tokenFamily.ValidateToken(request.RefreshToken);
            if (familyValidation.IsReuse)
            {
                // Token reuse detected — revoke entire family and user's refresh token
                if (familyValidation.FamilyId != null)
                    tokenFamily.RevokeFamily(familyValidation.FamilyId);

                // Revoke user's refresh token in DB
                if (familyValidation.UserId.HasValue)
                {
                    var compromisedUser = await userRepo.GetByIdAsync(familyValidation.UserId.Value);
                    if (compromisedUser != null)
                    {
                        compromisedUser.UpdateRefreshToken(null!, DateTime.UtcNow);
                        await userRepo.UpdateAsync(compromisedUser);
                        await uow.SaveChangesAsync();
                    }
                }

                await securityEvents.LogEventAsync(SecurityEvent.Create(
                    eventType: SecurityEventTypes.TokenRevoked,
                    severity: "Critical",
                    ipAddress: GetClientIp(httpContext),
                    requestPath: "/api/auth/refresh",
                    requestMethod: "POST",
                    responseStatusCode: 401,
                    correlationId: httpContext.TraceIdentifier,
                    details: "{\"reason\":\"refresh_token_reuse_attack\"}"));

                return Results.Json(new { error = "Token has been revoked", code = "TOKEN_REUSE_DETECTED" }, statusCode: 401);
            }

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

            // Register new token in family (tracks lineage for reuse detection)
            tokenFamily.RegisterToken(user.Id, refreshToken, request.RefreshToken);

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

        // ─── MFA Verify ───
        group.MapPost("/mfa-verify", async (
            MfaVerifyRequest request,
            IUserRepository userRepo,
            IUnitOfWork uow,
            IConfiguration config,
            ISecurityEventService securityEvents,
            IDeviceFingerprintService deviceFingerprint,
            IAdaptiveSessionService sessionService,
            IvfDbContext db,
            HttpContext httpContext) =>
        {
            if (!_pendingMfa.TryRemove(request.MfaToken, out var pending))
                return Results.Json(new { error = "Phiên xác thực MFA đã hết hạn", code = "MFA_EXPIRED" }, statusCode: 401);

            if (DateTime.UtcNow > pending.ExpiresAt)
                return Results.Json(new { error = "Phiên xác thực MFA đã hết hạn", code = "MFA_EXPIRED" }, statusCode: 401);

            var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m =>
                m.UserId == pending.UserId && m.IsMfaEnabled && !m.IsDeleted);
            if (mfa is null)
                return Results.Json(new { error = "MFA chưa được cấu hình", code = "MFA_NOT_CONFIGURED" }, statusCode: 400);

            // Verify TOTP or SMS code
            bool codeValid;
            if (mfa.MfaMethod == "totp" && !string.IsNullOrEmpty(mfa.TotpSecretKey))
            {
                var secretBytes = Base32Encoding.ToBytes(mfa.TotpSecretKey);
                var totp = new Totp(secretBytes, step: 30, totpSize: 6);
                codeValid = totp.VerifyTotp(request.Code, out _, new VerificationWindow(previous: 1, future: 1));
            }
            else if (mfa.MfaMethod == "sms")
            {
                // Check SMS OTP from AdvancedSecurityEndpoints pending store
                codeValid = AdvancedSecurityEndpoints.ValidateSmsOtp(pending.UserId.ToString(), request.Code);
            }
            else
            {
                return Results.Json(new { error = "Phương thức MFA không hợp lệ", code = "MFA_INVALID_METHOD" }, statusCode: 400);
            }

            if (!codeValid)
            {
                mfa.RecordMfaFailure();
                await db.SaveChangesAsync();
                return Results.Json(new
                {
                    error = "Mã xác thực không đúng",
                    code = "MFA_INVALID_CODE",
                    failedAttempts = mfa.FailedMfaAttempts
                }, statusCode: 401);
            }

            mfa.RecordMfaSuccess();
            await db.SaveChangesAsync();

            // MFA passed — complete login flow (same as normal login steps 5-9)
            var user = await userRepo.GetByIdAsync(pending.UserId);
            if (user is null) return Results.Unauthorized();

            var ipAddress = pending.IpAddress;
            var userAgent = pending.UserAgent;

            var deviceSignals = new DeviceSignals(
                UserAgent: userAgent,
                AcceptLanguage: httpContext.Request.Headers.AcceptLanguage.FirstOrDefault(),
                AcceptEncoding: httpContext.Request.Headers.AcceptEncoding.FirstOrDefault(),
                IpAddress: ipAddress,
                ScreenResolution: null, Timezone: null, Platform: null,
                CookiesEnabled: null, DoNotTrack: null,
                ClientHints: httpContext.Request.Headers["Sec-CH-UA"].FirstOrDefault());

            var fingerprint = await deviceFingerprint.RegisterDeviceAsync(user.Id, deviceSignals);
            var sessionCtx = new RequestSecurityContext(
                UserId: user.Id, Username: user.Username, IpAddress: ipAddress,
                UserAgent: userAgent, DeviceFingerprint: fingerprint,
                Country: null, City: null, RequestPath: "/api/auth/mfa-verify",
                RequestMethod: "POST", SessionId: null,
                CorrelationId: httpContext.TraceIdentifier, Timestamp: DateTime.UtcNow);

            var session = await sessionService.CreateSessionAsync(user.Id, sessionCtx);
            var token = GenerateJwtToken(user, config, fingerprint, session.SessionId);
            var refreshToken = GenerateRefreshToken();

            // Register initial token in family for reuse detection
            var tokenFamilyMfa = httpContext.RequestServices.GetRequiredService<RefreshTokenFamilyService>();
            tokenFamilyMfa.RegisterToken(user.Id, refreshToken, null);

            user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
            await userRepo.UpdateAsync(user);
            await uow.SaveChangesAsync();

            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.LoginSuccess,
                severity: "Info",
                userId: user.Id, username: user.Username,
                ipAddress: ipAddress, userAgent: userAgent,
                deviceFingerprint: fingerprint,
                requestPath: "/api/auth/mfa-verify",
                requestMethod: "POST", responseStatusCode: 200,
                correlationId: httpContext.TraceIdentifier,
                sessionId: session.SessionId,
                details: "{\"mfaVerified\":true}"));

            // Record enterprise login history & session (MFA)
            await RecordEnterpriseLoginAsync(httpContext.RequestServices, user, ipAddress, userAgent, "mfa", true,
                sessionId: session.SessionId, jwtToken: token);

            return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
        });

        // ─── MFA Send SMS OTP (for login MFA step) ───
        group.MapPost("/mfa-send-sms", async (
            MfaSendSmsRequest request,
            IvfDbContext db) =>
        {
            if (!_pendingMfa.TryGetValue(request.MfaToken, out var pending))
                return Results.Json(new { error = "Phiên MFA không hợp lệ", code = "MFA_EXPIRED" }, statusCode: 401);

            if (DateTime.UtcNow > pending.ExpiresAt)
                return Results.Json(new { error = "Phiên MFA đã hết hạn", code = "MFA_EXPIRED" }, statusCode: 401);

            var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m =>
                m.UserId == pending.UserId && m.IsPhoneVerified && !m.IsDeleted);
            if (mfa is null || string.IsNullOrEmpty(mfa.PhoneNumber))
                return Results.Json(new { error = "Chưa có số điện thoại xác minh", code = "NO_PHONE" }, statusCode: 400);

            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            AdvancedSecurityEndpoints.StoreSmsOtp(pending.UserId.ToString(), otp, TimeSpan.FromMinutes(5));

            return Results.Ok(new { message = "OTP đã được gửi", devOtp = otp });
        });

        // ─── Passkey Login (passwordless) ───
        group.MapPost("/passkey-login/begin", async (
            PasskeyLoginBeginRequest request,
            IvfDbContext db,
            IFido2 fido2) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username && !u.IsDeleted);
            if (user is null)
                return Results.Json(new { error = "Người dùng không tồn tại", code = "USER_NOT_FOUND" }, statusCode: 404);

            var credentials = await db.PasskeyCredentials
                .Where(p => p.UserId == user.Id && p.IsActive && !p.IsDeleted)
                .Select(p => new PublicKeyCredentialDescriptor(Convert.FromBase64String(p.CredentialId)))
                .ToListAsync();

            if (credentials.Count == 0)
                return Results.Json(new { error = "Chưa đăng ký Passkey cho tài khoản này", code = "NO_PASSKEYS" }, statusCode: 400);

            var options = fido2.GetAssertionOptions(credentials, UserVerificationRequirement.Preferred);
            _loginPendingAssertions[user.Id.ToString()] = options;

            var json = JsonSerializer.Serialize(new { userId = user.Id, options }, _fidoJsonOptions);
            return Results.Text(json, "application/json");
        });

        group.MapPost("/passkey-login/complete", async (
            HttpContext context,
            IUserRepository userRepo,
            IUnitOfWork uow,
            IConfiguration config,
            ISecurityEventService securityEvents,
            IThreatDetectionService threatDetection,
            IDeviceFingerprintService deviceFingerprint,
            IAdaptiveSessionService sessionService,
            IvfDbContext db,
            IFido2 fido2) =>
        {
            var request = await JsonSerializer.DeserializeAsync<PasskeyLoginCompleteRequest>(context.Request.Body, _fidoJsonOptions);
            if (request is null)
                return Results.BadRequest(new { error = "Invalid request", code = "INVALID_REQUEST" });

            if (!_loginPendingAssertions.TryRemove(request.UserId.ToString(), out var options))
                return Results.Json(new { error = "Phiên xác thực Passkey đã hết hạn", code = "PASSKEY_EXPIRED" }, statusCode: 401);

            var credentialIdBase64 = Convert.ToBase64String(request.AssertionResponse.Id);
            var storedCredential = await db.PasskeyCredentials
                .FirstOrDefaultAsync(p => p.CredentialId == credentialIdBase64 && p.IsActive && !p.IsDeleted);

            if (storedCredential is null)
                return Results.Json(new { error = "Passkey không hợp lệ", code = "PASSKEY_INVALID" }, statusCode: 401);

            var result = await fido2.MakeAssertionAsync(
                request.AssertionResponse, options,
                Convert.FromBase64String(storedCredential.PublicKey),
                storedCredential.SignatureCounter,
                async (args, ct) =>
                {
                    var cred = await db.PasskeyCredentials
                        .FirstOrDefaultAsync(p => p.UserId == request.UserId && p.CredentialId == credentialIdBase64 && !p.IsDeleted, ct);
                    return cred is not null;
                });

            storedCredential.UpdateCounter(result.Counter);
            await db.SaveChangesAsync();

            // Passkey auth successful — check lockout
            var user = await userRepo.GetByIdAsync(request.UserId);
            if (user is null) return Results.Unauthorized();

            var activeLockout = await db.AccountLockouts.FirstOrDefaultAsync(l =>
                l.UserId == user.Id && !l.IsDeleted && l.UnlocksAt > DateTime.UtcNow);
            if (activeLockout is not null)
                return Results.Json(new
                {
                    error = "Tài khoản đã bị khóa",
                    code = "ACCOUNT_LOCKED",
                    reason = activeLockout.Reason,
                    unlocksAt = activeLockout.UnlocksAt
                }, statusCode: 403);

            var ipAddress = GetClientIp(context);
            var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();

            var deviceSignals = new DeviceSignals(
                UserAgent: userAgent,
                AcceptLanguage: context.Request.Headers.AcceptLanguage.FirstOrDefault(),
                AcceptEncoding: context.Request.Headers.AcceptEncoding.FirstOrDefault(),
                IpAddress: ipAddress,
                ScreenResolution: null, Timezone: null, Platform: null,
                CookiesEnabled: null, DoNotTrack: null,
                ClientHints: context.Request.Headers["Sec-CH-UA"].FirstOrDefault());

            var fingerprint = await deviceFingerprint.RegisterDeviceAsync(user.Id, deviceSignals);
            var sessionCtx = new RequestSecurityContext(
                UserId: user.Id, Username: user.Username, IpAddress: ipAddress,
                UserAgent: userAgent, DeviceFingerprint: fingerprint,
                Country: null, City: null, RequestPath: "/api/auth/passkey-login",
                RequestMethod: "POST", SessionId: null,
                CorrelationId: context.TraceIdentifier, Timestamp: DateTime.UtcNow);

            var session = await sessionService.CreateSessionAsync(user.Id, sessionCtx);
            var token = GenerateJwtToken(user, config, fingerprint, session.SessionId);
            var refreshToken = GenerateRefreshToken();

            // Register initial token in family for reuse detection
            var tokenFamilyPk = context.RequestServices.GetRequiredService<RefreshTokenFamilyService>();
            tokenFamilyPk.RegisterToken(user.Id, refreshToken, null);

            user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
            await userRepo.UpdateAsync(user);
            await uow.SaveChangesAsync();

            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.LoginSuccess,
                severity: "Info",
                userId: user.Id, username: user.Username,
                ipAddress: ipAddress, userAgent: userAgent,
                deviceFingerprint: fingerprint,
                requestPath: "/api/auth/passkey-login",
                requestMethod: "POST", responseStatusCode: 200,
                correlationId: context.TraceIdentifier,
                sessionId: session.SessionId,
                details: "{\"method\":\"passkey\"}"));

            // Record enterprise login history & session (passkey)
            await RecordEnterpriseLoginAsync(context.RequestServices, user, ipAddress, userAgent, "passkey", true,
                sessionId: session.SessionId, jwtToken: token);

            return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
        });

        // ─── Logout (record enterprise session end) ───
        group.MapPost("/logout", async (
            ClaimsPrincipal principal,
            IEnterpriseUserRepository enterpriseRepo,
            HttpContext httpContext) =>
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Ok(new { message = "Logged out" });

            // Record logout in login history (find latest active login for this user)
            var recentLogins = await enterpriseRepo.GetUserRecentLoginsAsync(userId, 1);
            var lastLogin = recentLogins.FirstOrDefault(h => h.IsSuccess && h.LogoutAt == null);
            if (lastLogin != null)
            {
                lastLogin.RecordLogout();
                await enterpriseRepo.SaveChangesAsync();
            }

            // Revoke active enterprise sessions
            await enterpriseRepo.RevokeAllSessionsAsync(userId, "User logout", "user");

            return Results.Ok(new { message = "Logged out" });
        }).RequireAuthorization();
    }

    /// <summary>
    /// Records login attempt and session in enterprise tables for analytics and audit.
    /// </summary>
    private static async Task RecordEnterpriseLoginAsync(
        IServiceProvider services,
        User? user,
        string ipAddress,
        string? userAgent,
        string loginMethod,
        bool isSuccess,
        decimal? riskScore = null,
        string? sessionId = null,
        string? jwtToken = null,
        string? failureReason = null)
    {
        try
        {
            var repo = services.GetService<IEnterpriseUserRepository>();
            if (repo == null) return;

            var (deviceType, os, browser) = ParseUserAgent(userAgent);

            // Record login history
            var history = UserLoginHistory.Create(
                userId: user?.Id ?? Guid.Empty,
                loginMethod: loginMethod,
                isSuccess: isSuccess,
                failureReason: failureReason,
                ipAddress: ipAddress,
                userAgent: userAgent,
                country: null,
                city: null,
                deviceType: deviceType,
                operatingSystem: os,
                browser: browser,
                riskScore: riskScore,
                isSuspicious: riskScore > 70);
            await repo.AddLoginHistoryAsync(history);

            // Create session record on successful login
            if (isSuccess && user != null)
            {
                var session = UserSession.Create(
                    userId: user.Id,
                    sessionToken: sessionId ?? Guid.NewGuid().ToString(),
                    expiresAt: DateTime.UtcNow.AddHours(1),
                    ipAddress: ipAddress,
                    userAgent: userAgent,
                    country: null,
                    city: null,
                    deviceType: deviceType,
                    operatingSystem: os,
                    browser: browser);
                await repo.AddSessionAsync(session);

                // Enforce max concurrent sessions (revoke oldest beyond limit)
                const int maxConcurrentSessions = 3;
                var activeSessions = await repo.GetUserSessionsAsync(user.Id, true);
                if (activeSessions.Count >= maxConcurrentSessions)
                {
                    var sessionsToRevoke = activeSessions
                        .OrderByDescending(s => s.LastActivityAt)
                        .Skip(maxConcurrentSessions - 1) // keep newest (maxConcurrentSessions - 1) + the new one
                        .ToList();
                    foreach (var old in sessionsToRevoke)
                    {
                        old.Revoke("Exceeded concurrent session limit", "system");
                    }
                }
            }

            await repo.SaveChangesAsync();
        }
        catch
        {
            // Non-critical — don't break auth flow if enterprise recording fails
        }
    }

    /// <summary>
    /// Simple UA parsing for device type, OS, and browser.
    /// </summary>
    private static (string DeviceType, string OS, string Browser) ParseUserAgent(string? ua)
    {
        if (string.IsNullOrEmpty(ua))
            return ("Unknown", "Unknown", "Unknown");

        // Device type
        string deviceType;
        if (Regex.IsMatch(ua, @"Mobile|Android.*Mobile|iPhone|iPod", RegexOptions.IgnoreCase))
            deviceType = "Mobile";
        else if (Regex.IsMatch(ua, @"iPad|Android(?!.*Mobile)|Tablet", RegexOptions.IgnoreCase))
            deviceType = "Tablet";
        else
            deviceType = "Desktop";

        // OS
        string os;
        if (ua.Contains("Windows NT 10", StringComparison.OrdinalIgnoreCase)) os = "Windows";
        else if (ua.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase)) os = "macOS";
        else if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) && ua.Contains("Android", StringComparison.OrdinalIgnoreCase)) os = "Android";
        else if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase)) os = "Linux";
        else if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase)) os = "iOS";
        else os = "Other";

        // Browser
        string browser;
        if (ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) browser = "Edge";
        else if (ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase) || ua.Contains("Opera", StringComparison.OrdinalIgnoreCase)) browser = "Opera";
        else if (ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) browser = "Chrome";
        else if (ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) browser = "Safari";
        else if (ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) browser = "Firefox";
        else browser = "Other";

        return (deviceType, os, browser);
    }

    private static string GenerateJwtToken(User user, IConfiguration config, string? deviceFingerprint = null, string? sessionId = null)
    {
        var jwtSettings = config.GetSection("JwtSettings");
        var tokenHandler = new JwtSecurityTokenHandler();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("department", user.Department ?? ""),
            new("jti", Guid.NewGuid().ToString()), // Unique token ID for revocation tracking
            new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        // Zero Trust: Bind token to device and session
        if (!string.IsNullOrEmpty(deviceFingerprint))
            claims.Add(new Claim("device_fingerprint", deviceFingerprint));
        if (!string.IsNullOrEmpty(sessionId))
            claims.Add(new Claim("session_id", sessionId));

        // RS256 asymmetric signing — private key signs, public key verifies
        // Even if the validation key is exposed, tokens cannot be forged
        var keyService = JwtKeyService.Instance ?? throw new InvalidOperationException("JwtKeyService not initialized");

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(60),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = keyService.SigningCredentials
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
