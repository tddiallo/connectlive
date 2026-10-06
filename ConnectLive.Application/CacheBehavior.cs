using ConnectLive.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConnectLive.Application;
// cette classe implémente un comportement de pipeline pour la mise en cache des requêtes MediatR.
// Il vérifie d'abord si la requête implémente l'interface ICachable<TResponse>.
// Si c'est le cas, il tente de récupérer les données mises en cache.
// Si les données ne sont pas trouvées ou ont expiré, il appelle le prochain gestionnaire de la requête(next),
// met en cache le résultat et le retourne au demandeur au format approprié(exemple: JSON).
public class CacheBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheBehavior<TRequest, TResponse>> _logger;
    public CacheBehavior(IMemoryCache cache, ILogger<CacheBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

     // cette méthode gère la logique de mise en cache pour les requêtes MediatR.
     // on utilise le pattern de pipeline de MediatR pour intercepter les requêtes et gérer la mise en cache.

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!request.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICachable<>)))
            return await next();

        var cacheRequest = request as ICachable<TResponse>;
        if(cacheRequest?.Expiration <= 0)
        {
            return await next();
        }

        if (_cache.TryGetValue(cacheRequest.Key, out TResponse cachedData))
        {
            _logger.LogInformation(">>>>>>>[{commandName}][Cache] Pulled data from cache for key:[{key}]."
                , typeof(TRequest).Name, cacheRequest.Key);

        }
        else
        {
            _logger.LogInformation(">>>>>>>[{commandName}][Cache] Data not found or expired for Key:[{key}]."
                , typeof(TRequest).Name, cacheRequest.Key);

            cachedData = await next();
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(cacheRequest.Expiration))
                .SetSize(1)
                .SetPriority(CacheItemPriority.Low);

            _cache.Set(cacheRequest.Key, cachedData, cacheOptions);

            _logger.LogInformation(">>>>>>>[{commandName}][Cache] Cache Set [{key}] for '{cacheRequest.Expiration}' seconds."
               , typeof(TRequest).Name, cacheRequest.Key, cacheRequest.Expiration);
        }


        //ScoreQuery
        //InstructionQuery
        //.....

        //Switch Case...(User, Instruction, ...) // <<<< Session
        //ICache <<< CQ that implements it's we will use this mechanism.
        //
        //2- If we have a cache, we return it.
        //3- If we don't have a cache, we fetch the data
        //4- Save it to the cache.

        return cachedData;
    }
}
