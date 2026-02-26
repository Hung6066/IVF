using System.Text;
using FluentValidation;
using IVF.API.Endpoints;
using IVF.API.Services;
using IVF.Application;
using IVF.Application.Common.Interfaces;
using IVF.Infrastructure;
using IVF.Infrastructure.Persistence;
using IVF.Infrastructure.Seeding;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Clean Architecture DI
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<IVF.API.Hubs.IQueueNotifier, IVF.API.Hubs.QueueNotifier>();
builder.Services.AddSingleton<IVF.API.Services.BackupCompressionService>();
builder.Services.AddSingleton<IVF.API.Services.BackupRestoreService>();
builder.Services.AddSingleton<IVF.API.Services.BackupSchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IVF.API.Services.BackupSchedulerService>());

// ─── Cloud Backup Provider Factory (dynamic, DB-backed) ───
{
    var cloudSection = builder.Configuration.GetSection(IVF.API.Services.CloudBackupSettings.SectionName);
    builder.Services.Configure<IVF.API.Services.CloudBackupSettings>(cloudSection);
}
builder.Services.AddSingleton<IVF.API.Services.CloudBackupProviderFactory>();
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
    options.AddPolicy("AllowAngular", policy =>
        policy.SetIsOriginAllowed(_ => true) // Allow any origin for dev
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
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

// OWASP Secure Headers (Phase 1-3) + Phase 4 Compliance Hardening
app.Use(async (context, next) =>
{
    // Phase 1: Core OWASP headers
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "0"); // Modern browsers: CSP replaces this
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self';");

    // Phase 4: HSTS — enforce HTTPS (max-age=2 years, includeSubDomains, preload-ready)
    if (context.Request.IsHttps || app.Environment.IsProduction())
    {
        context.Response.Headers.Append("Strict-Transport-Security",
            "max-age=63072000; includeSubDomains; preload");
    }

    // Phase 4: Permissions-Policy — restrict browser features
    context.Response.Headers.Append("Permissions-Policy",
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), " +
        "accelerometer=(), gyroscope=(), magnetometer=(), autoplay=()");

    // Phase 4: Additional hardening headers
    context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
    context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
    context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");

    // Remove server identification header
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Remove("X-Powered-By");

    await next();
});

// app.UseCors("AllowAngular"); // Moved to top
app.UseAuthentication();
app.UseAuthorization();
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
app.MapUserSignatureEndpoints();
app.MapPatientDocumentEndpoints();
app.MapDocumentSignatureEndpoints();

// Auto-migrate and seed in dev
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(app.Services);
    await FormTemplateSeeder.SeedFormTemplatesAsync(app.Services);
    await FormTemplateCodeSeeder.RegenerateCodesAsync(app.Services); // Backfill codes sau migration
    await MenuSeeder.SeedAsync(db); // Seed default menu items

    // Seed permission definitions
    var permDefRepo = scope.ServiceProvider.GetRequiredService<IPermissionDefinitionRepository>();
    var permUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    await PermissionDefinitionSeeder.SeedAsync(permDefRepo, permUow);
}

// QuestPDF community license
QuestPDF.Settings.License = LicenseType.Community;

app.Run();
