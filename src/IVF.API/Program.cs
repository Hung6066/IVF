using System.Text;
using FluentValidation;
using IVF.API.Authorization;
using IVF.API.Endpoints;
using IVF.API.Middleware;
using IVF.API.Services;
using IVF.Application;
using IVF.Application.Common.Interfaces;
using IVF.Infrastructure;
using IVF.Infrastructure.Persistence;
using IVF.Infrastructure.Seeding;
using IVF.Infrastructure.Services;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ─── Resolve Docker Secrets in configuration ───
// Replace FILE:/path references with actual file contents (for connection strings, etc.)
ResolveDockerSecrets(builder.Configuration);

// Clean Architecture DI
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMemoryCache();

// ─── DataProtection: persistent key storage for private key encryption ───
{
    var dpKeysPath = builder.Configuration["DataProtection:KeysPath"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "keys", "dp");
    Directory.CreateDirectory(dpKeysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
        .SetApplicationName("IVF");
}

// ─── Digital Signing Service (SignServer + EJBCA) ───
var signingSection = builder.Configuration.GetSection(DigitalSigningOptions.SectionName);
builder.Services.Configure<DigitalSigningOptions>(signingSection);
var signingOptions = signingSection.Get<DigitalSigningOptions>() ?? new DigitalSigningOptions();

if (signingOptions.Enabled)
{
    // Validate production security constraints
    if (!builder.Environment.IsDevelopment())
    {
        signingOptions.ValidateProduction();
    }

    // Certificate expiry monitoring (Phase 3)
    builder.Services.AddSingleton<CertificateExpiryMonitorService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<CertificateExpiryMonitorService>());

    // Security compliance audit (Phase 4)
    builder.Services.AddSingleton<SecurityComplianceService>();
    builder.Services.AddSingleton<SecurityAuditService>();

    builder.Services.AddHttpClient<IDigitalSigningService, SignServerDigitalSigningService>(client =>
    {
        client.BaseAddress = new Uri(signingOptions.SignServerUrl);
        client.Timeout = TimeSpan.FromSeconds(signingOptions.TimeoutSeconds);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();

        // TLS validation
        if (signingOptions.SkipTlsValidation)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        else if (!string.IsNullOrEmpty(signingOptions.TrustedCaCertPath)
                 && File.Exists(signingOptions.TrustedCaCertPath))
        {
            // Custom CA validation — trust our internal CA chain
            var trustedCa = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                .LoadCertificateFromFile(signingOptions.TrustedCaCertPath);
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                if (errors == System.Net.Security.SslPolicyErrors.None)
                    return true;

                // Validate against our trusted CA
                if (cert != null)
                {
                    chain ??= new System.Security.Cryptography.X509Certificates.X509Chain();
                    chain.ChainPolicy.ExtraStore.Add(trustedCa);
                    chain.ChainPolicy.RevocationMode =
                        System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags =
                        System.Security.Cryptography.X509Certificates.X509VerificationFlags
                            .AllowUnknownCertificateAuthority;
                    return chain.Build(
                        System.Security.Cryptography.X509Certificates.X509CertificateLoader
                            .LoadCertificate(cert.GetRawCertData()));
                }
                return false;
            };
        }

        // Client certificate for mTLS
        if (!string.IsNullOrEmpty(signingOptions.ClientCertificatePath))
        {
            var certPassword = signingOptions.ResolveClientCertificatePassword();
            handler.ClientCertificates.Add(
                System.Security.Cryptography.X509Certificates.X509CertificateLoader
                    .LoadPkcs12FromFile(signingOptions.ClientCertificatePath, certPassword));
        }

        return handler;
    });
}
else
{
    builder.Services.AddSingleton<IDigitalSigningService, StubDigitalSigningService>();
}

// JSON Options
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    options.SerializerOptions.Converters.Add(new IVF.API.Converters.UtcDateTimeConverter());
});

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };

        // Support SignalR authentication via query string token
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Admin policies
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

    // Medical staff policies
    options.AddPolicy("MedicalStaff", policy => policy.RequireRole("Admin", "Doctor", "Nurse", "LabTech", "Embryologist"));
    options.AddPolicy("DoctorOrAdmin", policy => policy.RequireRole("Admin", "Doctor", "Director"));

    // Department-specific policies
    options.AddPolicy("LabAccess", policy => policy.RequireRole("Admin", "LabTech", "Doctor", "Embryologist"));
    options.AddPolicy("EmbryologyAccess", policy => policy.RequireRole("Admin", "Embryologist", "Doctor"));
    options.AddPolicy("AndrologyAccess", policy => policy.RequireRole("Admin", "LabTech", "Doctor"));

    // Billing policies
    options.AddPolicy("BillingAccess", policy => policy.RequireRole("Admin", "Cashier", "Director", "Receptionist"));

    // Queue management policies
    options.AddPolicy("QueueManagement", policy => policy.RequireRole("Admin", "Receptionist", "Nurse"));

    // Reports policies
    options.AddPolicy("ReportsAccess", policy => policy.RequireRole("Admin", "Director", "Doctor"));
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100, // 100 requests
                Window = TimeSpan.FromMinutes(1), // Per minute
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));

    // Signing-specific rate limit (Phase 3) — protect SignServer from abuse
    options.AddFixedWindowLimiter("signing", limiterOptions =>
    {
        limiterOptions.AutoReplenishment = true;
        limiterOptions.PermitLimit = signingOptions.SigningRateLimitPerMinute;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2;
    });

    // Strict limit for certificate provisioning (expensive operation)
    options.AddFixedWindowLimiter("signing-provision", limiterOptions =>
    {
        limiterOptions.AutoReplenishment = true;
        limiterOptions.PermitLimit = 3;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    // Zero Trust: Strict rate limit for auth endpoints (brute force prevention)
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.AutoReplenishment = true;
        limiterOptions.PermitLimit = 10; // 10 login attempts per minute per IP
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0; // No queuing for auth — immediate reject
    });

    // Zero Trust: Sensitive operation rate limit
    options.AddFixedWindowLimiter("sensitive", limiterOptions =>
    {
        limiterOptions.AutoReplenishment = true;
        limiterOptions.PermitLimit = 30; // 30 requests per minute for admin/security endpoints
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2;
    });
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<IVF.API.Hubs.IQueueNotifier, IVF.API.Hubs.QueueNotifier>();
builder.Services.AddSingleton<IVF.API.Services.BackupCompressionService>();
builder.Services.AddSingleton<IVF.API.Services.BackupRestoreService>();
builder.Services.AddSingleton<IVF.API.Services.BackupSchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IVF.API.Services.BackupSchedulerService>());

// ─── Data Backup Services (PostgreSQL + MinIO) ───
builder.Services.AddSingleton<IVF.API.Services.BackupIntegrityService>();
builder.Services.AddSingleton<IVF.API.Services.DatabaseBackupService>();
builder.Services.AddSingleton<IVF.API.Services.MinioBackupService>();
builder.Services.AddSingleton<IVF.API.Services.DataBackupService>();
builder.Services.AddSingleton<IVF.API.Services.DataBackupSchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IVF.API.Services.DataBackupSchedulerService>());

// ─── 3-2-1 Backup Architecture (WAL, Replication, Compliance) ───
builder.Services.AddSingleton<IVF.API.Services.WalBackupService>();
builder.Services.AddSingleton<IVF.API.Services.ReplicationMonitorService>();
builder.Services.AddSingleton<IVF.API.Services.BackupComplianceService>();
builder.Services.AddSingleton<IVF.API.Services.WalBackupSchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IVF.API.Services.WalBackupSchedulerService>());

// ─── Cloud Backup Provider Factory (dynamic, DB-backed) ───
{
    var cloudSection = builder.Configuration.GetSection(IVF.API.Services.CloudBackupSettings.SectionName);
    builder.Services.Configure<IVF.API.Services.CloudBackupSettings>(cloudSection);
}
builder.Services.AddSingleton<IVF.API.Services.CloudBackupProviderFactory>();

// ─── Cloud Replication (PostgreSQL + MinIO external sync) ───
builder.Services.AddSingleton<IVF.API.Services.CloudReplicationService>();
builder.Services.AddSingleton<IVF.API.Services.CloudReplicationSchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IVF.API.Services.CloudReplicationSchedulerService>());

// ─── Key Vault & Zero Trust ───
builder.Services.AddSingleton<IVF.Application.Common.Interfaces.IKeyVaultService, IVF.Infrastructure.Services.AzureKeyVaultService>();
builder.Services.AddScoped<IVF.Application.Common.Interfaces.IVaultSecretService, IVF.Infrastructure.Services.VaultSecretService>();
builder.Services.AddScoped<IVF.Application.Common.Interfaces.ISecretRotationService, IVF.Infrastructure.Services.SecretRotationService>();
builder.Services.AddScoped<IVF.Application.Common.Interfaces.IZeroTrustService, IVF.Infrastructure.Services.ZeroTrustService>();
builder.Services.AddScoped<IVF.Application.Common.Interfaces.ICurrentUserService, IVF.API.Services.CurrentUserService>();
builder.Services.AddScoped<IVF.Application.Common.Services.IFieldAccessService, IVF.Application.Common.Services.FieldAccessService>();

// ─── Zero Trust Security Services (Google BeyondCorp + Microsoft Sentinel + AWS GuardDuty) ───
builder.Services.AddScoped<IVF.Application.Common.Interfaces.ISecurityEventService, IVF.Infrastructure.Services.SecurityEventService>();
builder.Services.AddScoped<IVF.Application.Common.Interfaces.IThreatDetectionService, IVF.Infrastructure.Services.ThreatDetectionService>();
builder.Services.AddScoped<IVF.Application.Common.Interfaces.IDeviceFingerprintService, IVF.Infrastructure.Services.DeviceFingerprintService>();
builder.Services.AddScoped<IVF.Application.Common.Interfaces.IAdaptiveSessionService, IVF.Infrastructure.Services.AdaptiveSessionService>();

// ─── Vault Policy Authorization ───
builder.Services.AddVaultPolicyAuthorization();

// ─── Certificate Authority & auto-renewal ───
builder.Services.AddSingleton<IVF.API.Services.CertificateAuthorityService>();
builder.Services.AddHostedService<IVF.API.Services.CertAutoRenewalService>();
// Conditional Service Registration based on OS
if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
{
    builder.Services.AddSingleton<IVF.API.Services.BiometricMatcherService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<IVF.API.Services.BiometricMatcherService>());
    builder.Services.AddSingleton<IVF.Application.Common.Interfaces.IBiometricMatcher>(sp => sp.GetRequiredService<IVF.API.Services.BiometricMatcherService>());
}
else
{
    // Use Stub service on Mac/Linux to avoid loading Windows-only DLLs
    builder.Services.AddSingleton<IVF.API.Services.StubBiometricMatcherService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<IVF.API.Services.StubBiometricMatcherService>());
    builder.Services.AddSingleton<IVF.Application.Common.Interfaces.IBiometricMatcher>(sp => sp.GetRequiredService<IVF.API.Services.StubBiometricMatcherService>());
}
builder.Services.AddScoped<IVF.API.Services.SignedPdfGenerationService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("AllowAngular", policy =>
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials());
    }
    else
    {
        // Production: Zero Trust — explicit origin whitelist only
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["https://ivf.clinic"];
        options.AddPolicy("AllowAngular", policy =>
            policy.WithOrigins(allowedOrigins)
                  .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
                  .WithHeaders("Authorization", "Content-Type", "X-Vault-Token", "X-API-Key",
                               "X-Device-Fingerprint", "X-Session-Id", "X-Correlation-Id",
                               "X-Requested-With")
                  .AllowCredentials()
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
    }
});

var app = builder.Build();

// Enable CORS immediately
app.UseCors("AllowAngular");

// Validation exception handler
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        if (exception is ValidationException validationException)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                errors = validationException.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            });
        }
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

// OWASP Secure Headers (Phase 1-4) + Phase 5 Zero Trust Compliance Hardening
app.Use(async (context, next) =>
{
    // Phase 1: Core OWASP headers
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "0"); // Modern browsers: CSP replaces this
    context.Response.Headers.Append("Referrer-Policy", "no-referrer"); // Strictest — no referrer info leaked

    // Phase 5: Enhanced CSP with strict directives — aligned with Google/Microsoft security standards
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'none'; " +                              // Deny everything by default
        "script-src 'self'; " +                                // Scripts only from same origin
        "style-src 'self' 'unsafe-inline'; " +                 // Allow inline styles for Angular
        "img-src 'self' data: blob:; " +                       // Images from self, data URIs, blobs
        "font-src 'self'; " +                                  // Fonts from self only
        "connect-src 'self' wss: ws:; " +                      // API + WebSocket connections
        "media-src 'none'; " +                                 // No media
        "object-src 'none'; " +                                // No plugins
        "frame-src 'none'; " +                                 // No iframes
        "frame-ancestors 'none'; " +                           // Cannot be framed
        "base-uri 'self'; " +                                  // Prevent base tag hijacking
        "form-action 'self'; " +                               // Forms submit to self only
        "upgrade-insecure-requests; " +                        // Upgrade HTTP to HTTPS
        "block-all-mixed-content; " +                          // Block mixed HTTP/HTTPS content
        "require-trusted-types-for 'script';");                // Trusted Types for DOM XSS prevention

    // Phase 4: HSTS — enforce HTTPS (max-age=2 years, includeSubDomains, preload-ready)
    if (context.Request.IsHttps || app.Environment.IsProduction())
    {
        context.Response.Headers.Append("Strict-Transport-Security",
            "max-age=63072000; includeSubDomains; preload");
    }

    // Phase 4: Permissions-Policy — restrict ALL browser features (strictest possible)
    context.Response.Headers.Append("Permissions-Policy",
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), " +
        "accelerometer=(), gyroscope=(), magnetometer=(), autoplay=(), " +
        "ambient-light-sensor=(), battery=(), display-capture=(), " +
        "document-domain=(), encrypted-media=(), execution-while-not-rendered=(), " +
        "execution-while-out-of-viewport=(), fullscreen=(self), " +
        "gamepad=(), hid=(), idle-detection=(), interest-cohort=(), " +
        "keyboard-map=(), local-fonts=(), midi=(), otp-credentials=(), " +
        "picture-in-picture=(), publickey-credentials-get=(self), " +
        "screen-wake-lock=(), serial=(), speaker-selection=(), " +
        "sync-xhr=(), web-share=(), xr-spatial-tracking=()");

    // Phase 4+5: Cross-Origin isolation headers
    context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
    context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
    context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");

    // Phase 5: Zero Trust response headers
    context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "none");   // Block Flash/PDF cross-domain
    context.Response.Headers.Append("X-Download-Options", "noopen");                 // IE download protection
    context.Response.Headers.Append("X-DNS-Prefetch-Control", "off");                // Prevent DNS prefetch leaks
    context.Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate, private");  // No caching of sensitive API responses
    context.Response.Headers.Append("Pragma", "no-cache");

    // Remove server identification headers (information disclosure prevention)
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Remove("X-Powered-By");
    context.Response.Headers.Remove("X-AspNet-Version");
    context.Response.Headers.Remove("X-AspNetMvc-Version");

    await next();
});

// app.UseCors("AllowAngular"); // Moved to top
app.UseVaultTokenAuth(); // Vault token authentication (X-Vault-Token header)
app.UseApiKeyAuth(); // API key authentication (X-API-Key header or apiKey query param)
app.UseAuthentication();
app.UseAuthorization();
app.UseZeroTrust(); // Zero Trust continuous verification (Google BeyondCorp + Microsoft CAE + AWS GuardDuty)
app.UseRateLimiter(); // Enable Rate Limiting

// SignalR Hubs
app.MapHub<IVF.API.Hubs.QueueHub>("/hubs/queue");
app.MapHub<IVF.API.Hubs.NotificationHub>("/hubs/notifications");
app.MapHub<IVF.API.Hubs.FingerprintHub>("/hubs/fingerprint");
app.MapHub<IVF.API.Hubs.BackupHub>("/hubs/backup");

// Register Endpoints
app.MapAuthEndpoints();
app.MapPatientEndpoints();
app.MapPatientBiometricsEndpoints();
app.MapCoupleEndpoints();
app.MapCycleEndpoints();
app.MapCyclePhaseEndpoints();
app.MapQueueEndpoints();
app.MapUltrasoundEndpoints();
app.MapEmbryoEndpoints();
app.MapAndrologyEndpoints();
app.MapSpermBankEndpoints();
app.MapBillingEndpoints();
app.MapReportEndpoints();
app.MapAppointmentEndpoints();
app.MapNotificationEndpoints();
app.MapAuditEndpoints();
app.MapDoctorEndpoints();
app.MapUserEndpoints();
app.MapServiceCatalogEndpoints();
app.MapMenuEndpoints();
app.MapPermissionDefinitionEndpoints();
app.MapSeedEndpoints();
app.MapLabEndpoints();
app.MapFormEndpoints();
app.MapConceptEndpoints();
app.MapDigitalSigningEndpoints();
app.MapSigningAdminEndpoints();
app.MapBackupRestoreEndpoints();
app.MapDataBackupEndpoints();
app.MapDataBackupStrategyEndpoints();
app.MapBackupComplianceEndpoints();
app.MapCloudReplicationEndpoints();
app.MapCertificateAuthorityEndpoints();
app.MapUserSignatureEndpoints();
app.MapPatientDocumentEndpoints();
app.MapDocumentSignatureEndpoints();
app.MapKeyVaultEndpoints();
app.MapZeroTrustEndpoints();
app.MapSecurityEventEndpoints(); // Zero Trust security monitoring dashboard

// ── Config seeders: run in every environment (idempotent, no demo data) ──────
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
    await db.Database.MigrateAsync();
    await BackupStrategySeeder.SeedAsync(db);        // default 3-2-1 backup strategies
    await CloudReplicationSeeder.SeedAsync(db);      // default cloud replication config
    await ZTPolicySeeder.SeedAsync(db);              // default Zero Trust policies
    await EncryptionConfigSeeder.SeedAsync(db);      // default encryption configs

    // Permission definitions must always be present
    var permDefRepo = scope.ServiceProvider.GetRequiredService<IPermissionDefinitionRepository>();
    var permUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    await PermissionDefinitionSeeder.SeedAsync(permDefRepo, permUow);
}

// ── Demo/dev data: only in Development ───────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
    await DatabaseSeeder.SeedAsync(app.Services);
    await FormTemplateSeeder.SeedFormTemplatesAsync(app.Services);
    await FormTemplateCodeSeeder.RegenerateCodesAsync(app.Services); // Backfill codes after migration
    await MenuSeeder.SeedAsync(db);                  // default menu items
}

// QuestPDF community license
QuestPDF.Settings.License = LicenseType.Community;

// ─── Vault Configuration Provider ───
// Load vault secrets (under "config/" prefix) into IConfiguration pipeline.
// Secrets refresh every 5 minutes. Example: vault "config/ConnectionStrings/Redis" → IConfiguration["ConnectionStrings:Redis"]
try
{
    var configRoot = app.Services.GetRequiredService<IConfiguration>() as IConfigurationRoot;
    if (configRoot is not null)
    {
        ((IConfigurationBuilder)configRoot).AddVaultSecrets(app.Services, TimeSpan.FromMinutes(5));
        configRoot.Reload();
        app.Logger.LogInformation("Vault configuration provider initialized");
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Vault configuration provider failed to initialize — using static config");
}

app.Run();

// ─── Helper: Resolve FILE:/path references in configuration ───
static void ResolveDockerSecrets(IConfigurationRoot configuration)
{
    var filePattern = new System.Text.RegularExpressions.Regex(
        @"FILE:(/[^\s;,]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    foreach (var kvp in configuration.AsEnumerable())
    {
        if (kvp.Value is null || !kvp.Value.Contains("FILE:", StringComparison.OrdinalIgnoreCase))
            continue;

        var resolved = filePattern.Replace(kvp.Value, match =>
        {
            var filePath = match.Groups[1].Value;
            return File.Exists(filePath) ? File.ReadAllText(filePath).Trim() : match.Value;
        });

        configuration[kvp.Key] = resolved;
    }
}
