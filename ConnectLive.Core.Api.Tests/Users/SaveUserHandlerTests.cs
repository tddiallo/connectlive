using System.ComponentModel.DataAnnotations;
using AutoMapper;
using ConnectLive.Core.Api.Auth;
using ConnectLive.Core.Api.Configurations;
using ConnectLive.Domain.Exceptions;
using ConnectLive.Portal.Shared.Request;
using ConntectLive.DAL;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static ConnectLive.Core.Api.Commands.UserCommands;
using static ConnectLive.Core.Api.Queries.UserQueries;

namespace ConnectLive.Core.Api.Tests.Users;

/// <summary>Enregistrement d'un utilisateur : validation, unicité de l'email, absence de secret dans les profils.</summary>
public sealed class SaveUserHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services;

    public SaveUserHandlerTests()
    {
        _connection.Open();
        var mapperConfig = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        _services = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection))
            .AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>))
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddSingleton(mapperConfig.CreateMapper())
            .AddSingleton<ITokenService>(new TokenService(Options.Create(new JwtOptions { SigningKey = "unit-test-signing-key-0123456789-abcdef" }), TimeProvider.System))
            .AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())))
            .BuildServiceProvider();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    private static SaveUserRequest Request(string email = "alice@example.com") => new()
    {
        UserFirstName = "Alice",
        LastName = "Martin",
        Email = email,
        Nickname = "ali"
    };

    private async Task<T> Run<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Task<Portal.Shared.Response.SaveUserResponse> Save(SaveUserRequest request) =>
        Run(sp => new SaveUserCommandHandler(sp).Handle(new SaveUserCommand(request), CancellationToken.None));

    [Fact]
    public async Task Save_PersistsUser_AndNormalizesEmail()
    {
        var response = await Save(Request("  Alice@Example.COM "));

        Assert.Equal("alice@example.com", response.Email);
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.False(string.IsNullOrWhiteSpace(response.Signature.Secret));
    }

    [Fact]
    public async Task Save_Throws_Conflict_WhenEmailAlreadyRegistered_EvenWithDifferentCase()
    {
        await Save(Request("alice@example.com"));

        await Assert.ThrowsAsync<ConflictException>(() => Save(Request("ALICE@example.com")));
    }

    [Fact]
    public async Task GetUsers_NeverExposesASecret()
    {
        await Save(Request());

        var users = await Run(sp => new GetUsersQueryHandler(sp).Handle(new GetUsersQuery(), CancellationToken.None));

        Assert.Single(users);
        Assert.Null(users[0].Signature);
    }

    [Fact]
    public async Task Save_InvalidatesTheCachedUserList()
    {
        var cache = _services.GetRequiredService<IDistributedCache>();
        await cache.SetStringAsync(new GetUsersQuery().Key, "stale");

        await Save(Request());

        Assert.Null(await cache.GetStringAsync(new GetUsersQuery().Key));
    }

    [Theory]
    [InlineData("", "Martin", "alice@example.com", "ali")]
    [InlineData("Alice", "", "alice@example.com", "ali")]
    [InlineData("Alice", "Martin", "not-an-email", "ali")]
    [InlineData("Alice", "Martin", "", "ali")]
    [InlineData("Alice", "Martin", "alice@example.com", "")]
    public void Request_IsInvalid_WhenARequiredFieldIsMissingOrMalformed(string first, string last, string email, string nick)
    {
        var request = new SaveUserRequest { UserFirstName = first, LastName = last, Email = email, Nickname = nick };

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), true));
    }

    [Fact]
    public void Request_IsValid_WhenComplete()
    {
        var request = Request();

        Assert.True(Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), true));
    }
}
