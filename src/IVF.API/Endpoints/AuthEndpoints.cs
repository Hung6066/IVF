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
    // Fido2 serialization options (same as AdvancedSecurityEndpoints)
    private static readonly JsonSerializerOptions _fidoJsonOptions = new(JsonSerializerDefaults.Web);

    // Pending passkey assertions for login
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, AssertionOptions> _loginPendingAssertions = new();

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
            IStepUpAuthService stepUpAuth,
            IContextualAuthService contextualAuth,
            IBehavioralAnalyticsService behavioralAnalytics,
            IIncidentResponseService incidentResponse,
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
                await threatDetection.RecordFailedAttemptAsync(request.Username);

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

            // 3.5. Check tenant status (suspended/cancelled tenants cannot login)
            if (!user.IsPlatformAdmin)
            {
                var tenant = await db.Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == user.TenantId && !t.IsDeleted);
                if (tenant is null || tenant.Status == TenantStatus.Suspended || tenant.Status == TenantStatus.Cancelled)
                {
                    var statusLabel = tenant?.Status.ToString() ?? "NotFound";
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
                        details: $"{{\"reason\":\"tenant_{statusLabel.ToLowerInvariant()}\"}}"));

                    return Results.Json(new
                    {
                        error = "Trung tâm của bạn đã bị tạm ngưng hoạt động. Vui lòng liên hệ quản trị viên.",
                        code = "TENANT_SUSPENDED"
                    }, statusCode: 403);
                }
            }

            // 4. Clear brute force counter on success
            await threatDetection.ClearFailedAttemptsAsync(request.Username);

            // 4.5 Check MFA requirement
            var mfaSettings = await db.UserMfaSettings.FirstOrDefaultAsync(m =>
                m.UserId == user.Id && m.IsMfaEnabled && !m.IsDeleted);
            if (mfaSettings is not null)
            {
                var mfaToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
                var mfaPending = httpContext.RequestServices.GetRequiredService<MfaPendingService>();
                await mfaPending.StoreAsync(mfaToken, new MfaPendingService.MfaPendingInfo(
                    user.Id, user.Username, ipAddress, userAgent,
                    DateTime.UtcNow.AddMinutes(5), mfaSettings.MfaMethod));

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

            // 6.1. Step-up authentication — risk-based MFA escalation
            var stepUpDecision = await stepUpAuth.EvaluateStepUpAsync(user.Id, assessment, deviceTrust);

            // In Development, skip step-up for localhost (allows Playwright E2E tests to run without 2FA)
            // Uses Connection.RemoteIpAddress (not X-Forwarded-For) to prevent spoofing
            var remoteIp = httpContext.Connection.RemoteIpAddress;
            var isLocalhostIp = remoteIp != null && (
                System.Net.IPAddress.IsLoopback(remoteIp) ||
                (remoteIp.IsIPv4MappedToIPv6 && System.Net.IPAddress.IsLoopback(remoteIp.MapToIPv4())));
            var isDev = httpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>().IsDevelopment()
                || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is "Development";
            var skipStepUp = isDev && isLocalhostIp;

            if (!skipStepUp && stepUpDecision.RequiresStepUp)
            {
                if (stepUpDecision.RequiredAction == "block")
                {
                    await securityEvents.LogEventAsync(SecurityEvent.Create(
                        eventType: SecurityEventTypes.StepUpRequired,
                        severity: "Critical",
                        userId: user.Id,
                        username: user.Username,
                        ipAddress: ipAddress,
                        correlationId: httpContext.TraceIdentifier,
                        riskScore: stepUpDecision.RiskScore,
                        isBlocked: true,
                        details: $"{{\"action\":\"block\",\"reason\":\"{stepUpDecision.Reason}\"}}"));
                    return Results.Json(new { error = stepUpDecision.Reason, code = "ZT_STEP_UP_BLOCKED" }, statusCode: 403);
                }

                // Require MFA step-up even if MFA wasn't originally enabled
                var mfaToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
                var mfaPendingStepUp = httpContext.RequestServices.GetRequiredService<MfaPendingService>();
                await mfaPendingStepUp.StoreAsync(mfaToken, new MfaPendingService.MfaPendingInfo(
                    user.Id, user.Username, ipAddress, userAgent,
                    DateTime.UtcNow.AddMinutes(5), "totp"));

                await securityEvents.LogEventAsync(SecurityEvent.Create(
                    eventType: SecurityEventTypes.StepUpRequired,
                    severity: "Medium",
                    userId: user.Id,
                    username: user.Username,
                    ipAddress: ipAddress,
                    correlationId: httpContext.TraceIdentifier,
                    riskScore: stepUpDecision.RiskScore,
                    details: $"{{\"action\":\"{stepUpDecision.RequiredAction}\",\"reason\":\"{stepUpDecision.Reason}\"}}"));

                return Results.Json(new
                {
                    code = "STEP_UP_REQUIRED",
                    mfaToken,
                    requiredAction = stepUpDecision.RequiredAction,
                    reason = stepUpDecision.Reason,
                    user = UserDto.FromEntity(user)
                }, statusCode: 200);
            }

            // 6.2. Contextual authentication — detect unusual login patterns
            var contextResult = await contextualAuth.EvaluateContextAsync(user.Id, ipAddress, userAgent, fingerprint, null);
            if (contextResult.RequiresAdditionalVerification)
            {
                await securityEvents.LogEventAsync(SecurityEvent.Create(
                    eventType: SecurityEventTypes.ContextualAuthTriggered,
                    severity: "Medium",
                    userId: user.Id,
                    username: user.Username,
                    ipAddress: ipAddress,
                    correlationId: httpContext.TraceIdentifier,
                    details: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        triggers = contextResult.Triggers,
                        recommendation = contextResult.RecommendedAction
                    })));
            }

            // 6.3. Behavioral analytics — update user profile and detect anomalies
            await behavioralAnalytics.UpdateProfileAsync(user.Id);
            var behaviorResult = await behavioralAnalytics.AnalyzeLoginAsync(user.Id, ipAddress, userAgent, null, DateTime.UtcNow);
            if (behaviorResult.IsAnomalous)
            {
                var anomalyEvent = SecurityEvent.Create(
                    eventType: SecurityEventTypes.BehaviorAnomalyDetected,
                    severity: behaviorResult.AnomalyScore >= 50 ? "High" : "Medium",
                    userId: user.Id,
                    username: user.Username,
                    ipAddress: ipAddress,
                    correlationId: httpContext.TraceIdentifier,
                    riskScore: (int)behaviorResult.AnomalyScore,
                    details: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        anomalies = behaviorResult.AnomalyFactors,
                        anomalyScore = behaviorResult.AnomalyScore
                    }));
                await securityEvents.LogEventAsync(anomalyEvent);

                // Route anomaly to incident response
                await incidentResponse.ProcessEventAsync(anomalyEvent);
            }

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

            user.UpdateRefreshToken(HashRefreshToken(refreshToken), DateTime.UtcNow.AddDays(7));
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

            SetAuthCookie(httpContext, token);
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
            // Token family tracking is Redis-backed (v8-redis-family) — works across all replicas.
            // Redis provides shared state for token reuse detection (RFC 6749 §10.4).
            // DB-level validation via GetByRefreshTokenAsync below provides additional
            // token reuse protection: after rotation, the old token no longer matches.

            var user = await userRepo.GetByRefreshTokenAsync(HashRefreshToken(request.RefreshToken));
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

            // Carry forward security claims from the current JWT
            var currentAmr = httpContext.User.FindFirst("amr")?.Value
                ?? httpContext.User.FindFirst("http://schemas.microsoft.com/claims/authnmethodsreference")?.Value;
            var currentDeviceFp = httpContext.User.FindFirst("device_fingerprint")?.Value;
            var currentSessionId = httpContext.User.FindFirst("session_id")?.Value;

            var token = GenerateJwtToken(user, config, currentDeviceFp, currentSessionId, currentAmr);
            var refreshToken = GenerateRefreshToken();

            // Register new token in family (tracks lineage for reuse detection)
            tokenFamily.RegisterToken(user.Id, refreshToken, request.RefreshToken);

            user.UpdateRefreshToken(HashRefreshToken(refreshToken), DateTime.UtcNow.AddDays(7));
            await userRepo.UpdateAsync(user);
            await uow.SaveChangesAsync();

            await securityEvents.LogEventAsync(SecurityEvent.Create(
                eventType: SecurityEventTypes.TokenRefresh,
                severity: "Info",
                userId: user.Id,
                username: user.Username,
                ipAddress: GetClientIp(httpContext),
                correlationId: httpContext.TraceIdentifier));

            SetAuthCookie(httpContext, token);
            return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, IUserRepository userRepo) =>
        {
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            var user = await userRepo.GetByIdAsync(Guid.Parse(userId));
            if (user == null) return Results.NotFound();

            var dto = UserDto.FromEntity(user);

            // If impersonating, include actor info
            var isImpersonation = principal.FindFirst("impersonation")?.Value == "true";
            if (isImpersonation)
            {
                var actorId = principal.FindFirst("act_sub")?.Value;
                return Results.Ok(new { user = dto, isImpersonation = true, actorUserId = actorId });
            }

            return Results.Ok(dto);
        }).RequireAuthorization();

        // Get current user's permissions (including delegated)
        group.MapGet("/me/permissions", async (ClaimsPrincipal principal, IUserPermissionRepository permRepo, IPermissionDefinitionRepository permDefRepo, IvfDbContext db) =>
        {
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var uid = Guid.Parse(userId);

            // Admin role gets all permissions automatically
            var role = principal.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var allDefs = await permDefRepo.GetActiveAsync();
                return Results.Ok(allDefs.Select(d => d.Code).ToHashSet());
            }

            var directPermissions = await permRepo.GetByUserIdAsync(uid);
            var directCodes = directPermissions.Select(p => p.PermissionCode).ToHashSet();

            // Include delegated permissions
            var now = DateTime.UtcNow;
            var delegations = await db.PermissionDelegations
                .Where(d => d.ToUserId == uid && !d.IsRevoked && !d.IsDeleted
                    && d.ValidFrom <= now && d.ValidUntil > now)
                .Select(d => d.Permissions)
                .ToListAsync();

            foreach (var permJson in delegations)
            {
                try
                {
                    var perms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(permJson);
                    if (perms is not null)
                        foreach (var p in perms) directCodes.Add(p);
                }
                catch { /* skip invalid JSON */ }
            }

            return Results.Ok(directCodes);
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
            var mfaPendingVerify = httpContext.RequestServices.GetRequiredService<MfaPendingService>();
            var pending = await mfaPendingVerify.RemoveAsync(request.MfaToken);
            if (pending is null)
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
            var token = GenerateJwtToken(user, config, fingerprint, session.SessionId, amr: "mfa");
            var refreshToken = GenerateRefreshToken();

            // Register initial token in family for reuse detection
            var tokenFamilyMfa = httpContext.RequestServices.GetRequiredService<RefreshTokenFamilyService>();
            tokenFamilyMfa.RegisterToken(user.Id, refreshToken, null);

            user.UpdateRefreshToken(HashRefreshToken(refreshToken), DateTime.UtcNow.AddDays(7));
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

            SetAuthCookie(httpContext, token);
            return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
        });

        // ─── MFA Send SMS OTP (for login MFA step) ───
        group.MapPost("/mfa-send-sms", async (
            MfaSendSmsRequest request,
            MfaPendingService mfaPendingSms,
            IvfDbContext db) =>
        {
            var pending = await mfaPendingSms.GetAsync(request.MfaToken);
            if (pending is null)
                return Results.Json(new { error = "Phiên MFA không hợp lệ", code = "MFA_EXPIRED" }, statusCode: 401);

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
            var token = GenerateJwtToken(user, config, fingerprint, session.SessionId, amr: "mfa");
            var refreshToken = GenerateRefreshToken();

            // Register initial token in family for reuse detection
            var tokenFamilyPk = context.RequestServices.GetRequiredService<RefreshTokenFamilyService>();
            tokenFamilyPk.RegisterToken(user.Id, refreshToken, null);

            user.UpdateRefreshToken(HashRefreshToken(refreshToken), DateTime.UtcNow.AddDays(7));
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

            SetAuthCookie(context, token);
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

            ClearAuthCookie(httpContext);
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

    internal static string GenerateJwtToken(User user, IConfiguration config, string? deviceFingerprint = null, string? sessionId = null, string? amr = null)
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
            new("tenant_id", user.TenantId.ToString()),
            new("jti", Guid.NewGuid().ToString()), // Unique token ID for revocation tracking
            new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        // Platform admin flag
        if (user.IsPlatformAdmin)
            claims.Add(new Claim("platform_admin", "true"));

        // Zero Trust: Bind token to device and session
        if (!string.IsNullOrEmpty(deviceFingerprint))
            claims.Add(new Claim("device_fingerprint", deviceFingerprint));
        if (!string.IsNullOrEmpty(sessionId))
            claims.Add(new Claim("session_id", sessionId));
        // Authentication Methods Reference (RFC 8176) — indicates completed auth factors
        if (!string.IsNullOrEmpty(amr))
            claims.Add(new Claim("amr", amr));

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

    internal static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// SHA-256 hash refresh token before storing in DB.
    /// Client receives plaintext; DB stores hash — if DB leaks, tokens are safe.
    /// </summary>
    internal static string HashRefreshToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hash);
    }

    internal static string GetClientIp(HttpContext context)
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

    /// <summary>
    /// Set httpOnly secure cookie with JWT for browser clients (HIPAA compliance).
    /// Cookie name uses __Host- prefix for maximum security (requires Secure, no Domain, Path=/).
    /// </summary>
    internal static void SetAuthCookie(HttpContext context, string token)
    {
        context.Response.Cookies.Append("__Host-ivf-token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromHours(1),
            IsEssential = true
        });
    }

    /// <summary>
    /// Clear the auth cookie on logout.
    /// </summary>
    private static void ClearAuthCookie(HttpContext context)
    {
        context.Response.Cookies.Delete("__Host-ivf-token", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }
}
