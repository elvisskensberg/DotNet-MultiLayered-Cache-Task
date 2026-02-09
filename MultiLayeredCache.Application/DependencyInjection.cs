using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MultiLayeredCache.Application.Behaviors;
using System.Reflection;

namespace MultiLayeredCache.Application;

/// <summary>
/// Extension methods for configuring Application layer services
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds application services to the DI container
    /// Configures MediatR, FluentValidation, and validation pipeline behavior
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Add MediatR with handlers from this assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Add validation behavior to the pipeline
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Add FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
