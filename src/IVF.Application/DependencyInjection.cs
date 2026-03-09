using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using IVF.Application.Common.Behaviors;

namespace IVF.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Add pipeline behaviors (order matters: logging wraps everything)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FeatureGateBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(VaultPolicyBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ZeroTrustBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FieldAccessBehavior<,>));

        return services;
    }
}

// Validation Behavior moved to Common/Behaviors
