using Asp.Versioning;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MultiLayeredCache.Api.Middleware;
using MultiLayeredCache.Application;
using MultiLayeredCache.Infrastructure;
using MultiLayeredCache.Infrastructure.HealthChecks;

namespace MultiLayeredCache.Api;

/// <summary>
/// Configures services and the application's request pipeline
/// </summary>
public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    /// <summary>
    /// Configures application services (Dependency Injection)
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        // Add MVC controllers
        services.AddControllers();

        // PRODUCTION RECOMMENDATION: Enable API Versioning
        // Uncomment the block below to support versioned endpoints (e.g., /api/v1/data, /api/v2/data)
        // This allows backward-compatible API evolution without breaking existing clients.
        /*
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });
        */

        // Add API explorer and Swagger
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Add Infrastructure layer (Repository, Cache, Options Pattern)
        services.AddInfrastructure(Configuration);

        // Add Application layer (MediatR, FluentValidation, CQRS)
        services.AddApplication();

        // Add Health Checks
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready", "cache" })
            .AddCheck<SdcsHealthCheck>("sdcs", tags: new[] { "ready", "cache" })
            .AddCheck<CosmosDbHealthCheck>("cosmosdb", tags: new[] { "ready", "database" });
    }

    /// <summary>
    /// Configures the HTTP request pipeline (Middleware)
    /// </summary>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Global Exception Middleware (must be first)
        app.UseGlobalExceptionHandler();

        // Correlation ID for distributed tracing (should be early in pipeline)
        app.UseCorrelationId();

        // Development-specific middleware
        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Multi-Layered Cache API v1");
                options.RoutePrefix = string.Empty; // Serve Swagger UI at the app's root
            });
        }

        // Standard middleware pipeline
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthorization();

        // Map controller endpoints
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();

            // Health check endpoints for Kubernetes/load balancers
            endpoints.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false, // No checks = always healthy (liveness check)
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
        });
    }
}
