using ConnectLive.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace ConnectLive.Application.Middlewares;
public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlerMiddleware(ILogger<ExceptionHandlerMiddleware> logger, RequestDelegate next, IHostEnvironment environment)
    {
        _logger = logger;
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        _logger.LogInformation("[ExceptionHandlerMiddleware][START]");

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }

        _logger.LogInformation("[ExceptionHandlerMiddleware][END]");
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = exception switch
        {
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            KeyNotFoundException => HttpStatusCode.NotFound,
            ForbiddenException => HttpStatusCode.Forbidden,
            ConflictException => HttpStatusCode.Conflict,
            ArgumentException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };

        // Never echo raw exception details to the client outside Development: that leaks internals.
        var isExpected = (int)code < 500;

        // Un 4xx est une erreur du client (404, 409…) : un simple avertissement, sans alerte admin.
        // Seules les erreurs serveur (5xx) méritent le niveau Error et l'alerte.
        if (isExpected)
        {
            _logger.LogWarning("[ExceptionHandlerMiddleware][{StatusCode}] {Message}", (int)code, exception.Message);
        }
        else
        {
            _logger.LogError(exception, "[ExceptionHandlerMiddleware][EXCEPTION]");
            await SendExceptionToAdmin(exception);
        }

        var message = _environment.IsDevelopment() || isExpected
            ? exception.InnerException?.Message ?? exception.Message
            : "An unexpected error occurred.";

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }

    private Task SendExceptionToAdmin(Exception exception)
    {
        _logger.LogCritical(exception, "[ExceptionHandlerMiddleware][ADMIN ALERT] Unhandled exception requires admin attention.");
        return Task.CompletedTask;
    }
}
