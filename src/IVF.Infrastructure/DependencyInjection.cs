using IVF.Application.Common.Interfaces;
using IVF.Infrastructure.Persistence;
using IVF.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<ICyclePhaseDataRepository, CyclePhaseDataRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register Services
        services.AddScoped<INotificationService, Services.NotificationService>();
        services.AddScoped<IFlowSeeder, FlowSeeder>();

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

