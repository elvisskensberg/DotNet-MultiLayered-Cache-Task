using System.Diagnostics;

namespace MultiLayeredCache.Api.Middleware;

/// <summary>
/// Middleware that ensures each request has a unique correlation ID for distributed tracing
/// Reads from X-Correlation-ID header or generates a new one
/// Adds the correlation ID to response headers and logging context
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get correlation ID from request header or generate new one
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        // Add to response headers for client tracking
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Add to HttpContext.Items for use in other middleware/controllers
        context.Items[CorrelationIdHeader] = correlationId;

        // Add to Activity for distributed tracing (Application Insights, OpenTelemetry)
        Activity.Current?.SetTag("correlation_id", correlationId);

        // Add to logging scope for all logs in this request
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path,
            ["RequestMethod"] = context.Request.Method
        }))
        {
            _logger.LogDebug("Request started with CorrelationId: {CorrelationId}", correlationId);

            await _next(context);

            _logger.LogDebug("Request completed with CorrelationId: {CorrelationId}", correlationId);
        }
    }
}

/// <summary>
/// Extension method for easy middleware registration
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
