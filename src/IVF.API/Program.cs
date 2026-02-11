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
    builder.Services.AddHttpClient<IDigitalSigningService, SignServerDigitalSigningService>(client =>
    {
        client.BaseAddress = new Uri(signingOptions.SignServerUrl);
        client.Timeout = TimeSpan.FromSeconds(signingOptions.TimeoutSeconds);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        if (signingOptions.SkipTlsValidation)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        if (!string.IsNullOrEmpty(signingOptions.ClientCertificatePath))
        {
            handler.ClientCertificates.Add(
                new System.Security.Cryptography.X509Certificates.X509Certificate2(
                    signingOptions.ClientCertificatePath,
                    signingOptions.ClientCertificatePassword));
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
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<IVF.API.Hubs.IQueueNotifier, IVF.API.Hubs.QueueNotifier>();
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

// OWASP Secure Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline';");
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
app.MapSeedEndpoints();
app.MapLabEndpoints();
app.MapFormEndpoints();
app.MapConceptEndpoints();
app.MapDigitalSigningEndpoints();
app.MapSigningAdminEndpoints();
app.MapUserSignatureEndpoints();

// Auto-migrate and seed in dev
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(app.Services);
    await FormTemplateSeeder.SeedFormTemplatesAsync(app.Services);
}

// QuestPDF community license
QuestPDF.Settings.License = LicenseType.Community;

app.Run();
