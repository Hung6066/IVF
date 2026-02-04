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
        // Add DbContext
        services.AddDbContext<IvfDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(IvfDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(3);
                }));

        // Register Repositories
        services.AddScoped<IPatientRepository, PatientRepository>();
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
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
