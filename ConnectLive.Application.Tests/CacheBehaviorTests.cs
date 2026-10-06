using ConnectLive.Application;
using ConnectLive.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ConnectLive.Application.Tests;

public class CacheBehaviorTests
{
    public record Payload(int Id, string Name);

    public class CachedQuery : IRequest<Payload>, ICachable<Payload>
    {
        public string Key { get; set; } = "payload:1";
        public int Expiration { get; set; } = 30;
    }

    public class PlainQuery : IRequest<Payload> { }

    private static IDistributedCache NewCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static CacheBehavior<TRequest, Payload> NewBehavior<TRequest>(IDistributedCache cache)
        where TRequest : IRequest<Payload> =>
        new(cache, NullLogger<CacheBehavior<TRequest, Payload>>.Instance);

    [Fact]
    public async Task Handle_CallsNextOnce_WhenSecondCallIsServedFromCache()
    {
        // Arrange
        var behavior = NewBehavior<CachedQuery>(NewCache());
        var calls = 0;
        RequestHandlerDelegate<Payload> next = () => { calls++; return Task.FromResult(new Payload(1, "Awa")); };

        // Act
        var first = await behavior.Handle(new CachedQuery(), next, CancellationToken.None);
        var second = await behavior.Handle(new CachedQuery(), next, CancellationToken.None);

        // Assert
        Assert.Equal(1, calls);
        Assert.Equal(new Payload(1, "Awa"), first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Handle_BypassesCache_WhenRequestIsNotCachable()
    {
        var cache = NewCache();
        var behavior = NewBehavior<PlainQuery>(cache);
        var calls = 0;
        RequestHandlerDelegate<Payload> next = () => { calls++; return Task.FromResult(new Payload(2, "Moussa")); };

        await behavior.Handle(new PlainQuery(), next, CancellationToken.None);
        await behavior.Handle(new PlainQuery(), next, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Handle_BypassesCache_WhenExpirationIsNotPositive(int expiration)
    {
        var behavior = NewBehavior<CachedQuery>(NewCache());
        var calls = 0;
        RequestHandlerDelegate<Payload> next = () => { calls++; return Task.FromResult(new Payload(3, "Fatou")); };
        var query = new CachedQuery { Expiration = expiration };

        await behavior.Handle(query, next, CancellationToken.None);
        await behavior.Handle(query, next, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Handle_StoresEntryUnderRequestKey()
    {
        var cache = NewCache();
        var behavior = NewBehavior<CachedQuery>(cache);

        await behavior.Handle(new CachedQuery { Key = "user:42" },
            () => Task.FromResult(new Payload(42, "Ibrahima")), CancellationToken.None);

        Assert.NotNull(await cache.GetAsync("user:42"));
    }

    [Fact]
    public async Task Handle_FallsBackToHandler_WhenCacheEntryIsCorrupted()
    {
        var cache = NewCache();
        await cache.SetAsync("payload:1", new byte[] { 0x01, 0x02, 0x03 });
        var behavior = NewBehavior<CachedQuery>(cache);

        var result = await behavior.Handle(new CachedQuery(),
            () => Task.FromResult(new Payload(7, "Cheikh")), CancellationToken.None);

        Assert.Equal(new Payload(7, "Cheikh"), result);
    }
}
