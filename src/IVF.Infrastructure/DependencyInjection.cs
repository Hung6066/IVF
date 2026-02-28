using IVF.Application.Common.Interfaces;
using IVF.Infrastructure.Persistence;
using IVF.Infrastructure.Repositories;
using IVF.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace IVF.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register AuditInterceptor
        services.AddScoped<AuditInterceptor>();

        // Add DbContext with AuditInterceptor
        services.AddDbContext<IvfDbContext>((sp, options) =>
        {
            var auditInterceptor = sp.GetRequiredService<AuditInterceptor>();
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(IvfDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(3);
                })
                .AddInterceptors(auditInterceptor);
        });

        // Register Repositories
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IPatientBiometricsRepository, PatientBiometricsRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IQueueTicketRepository, QueueTicketRepository>();
        services.AddScoped<ITreatmentCycleRepository, TreatmentCycleRepository>();
        services.AddScoped<ICoupleRepository, CoupleRepository>();
        services.AddScoped<IUltrasoundRepository, UltrasoundRepository>();
        services.AddScoped<IEmbryoRepository, EmbryoRepository>();
        services.AddScoped<ICryoLocationRepository, CryoLocationRepository>();
        services.AddScoped<ISemenAnalysisRepository, SemenAnalysisRepository>();
        services.AddScoped<ISpermDonorRepository, SpermDonorRepository>();
        services.AddScoped<ISpermSampleRepository, SpermSampleRepository>();
        services.AddScoped<ISpermWashingRepository, SpermWashingRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUserPermissionRepository, UserPermissionRepository>();
        services.AddScoped<IServiceCatalogRepository, ServiceCatalogRepository>();
        services.AddScoped<IMenuItemRepository, MenuItemRepository>();
        services.AddScoped<IPermissionDefinitionRepository, PermissionDefinitionRepository>();
        services.AddScoped<ICyclePhaseDataRepository, CyclePhaseDataRepository>();
        services.AddScoped<IFormRepository, FormRepository>();
        services.AddScoped<IConceptRepository, ConceptRepository>();
        services.AddScoped<IPatientDocumentRepository, PatientDocumentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register Services
        services.AddScoped<INotificationService, Services.NotificationService>();
        services.AddScoped<IFlowSeeder, FlowSeeder>();

        // Key Vault & Zero Trust
        services.AddScoped<IApiKeyManagementRepository, ApiKeyManagementRepository>();
        services.AddScoped<IVaultRepository, VaultRepository>();

        // ─── MinIO Object Storage (S3-compatible) ───
        var minioSection = configuration.GetSection(MinioOptions.SectionName);
        services.Configure<MinioOptions>(minioSection);
        var minioOpts = minioSection.Get<MinioOptions>() ?? new MinioOptions();

        services.AddSingleton<IMinioClient>(_ =>
        {
            var client = new MinioClient()
                .WithEndpoint(minioOpts.Endpoint)
                .WithCredentials(minioOpts.AccessKey, minioOpts.SecretKey);

            if (minioOpts.UseSSL)
            {
                client.WithSSL();
                var handler = new HttpClientHandler();
                if (!string.IsNullOrWhiteSpace(minioOpts.TrustedCaCertPath) && File.Exists(minioOpts.TrustedCaCertPath))
                {
                    // Validate MinIO TLS against our private CA certificate
                    var caCert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(minioOpts.TrustedCaCertPath);
                    handler.ServerCertificateCustomValidationCallback = (_, cert, chain, errors) =>
                    {
                        if (errors == System.Net.Security.SslPolicyErrors.None) return true;
                        if (cert == null || chain == null) return false;
                        chain.ChainPolicy.TrustMode = System.Security.Cryptography.X509Certificates.X509ChainTrustMode.CustomRootTrust;
                        chain.ChainPolicy.CustomTrustStore.Add(caCert);
                        return chain.Build(System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(cert.GetRawCertData()));
                    };
                }
                else
                {
                    // Fallback: accept internal CA certs (development only)
                    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                }
                client.WithHttpClient(new HttpClient(handler));
            }

            return client.Build();
        });

        services.AddSingleton<IObjectStorageService, MinioObjectStorageService>();
        services.AddSingleton<IFileStorageService, MinioFileStorageService>();

        // Partition Maintenance (auto-creates future partitions)
        services.AddHostedService<PartitionMaintenanceService>();

        // Redis Configuration (High-Performance Matcher Cache)
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        // Ensure abortConnect=false so app starts even if Redis is down
        if (!redisConnectionString.Contains("abortConnect=", StringComparison.OrdinalIgnoreCase))
        {
            redisConnectionString += ",abortConnect=false";
        }

        try
        {
            var multiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString);
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(multiplexer);
        }
        catch (Exception)
        {
            // Should not happen with abortConnect=false, but safe guard
        }

        return services;
    }
}

