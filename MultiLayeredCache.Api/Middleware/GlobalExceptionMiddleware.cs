using FluentValidation;
using System.Net;
using System.Text.Json;
using MultiLayeredCache.Domain.Exceptions;

namespace MultiLayeredCache.Api.Middleware;

/// <summary>
/// Global exception handling middleware
/// Catches all unhandled exceptions and returns appropriate HTTP responses
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException validationEx)
        {
            _logger.LogWarning(validationEx, "Validation failed");
            await HandleValidationExceptionAsync(context, validationEx);
        }
        catch (NotFoundException notFoundEx)
        {
            _logger.LogWarning(notFoundEx, "Resource not found");
            await HandleNotFoundExceptionAsync(context, notFoundEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleValidationExceptionAsync(HttpContext context, ValidationException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

        var response = new
        {
            message = "Validation failed",
            errors = exception.Errors.Select(e => new
            {
                property = e.PropertyName,
                error = e.ErrorMessage
            })
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private static async Task HandleNotFoundExceptionAsync(HttpContext context, NotFoundException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;

        var response = new
        {
            message = exception.Message
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            message = "An error occurred while processing your request",
            detail = exception.Message
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

/// <summary>
/// Extension method to register the Global Exception Middleware
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
