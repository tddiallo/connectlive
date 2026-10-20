using ConnectLive.Application;
using ConnectLive.Application.Extensions;
using ConnectLive.Application.Messaging;
using ConnectLive.Core.Api.Configurations;
using ConntectLive.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using ConnectLive.Core.Api.Auth;
using ConnectLive.Core.Api.Extensions;
using ConnectLive.Core.Api;
using ConnectLive.Core.Api.Realtime;
using ConnectLive.Core.Controllers;
using ConnectLive.Portal.Shared.Realtime;

//var builder = WebApplication.CreateBuilder(args);
var builder = HostExtensions.CreateWebHostBuilder<ClassInfo>(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Bouton « Authorize » dans Swagger UI : coller le jeton renvoyé par POST /User/save-profile
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Jeton renvoyé par POST /User/save-profile (champ signature.secret)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("ConnectLiveContext"));
});

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();


// Bus d'événements (MassTransit/RabbitMQ) : actif seulement si EventBusConnection est renseignée
builder.Services.AddConnectLiveMessaging(builder.Configuration, consume: false);
// builder.Services.AddAutoMapper(typeof(MappingProfile), typeof(ApplicationDbContext));
//remplacer par l'utilisation de la configuration explicite de AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(MappingProfile).Assembly));

// Temps réel : hub SignalR, présence, notifier ; backplane Redis si ConnectionStrings:Redis est défini
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddConnectLiveRealtime(builder.Configuration);

// Authentification JWT Bearer (clé de signature obligatoire, voir docs/AUTHENTIFICATION.md)
builder.Services.AddConnectLiveAuthentication(builder.Configuration);

// Cache distribué (Redis si ConnectionStrings:Redis est défini, sinon mémoire locale)
builder.Services.AddConnectLiveCaching(builder.Configuration);
builder.Services.AddConnectLiveHealthChecks(builder.Configuration);

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(WatchBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CacheBehavior<,>));

builder.Services.AddConnectLiveCors(builder.Configuration, builder.Environment);

// L'enregistrement est public : on limite le débit par adresse IP pour freiner la création massive de comptes.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(UserController.RegistrationRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Le middleware d'exceptions est placé tôt pour couvrir authentification, limitation de débit et contrôleurs.
app.UseCustomException();

// CORS must be before UseHttpsRedirection
app.UseCors(CorsExtensions.PolicyName);

// Disable HTTPS redirection in development to avoid CORS issues
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();
app.MapConnectLiveHealthChecks();
app.MapHub<SessionHub>(SessionHubEvents.HubPath);

if (app.Configuration.GetValue(DatabaseExtensions.MigrateOnStartupKey, app.Environment.IsDevelopment()))
    await app.MigrateDatabaseAsync();

app.Run();

/// <summary>Rend le point d'entrée accessible aux tests d'intégration (<c>WebApplicationFactory&lt;Program&gt;</c>).</summary>
public partial class Program;
