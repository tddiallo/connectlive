# Cache distribué (Redis)

## Rôle

`CacheBehavior<TRequest, TResponse>` (`ConnectLive.Application`) est un `IPipelineBehavior` MediatR qui met en
cache la réponse de toute requête implémentant `ICachable<TResponse>` (`Key` + `Expiration` en secondes).
Il s'appuie sur `IDistributedCache`, ce qui permet à plusieurs instances de l'API de partager le même cache.

## Choix du fournisseur

Défini dans `ConnectLive.Core/Extensions/CachingExtensions.cs` (`AddConnectLiveCaching`) :

| `ConnectionStrings:Redis` | Fournisseur | Usage |
|---|---|---|
| renseignée (ex. `localhost:6379`) | Redis, préfixe de clés `ConnectLive:` | Docker Compose, QA, prod |
| vide / absente | `AddDistributedMemoryCache()` | Dev local sans dépendance (cache non partagé) |

Via Docker Compose, l'API reçoit `ConnectionStrings__Redis=redis:6379` (service `redis`, image `redis:7-alpine`).
En local : `docker compose up redis` puis `dotnet run` avec `ConnectionStrings__Redis=localhost:6379`.

## Comportement

1. Requête non `ICachable` ou `Expiration <= 0` → le handler est appelé directement.
2. Sinon lecture de `Key` dans le cache ; hit → la valeur JSON est désérialisée et retournée.
3. Miss → handler, puis écriture en JSON (`System.Text.Json`) avec **expiration glissante** de `Expiration` s.
4. Une entrée illisible (schéma modifié, corruption) est ignorée (warning loggé) et reconstruite par le handler.

## Contraintes pour les nouvelles requêtes cachables

- Le type de réponse doit être sérialisable par `System.Text.Json` (propriétés publiques, constructeur compatible).
- Les clés doivent être métier et préfixées : `user:{id}`, `session:{id}`, `leaderboard:{sessionId}`.
- Pas d'invalidation automatique : la TTL est le seul mécanisme d'expiration pour l'instant.

## Tests

`ConnectLive.Application.Tests/CacheBehaviorTests.cs` (xunit, `MemoryDistributedCache`) : hit/miss, contournement
(non cachable, expiration ≤ 0), clé utilisée, entrée corrompue. Lancer : `dotnet test ConnectLive.Application.Tests`.
