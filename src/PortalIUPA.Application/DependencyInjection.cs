using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using PortalIUPA.Application.Behaviors;
using PortalIUPA.Application.Services;

namespace PortalIUPA.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var ensamblado = typeof(DependencyInjection).Assembly;

        services.AddMemoryCache();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(ensamblado));
        services.AddValidatorsFromAssembly(ensamblado);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        services.AddScoped<IAprobadorResolver, AprobadorResolver>();
        services.AddScoped<NotificadorLicencias>();

        return services;
    }
}