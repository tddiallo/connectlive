using AutoMapper;
using ConnectLive.Core.Api.Auth;
using ConnectLive.Core.Api.Configurations;
using ConnectLive.Domain.Contracts;
using ConnectLive.Domain.Exceptions;
using ConnectLive.Portal.Shared.Request;
using ConntectLive.DAL;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static ConnectLive.Core.Api.Commands.UserCommands;

namespace ConnectLive.Core.Api.Tests.Users;

/// <summary>
/// Publication de l'événement <see cref="UserCreatedContractQuery"/> après un enregistrement (voir docs/MASSTRANSIT-FLUX.md).
/// Le bus est le harnais de test en mémoire de MassTransit : même API que RabbitMQ, sans broker.
/// </summary>
public sealed class UserCreatedPublishTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public UserCreatedPublishTests() => _connection.Open();

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private ServiceCollection BaseServices()
    {
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var services = new ServiceCollection();
        services
            .AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection))
            .AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>))
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddSingleton(mapper)
            .AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())))
            .AddSingleton<ITokenService>(new TokenService(
                Options.Create(new JwtOptions { SigningKey = "unit-test-signing-key-0123456789-abcdef" }), TimeProvider.System));
        return services;
    }

    private static void CreateSchema(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
    }

    private static SaveUserRequest Request(string email = "alice@example.com") => new()
    {
        UserFirstName = "Alice", LastName = "Martin", Email = email, Nickname = "ali"
    };

    private static async Task Save(ServiceProvider provider, SaveUserRequest request)
    {
        using var scope = provider.CreateScope();
        await new SaveUserCommandHandler(scope.ServiceProvider).Handle(new SaveUserCommand(request), CancellationToken.None);
    }

    [Fact]
    public async Task Save_PublishesUserCreated_WithEmailAndFullName()
    {
        var services = BaseServices();
        services.AddMassTransitTestHarness();
        await using var provider = services.BuildServiceProvider(true);
        CreateSchema(provider);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await Save(provider, Request("Alice@Example.com"));

        Assert.True(await harness.Published.Any<UserCreatedContractQuery>(x =>
            x.Context.Message.Email == "alice@example.com" && x.Context.Message.FullName == "Alice Martin"));
    }

    [Fact]
    public async Task Save_DoesNotPublish_WhenTheEmailIsAlreadyRegistered()
    {
        var services = BaseServices();
        services.AddMassTransitTestHarness();
        await using var provider = services.BuildServiceProvider(true);
        CreateSchema(provider);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await Save(provider, Request());

        await Assert.ThrowsAsync<ConflictException>(() => Save(provider, Request()));

        Assert.Single(harness.Published.Select<UserCreatedContractQuery>());
    }

    [Fact]
    public async Task Save_StillSucceeds_WhenTheBrokerIsDown()
    {
        var publisher = Substitute.For<IPublishEndpoint>();
        publisher.Publish(Arg.Any<UserCreatedContractQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RabbitMqUnavailableException());
        var services = BaseServices();
        services.AddSingleton(publisher);
        await using var provider = services.BuildServiceProvider(true);
        CreateSchema(provider);

        await Save(provider, Request()); // ne doit pas lever : l'utilisateur est déjà enregistré

        using var scope = provider.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task Save_Works_WhenNoBusIsConfigured()
    {
        await using var provider = BaseServices().BuildServiceProvider(true);
        CreateSchema(provider);

        await Save(provider, Request());

        using var scope = provider.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync());
    }

    private sealed class RabbitMqUnavailableException : Exception
    {
        public RabbitMqUnavailableException() : base("broker unavailable") { }
    }
}
