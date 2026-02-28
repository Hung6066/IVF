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
                // Accept self-signed / internal CA certs for MinIO TLS
                client.WithHttpClient(new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                }));
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

