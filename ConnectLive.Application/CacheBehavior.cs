using System.Text.Json;
using ConnectLive.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ConnectLive.Application;

/// <summary>
/// Comportement de pipeline MediatR qui met en cache la réponse des requêtes implémentant
/// <see cref="ICachable{TEntity}"/>.
/// </summary>
/// <remarks>
/// Le cache est <see cref="IDistributedCache"/> : Redis quand <c>ConnectionStrings:Redis</c> est configuré,
/// sinon un cache mémoire de process (voir <c>CachingExtensions</c> dans l'API). Les réponses sont sérialisées
/// en JSON, elles doivent donc être sérialisables par <c>System.Text.Json</c>.
/// Flux : requête non cachable ou <c>Expiration &lt;= 0</c> → handler direct ; sinon lecture du cache,
/// et en cas d'absence (ou d'entrée illisible) appel du handler puis écriture avec expiration glissante.
/// </remarks>
public class CacheBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheBehavior<TRequest, TResponse>> _logger;

    public CacheBehavior(IDistributedCache cache, ILogger<CacheBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICachable<TResponse> cacheRequest || cacheRequest.Expiration <= 0)
            return await next();

        var cachedBytes = await _cache.GetAsync(cacheRequest.Key, cancellationToken);
        if (cachedBytes is not null && TryDeserialize(cachedBytes, cacheRequest.Key, out var cachedData))
        {
            _logger.LogInformation("[{CommandName}][Cache] Pulled data from cache for key:[{Key}].",
                typeof(TRequest).Name, cacheRequest.Key);
            return cachedData;
        }

        _logger.LogInformation("[{CommandName}][Cache] Data not found or expired for key:[{Key}].",
            typeof(TRequest).Name, cacheRequest.Key);

        var response = await next();

        var options = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromSeconds(cacheRequest.Expiration)
        };
        await _cache.SetAsync(cacheRequest.Key, JsonSerializer.SerializeToUtf8Bytes(response), options, cancellationToken);

        _logger.LogInformation("[{CommandName}][Cache] Cache set [{Key}] for {Expiration} seconds.",
            typeof(TRequest).Name, cacheRequest.Key, cacheRequest.Expiration);

        return response;
    }

    /// <summary>
    /// Désérialise une entrée de cache. Une entrée illisible (changement de schéma, corruption) est traitée
    /// comme un « miss » pour que le handler reconstruise la valeur plutôt que de faire échouer la requête.
    /// </summary>
    private bool TryDeserialize(byte[] bytes, string key, out TResponse value)
    {
        try
        {
            var deserialized = JsonSerializer.Deserialize<TResponse>(bytes);
            if (deserialized is not null)
            {
                value = deserialized;
                return true;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[{CommandName}][Cache] Unreadable cache entry for key:[{Key}], ignoring it.",
                typeof(TRequest).Name, key);
        }

        value = default!;
        return false;
    }
}
