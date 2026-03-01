using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IVF.Infrastructure.Services.Kms;

/// <summary>
/// Registers the appropriate IKmsProvider based on configuration.
/// Config key: "KmsProvider" — values: "Azure", "Local" (default).
/// </summary>
public static class KmsProviderRegistration
{
    public static IServiceCollection AddKmsProvider(this IServiceCollection services, IConfiguration config)
    {
        var provider = config.GetValue<string>("KmsProvider") ?? "Local";

        switch (provider.ToLowerInvariant())
        {
            case "azure":
                services.AddScoped<IKmsProvider, AzureKmsProvider>();
                break;
            default:
                services.AddScoped<IKmsProvider, LocalKmsProvider>();
                break;
        }

        return services;
    }
}
