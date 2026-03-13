using System.Text;
using FluentValidation;
using IVF.API.Authorization;
using IVF.API.Middleware;
using IVF.API.Services;
using IVF.Application;
using IVF.Application.Common.Interfaces;
using IVF.Infrastructure;
using IVF.Infrastructure.Caching;
using IVF.Infrastructure.Persistence;
using IVF.Infrastructure.Repositories;
using IVF.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Fido2NetLib;
using Serilog;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace IVF.API.Extensions;

/// <summary>
/// Master service collection extensions for enterprise configuration
/// Organizes all DI registrations into logical groups
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all enterprise services in the correct order
    /// </summary>
    public static IServiceCollection AddEnterpriseServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Level 1: Core Infrastructure
        services.AddCoreInfrastructure(configuration);

        // Level 2: Database & Caching
        services.AddEnterpriseDatabase(configuration);
        services.AddEnterpriseCaching(configuration);

        // Level 3: Resilience
        services.AddResiliencePolicies();

        // Level 4: Authentication & Authorization
        services.AddEnterpriseAuthentication(configuration, environment);
        services.AddEnterpriseAuthorization();
        services.AddEnterpriseSecurity(configuration);

        // Level 5: Application Services
        services.AddApplicationServices(configuration, environment);

        // Level 6: Background Services
        services.AddBackgroundServices(configuration);

        // Level 7: API Services
        services.AddApiServices(configuration);

        return services;
    }

    /// <summary>
    /// Level 1: Core Infrastructure (HttpContext, Logging, etc.)
    /// </summary>
    public static IServiceCollection AddCoreInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        // Clean Architecture layers
        services.AddApplication();
        services.AddInfrastructure(configuration);

        // JSON serialization options
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            options.SerializerOptions.Converters.Add(new IVF.API.Converters.UtcDateTimeConverter());
            options.SerializerOptions.Converters.Add(new IVF.API.Converters.NullableGuidConverter());
        });

        // Data Protection
        var dpKeysPath = configuration["DataProtection:KeysPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "keys", "dp");
        Directory.CreateDirectory(dpKeysPath);

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
            .SetApplicationName("IVF");

        // FIDO2/WebAuthn
        services.AddFido2(options =>
        {
            options.ServerDomain = configuration["Fido2:ServerDomain"] ?? "localhost";
            options.ServerName = configuration["Fido2:ServerName"] ?? "IVF System";
            options.Origins = configuration.GetSection("Fido2:Origins").Get<HashSet<string>>()
                ?? new HashSet<string> { "https://localhost:4200" };
        });

        // QuestPDF License
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        return services;
    }

    /// <summary>
    /// Level 4: Enterprise Authentication
    /// </summary>
    public static IServiceCollection AddEnterpriseAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // JWT Key Service
        var jwtKeyService = new JwtKeyService(
            configuration,
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<JwtKeyService>());
        services.AddSingleton(jwtKeyService);

        // Security Services
        services.AddSingleton<JwtKeyService>();
        services.AddSingleton<RefreshTokenFamilyService>(sp =>
            new RefreshTokenFamilyService(
                sp.GetRequiredService<ILogger<RefreshTokenFamilyService>>(),
                sp.GetService<IConnectionMultiplexer>()));

        services.AddSingleton<MfaPendingService>(sp =>
            new MfaPendingService(
                sp.GetRequiredService<ILogger<MfaPendingService>>(),
                sp.GetService<IConnectionMultiplexer>()));

        services.AddSingleton<PasswordPolicyService>();
        services.AddSingleton<DeviceFingerprintService>();

        // JWT Bearer Authentication
        var jwtSettings = configuration.GetSection("JwtSettings");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                    IssuerSigningKey = jwtKeyService.ValidationKey,
                    ClockSkew = TimeSpan.Zero,
                    ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // SignalR: token via query string
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                            return Task.CompletedTask;
                        }

                        // Browser: httpOnly cookie fallback
                        if (string.IsNullOrEmpty(context.Request.Headers.Authorization)
                            && context.Request.Cookies.TryGetValue("__Host-ivf-token", out var cookieToken))
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// Level 4: Enterprise Authorization
    /// </summary>
    public static IServiceCollection AddEnterpriseAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Admin policies
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            options.AddPolicy("SuperAdmin", policy => policy.RequireRole("Admin").RequireClaim("level", "super"));

            // Medical staff policies
            options.AddPolicy("MedicalStaff", policy =>
                policy.RequireRole("Admin", "Doctor", "Nurse", "LabTech", "Embryologist"));
            options.AddPolicy("DoctorOrAdmin", policy =>
                policy.RequireRole("Admin", "Doctor", "Director"));

            // Department-specific policies
            options.AddPolicy("LabAccess", policy =>
                policy.RequireRole("Admin", "LabTech", "Doctor", "Embryologist"));
            options.AddPolicy("EmbryologyAccess", policy =>
                policy.RequireRole("Admin", "Embryologist", "Doctor"));
            options.AddPolicy("AndrologyAccess", policy =>
                policy.RequireRole("Admin", "LabTech", "Doctor"));

            // Billing policies
            options.AddPolicy("BillingAccess", policy =>
                policy.RequireRole("Admin", "Cashier", "Director", "Receptionist"));

            // Queue management policies
            options.AddPolicy("QueueManagement", policy =>
                policy.RequireRole("Admin", "Receptionist", "Nurse"));

            // Reports policies
            options.AddPolicy("ReportsAccess", policy =>
                policy.RequireRole("Admin", "Director", "Doctor"));

            // Compliance policies
            options.AddPolicy("ComplianceAccess", policy =>
                policy.RequireRole("Admin", "ComplianceOfficer"));

            // Backup policies
            options.AddPolicy("BackupAccess", policy =>
                policy.RequireRole("Admin"));
        });

        return services;
    }

    /// <summary>
    /// Level 5: Application Services
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Digital Signing
        var signingSection = configuration.GetSection(DigitalSigningOptions.SectionName);
        services.Configure<DigitalSigningOptions>(signingSection);
        var signingOptions = signingSection.Get<DigitalSigningOptions>() ?? new DigitalSigningOptions();

        if (signingOptions.Enabled)
        {
            if (!environment.IsDevelopment())
            {
                signingOptions.ValidateProduction();
            }

            services.AddSingleton<CertificateExpiryMonitorService>();
            services.AddHostedService(sp => sp.GetRequiredService<CertificateExpiryMonitorService>());
            services.AddSingleton<SecurityComplianceService>();
            services.AddSingleton<SecurityAuditService>();

            services.AddHttpClient<IDigitalSigningService, SignServerDigitalSigningService>(client =>
            {
                client.BaseAddress = new Uri(signingOptions.SignServerUrl);
                client.Timeout = TimeSpan.FromSeconds(signingOptions.TimeoutSeconds);
            })
            .AddEnterpriseResilienceHandler()
            .ConfigurePrimaryHttpMessageHandler(() => CreateSignServerHandler(signingOptions));
        }
        else
        {
            services.AddSingleton<IDigitalSigningService, StubDigitalSigningService>();
        }

        // Rate Limiting
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.Identity?.Name
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = configuration.GetValue("RateLimiting:GlobalLimit", 1000),
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    }));

            // Signing-specific rate limit
            options.AddFixedWindowLimiter("signing", limiterOptions =>
            {
                limiterOptions.AutoReplenishment = true;
                limiterOptions.PermitLimit = signingOptions.SigningRateLimitPerMinute;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 2;
            });

            // Auth rate limit (stricter)
            options.AddFixedWindowLimiter("auth", limiterOptions =>
            {
                limiterOptions.AutoReplenishment = true;
                limiterOptions.PermitLimit = 10;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
            });

            // Login rate limit (strictest)
            options.AddFixedWindowLimiter("login", limiterOptions =>
            {
                limiterOptions.AutoReplenishment = true;
                limiterOptions.PermitLimit = 5;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
            });

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests",
                    retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                        ? retryAfter.TotalSeconds
                        : 60
                }, token);
            };
        });

        // SignalR
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.MaximumReceiveMessageSize = 64 * 1024; // 64KB
            options.StreamBufferCapacity = 10;
            options.MaximumParallelInvocationsPerClient = 1;
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        });

        // CORS
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200", "https://localhost:4200" };

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders("X-Correlation-Id", "X-Request-Id");
            });
        });

        return services;
    }

    /// <summary>
    /// Level 6: Background Services
    /// </summary>
    public static IServiceCollection AddBackgroundServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Distributed Lock Service
        services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();

        // Cache Invalidation
        services.AddSingleton<ICacheInvalidationPublisher, RedisCacheInvalidationPublisher>();
        services.AddHostedService<CacheInvalidationSubscriber>();

        // Database Metrics
        services.AddHostedService<DatabaseMetricsCollector>();

        // Backup Services (conditionally)
        if (configuration.GetValue("Backup:Enabled", true))
        {
            services.AddHostedService<BackupSchedulerService>();
            services.AddHostedService<WalBackupSchedulerService>();
        }

        // Compliance Services
        if (configuration.GetValue("Compliance:Enabled", true))
        {
            services.AddHostedService<ComplianceScanSchedulerService>();
            services.AddHostedService<CtLogMonitorService>();
        }

        // Secret Rotation
        if (configuration.GetValue("Vault:RotationEnabled", false))
        {
            services.AddHostedService<VaultLeaseMaintenanceService>();
        }

        // Infrastructure Monitoring
        if (configuration.GetValue("Infrastructure:MonitoringEnabled", true))
        {
            services.AddHostedService<SwarmAutoHealingService>();
        }

        return services;
    }

    /// <summary>
    /// Level 7: API Services
    /// </summary>
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Swagger/OpenAPI
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Generic Repository Registration
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));

        // Response Compression
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        });

        services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Fastest;
        });

        return services;
    }

    private static HttpClientHandler CreateSignServerHandler(DigitalSigningOptions options)
    {
        var handler = new HttpClientHandler();

        if (options.SkipTlsValidation)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        else if (!string.IsNullOrEmpty(options.TrustedCaCertPath) && File.Exists(options.TrustedCaCertPath))
        {
            var trustedCa = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                .LoadCertificateFromFile(options.TrustedCaCertPath);

            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                if (errors == System.Net.Security.SslPolicyErrors.None)
                    return true;

                if (cert != null)
                {
                    chain ??= new System.Security.Cryptography.X509Certificates.X509Chain();
                    chain.ChainPolicy.ExtraStore.Add(trustedCa);
                    chain.ChainPolicy.RevocationMode =
                        System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags =
                        System.Security.Cryptography.X509Certificates.X509VerificationFlags.AllowUnknownCertificateAuthority;
                    return chain.Build(
                        System.Security.Cryptography.X509Certificates.X509CertificateLoader
                            .LoadCertificate(cert.GetRawCertData()));
                }
                return false;
            };
        }

        if (!string.IsNullOrEmpty(options.ClientCertificatePath))
        {
            var certPassword = options.ResolveClientCertificatePassword();
            handler.ClientCertificates.Add(
                System.Security.Cryptography.X509Certificates.X509CertificateLoader
                    .LoadPkcs12FromFile(options.ClientCertificatePath, certPassword));
        }

        return handler;
    }
}

/// <summary>
/// Distributed Lock Service Interface
/// </summary>
public interface IDistributedLockService
{
    Task<IDisposable?> AcquireLockAsync(string resource, TimeSpan expiry, CancellationToken ct = default);
    Task<bool> TryAcquireLockAsync(string resource, TimeSpan expiry, CancellationToken ct = default);
    Task ReleaseLockAsync(string resource, CancellationToken ct = default);
}

/// <summary>
/// Redis-based distributed lock implementation
/// </summary>
public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisDistributedLockService> _logger;
    private readonly string _instanceId;

    public RedisDistributedLockService(
        IConnectionMultiplexer? redis,
        ILogger<RedisDistributedLockService> logger)
    {
        _redis = redis;
        _logger = logger;
        _instanceId = $"{Environment.MachineName}:{Environment.ProcessId}";
    }

    public async Task<IDisposable?> AcquireLockAsync(string resource, TimeSpan expiry, CancellationToken ct = default)
    {
        if (_redis == null)
        {
            _logger.LogWarning("Redis not available, using in-memory lock");
            return new InMemoryLock(resource);
        }

        var db = _redis.GetDatabase();
        var lockKey = $"lock:{resource}";
        var lockValue = $"{_instanceId}:{Guid.NewGuid()}";

        var acquired = await db.StringSetAsync(
            lockKey,
            lockValue,
            expiry,
            When.NotExists);

        if (acquired)
        {
            _logger.LogDebug("Acquired lock on {Resource}", resource);
            return new RedisLock(db, lockKey, lockValue, _logger);
        }

        _logger.LogDebug("Failed to acquire lock on {Resource}", resource);
        return null;
    }

    public async Task<bool> TryAcquireLockAsync(string resource, TimeSpan expiry, CancellationToken ct = default)
    {
        var @lock = await AcquireLockAsync(resource, expiry, ct);
        return @lock != null;
    }

    public async Task ReleaseLockAsync(string resource, CancellationToken ct = default)
    {
        if (_redis == null) return;

        var db = _redis.GetDatabase();
        var lockKey = $"lock:{resource}";
        await db.KeyDeleteAsync(lockKey);
    }

    private class RedisLock : IDisposable
    {
        private readonly StackExchange.Redis.IDatabase _db;
        private readonly string _key;
        private readonly string _value;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private bool _disposed;

        public RedisLock(StackExchange.Redis.IDatabase db, string key, string value, Microsoft.Extensions.Logging.ILogger logger)
        {
            _db = db;
            _key = key;
            _value = value;
            _logger = logger;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // Only release if we still own the lock
                var currentValue = _db.StringGet(_key);
                if (currentValue == _value)
                {
                    _db.KeyDelete(_key);
                    _logger.LogDebug("Released lock {Key}", _key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release lock {Key}", _key);
            }
        }
    }

    private class InMemoryLock : IDisposable
    {
        private static readonly HashSet<string> _locks = new();
        private readonly string _resource;

        public InMemoryLock(string resource)
        {
            _resource = resource;
            lock (_locks)
            {
                _locks.Add(resource);
            }
        }

        public void Dispose()
        {
            lock (_locks)
            {
                _locks.Remove(_resource);
            }
        }
    }
}
