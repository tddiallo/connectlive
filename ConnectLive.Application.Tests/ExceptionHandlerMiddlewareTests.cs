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
