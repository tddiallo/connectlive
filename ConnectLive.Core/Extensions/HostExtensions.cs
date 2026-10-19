using ConnectLive.Application.BusEvents;
using Serilog;
using System.Net;

namespace ConnectLive.Core.Api.Extensions;

public static class HostExtensions
{
    public static WebApplicationBuilder CreateWebHostBuilder<T>(string[] args)
        where T : class
    {
        string appName = typeof(T).Assembly.GetName().Name;

        var builder = WebApplication.CreateBuilder(args);
        builder.Host.ConfigureAppConfiguration((host, builder) =>
        {
            builder.SetBasePath(Directory.GetCurrentDirectory());
            builder.AddJsonFile("appsettings.json", optional: true);
            builder.AddJsonFile($"appsettings.{host.HostingEnvironment.EnvironmentName}.json", optional: true);
            builder.AddEnvironmentVariables();
            builder.AddCommandLine(args);
        })
           .ConfigureLogging((host, builder) => builder.UseSerilog(host.Configuration, appName).AddSerilog());

        return builder;
    }

    public static ILoggingBuilder UseSerilog(this ILoggingBuilder builder, IConfiguration configuration, string appName)
    {
        var seqServerUrl = configuration["Serilog:SeqServerUrl"];
        var hostName = Dns.GetHostName();
        var ipAddress = Dns.GetHostEntry(hostName).AddressList[0].ToString();

        Log.Logger = new LoggerConfiguration()
           .Enrich.WithProperty("Context", appName)
           .Enrich.WithProperty("HostName", hostName)
           .Enrich.WithProperty("IP", ipAddress)
           .Enrich.FromLogContext()
           .WriteTo.Seq(string.IsNullOrWhiteSpace(seqServerUrl) ? "http://seq" : seqServerUrl)
           .ReadFrom.Configuration(configuration)
           .CreateLogger();

        return builder;
    }
}
