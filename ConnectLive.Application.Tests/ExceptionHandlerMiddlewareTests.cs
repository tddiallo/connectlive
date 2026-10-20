using ConnectLive.Application.Middlewares;
using ConnectLive.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ConnectLive.Application.Tests;

public class ExceptionHandlerMiddlewareTests
{
    private readonly ILogger<ExceptionHandlerMiddleware> _logger = Substitute.For<ILogger<ExceptionHandlerMiddleware>>();
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();

    private async Task<DefaultHttpContext> Invoke(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await new ExceptionHandlerMiddleware(_logger, _ => throw exception, _environment).InvokeAsync(context);
        return context;
    }

    private int CountLogs(LogLevel level) =>
        _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)c.GetArguments()[0]! == level);

    [Theory]
    [InlineData(typeof(KeyNotFoundException), 404)]
    [InlineData(typeof(ConflictException), 409)]
    [InlineData(typeof(ForbiddenException), 403)]
    [InlineData(typeof(ArgumentException), 400)]
    [InlineData(typeof(UnauthorizedAccessException), 401)]
    public async Task ClientErrors_MapToTheirStatus_WithoutCriticalAlert(Type type, int status)
    {
        var exception = (Exception)Activator.CreateInstance(type, "boom")!;

        var context = await Invoke(exception);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(0, CountLogs(LogLevel.Critical));
        Assert.Equal(0, CountLogs(LogLevel.Error));
    }

    [Fact]
    public async Task ServerErrors_AreOpaqueOutsideDevelopment_AndRaiseTheAdminAlert()
    {
        _environment.EnvironmentName.Returns(Environments.Production);

        var context = await Invoke(new InvalidOperationException("secret internals"));
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.Equal(500, context.Response.StatusCode);
        Assert.DoesNotContain("secret internals", body);
        Assert.Equal(1, CountLogs(LogLevel.Critical));
    }
}

public class ExceptionHandlerProblemDetailsTests
{
    private static async Task<(DefaultHttpContext Context, System.Text.Json.JsonElement Body)> Invoke(Exception exception, string environment = "Production")
    {
        var env = NSubstitute.Substitute.For<Microsoft.Extensions.Hosting.IHostEnvironment>();
        env.EnvironmentName.Returns(environment);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        context.Response.Body = new MemoryStream();
        var logger = NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger<ConnectLive.Application.Middlewares.ExceptionHandlerMiddleware>>();

        await new ConnectLive.Application.Middlewares.ExceptionHandlerMiddleware(logger, _ => throw exception, env).InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var doc = System.Text.Json.JsonDocument.Parse(await new StreamReader(context.Response.Body).ReadToEndAsync());
        return (context, doc.RootElement.Clone());
    }

    [Fact]
    public async Task Errors_AreProblemDetails_WithStatusTitleDetailAndTraceId()
    {
        var (context, body) = await Invoke(new ConnectLive.Domain.Exceptions.ConflictException("already exists"));

        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Equal(409, body.GetProperty("status").GetInt32());
        Assert.Equal("Conflict", body.GetProperty("title").GetString());
        Assert.Equal("already exists", body.GetProperty("detail").GetString());
        Assert.Equal("trace-123", body.GetProperty("traceId").GetString());
        Assert.StartsWith("https://", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task LegacyErrorField_IsKept_ForExistingClients()
    {
        var (_, body) = await Invoke(new KeyNotFoundException("nope"));

        Assert.Equal("nope", body.GetProperty("error").GetString());
        Assert.Equal(404, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ServerErrors_HideInternals_OutsideDevelopment()
    {
        var (_, body) = await Invoke(new InvalidOperationException("connection string password=hunter2"));

        Assert.Equal(500, body.GetProperty("status").GetInt32());
        Assert.DoesNotContain("hunter2", body.GetRawText());
        Assert.Equal("An unexpected error occurred.", body.GetProperty("detail").GetString());
    }
}
