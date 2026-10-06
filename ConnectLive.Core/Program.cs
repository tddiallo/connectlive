using AutoMapper;
using ConnectLive.Application;
using ConnectLive.Application.Extensions;
using ConnectLive.Core.Api.Configurations;
using ConntectLive.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.PostgreSql;
using Workers;
using ConnectLive.Core.Api.Filters;
using Connectlive.Proxy;
using ConnectLive.Core.Api.Extensions;
using ConnectLive.Core.Api;
using ConnectLive.Core.Api.Realtime;
using ConnectLive.Portal.Shared.Realtime;

//var builder = WebApplication.CreateBuilder(args);
var builder = HostExtensions.CreateWebHostBuilder<ClassInfo>(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("ConnectLiveContext"));
});

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IEmailWorker, EmailWorker>();
builder.Services.AddScoped<IProxy, Proxy>();

builder.Services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));

// MassTransit/RabbitMQ disabled for local development - requires external RabbitMQ
//builder.Services.AddBusPublisherRegistration(builder.Configuration);
// builder.Services.AddAutoMapper(typeof(MappingProfile), typeof(ApplicationDbContext));
//remplacer par l'utilisation de la configuration explicite de AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
// AutoMapper 16 ne scanne plus le DI : les IValueResolver utilisés par les profils doivent être enregistrés.
builder.Services.AddTransient<TokenResolver>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(MappingProfile).Assembly));

// Temps réel : hub SignalR des sessions, suivi de présence (singleton) et notifier injectable
builder.Services.AddSignalR();
builder.Services.AddSingleton<SessionPresenceTracker>();
builder.Services.AddSingleton<ISessionNotifier, SignalRSessionNotifier>();

// Cache distribué (Redis si ConnectionStrings:Redis est défini, sinon mémoire locale)
builder.Services.AddConnectLiveCaching(builder.Configuration);

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(WatchBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CacheBehavior<,>));

// Hangfire disabled for local development - requires external PostgreSQL
//builder.Services.AddHangfire(x => x.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("ConnectLiveContext"))));
//builder.Services.AddHangfireServer();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policyBuilder =>
    {
        policyBuilder
            .AllowAnyOrigin()  // Temporary: allow all origins for testing
            .AllowAnyHeader()
            .AllowAnyMethod();
            // Note: AllowCredentials() cannot be used with AllowAnyOrigin()
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS must be before UseHttpsRedirection
app.UseCors("CorsPolicy");

// Disable HTTPS redirection in development to avoid CORS issues
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

// Hangfire dashboard disabled - requires Hangfire configuration
//app.UseHangfireDashboard("/workers", new DashboardOptions
//{
//    Authorization = new[] { new AuthorizationFilter() }
//});

app.UseCustomException();
app.MapControllers();
app.MapHub<SessionHub>(SessionHubEvents.HubPath);

app.Run();
