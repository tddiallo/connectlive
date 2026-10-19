# Plan d’intégration Redis + SignalR

> ⚠ **Document historique.** Rédigé avant plusieurs évolutions (Postgres comme base unique, configuration MassTransit centralisée, backplane Redis, authentification JWT, CI). Pour l'état actuel, voir [AUDIT-360.md](AUDIT-360.md) et les documents thématiques listés dans le [README](../README.md).

## Objectif

Faire passer l’application d’une base locale et d’une logique de cache mémoire vers un système prêt pour le live / session temps réel, avec un cache distribué et une diffusion de notifications en temps réel.

## 1) État actuel du projet

- API ASP.NET Core en .NET 8
- Architecture en couches : Core API / Application / Domain / DAL / SPA / Workers
- Cache actuel : `AddMemoryCache()` dans [ConnectLive.Core/Program.cs](../ConnectLive.Core/Program.cs)
- Pipeline MediatR avec `CacheBehavior` dans [ConnectLive.Core/ConnectLive.Application/CacheBehavior.cs](../ConnectLive.Core/ConnectLive.Application/CacheBehavior.cs)
- Le produit est orienté “Live Quiz / session temps réel” avec interactions asynchrones et événements
- Le système n’est pas encore branché sur Redis ni sur SignalR dans le runtime principal

## 2) Pourquoi Redis ?

Redis apporte :

- cache distribué partagé entre plusieurs instances
- meilleure gestion de la session / lobby / verrouillage / state partagé
- support des pub/sub pour événements
- préparation à la scalabilité horizontale

## 3) Étapes d’implémentation Redis

### Étape 1 — Ajouter le package

Dans le projet API principal :

```bash
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

Et idéalement :

```bash
dotnet add package StackExchange.Redis
```

### Étape 2 — Remplacer le cache mémoire par le cache distribué

Dans [ConnectLive.Core/Program.cs](../ConnectLive.Core/Program.cs), remplacer :

```csharp
builder.Services.AddMemoryCache();
```

par :

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "ConnectLive:";
});
```

Dans `appsettings.json` :

```json
"ConnectionStrings": {
  "ConnectLiveContext": "Data Source=connectlive.db",
  "Redis": "localhost:6379"
}
```

### Étape 3 — Adapter le comportement de cache

Le `CacheBehavior` doit dépendre de `IDistributedCache` au lieu de `IMemoryCache`.

Exemple de changement de logique :

- vérifier `GetString(key)`
- mettre en cache via `SetString(key, value, options)`
- définir TTL de 30s à 5 minutes selon le type de données

Pour les clés sensibles, ajouter un préfixe métier :

- `session:{id}`
- `leaderboard:{sessionId}`
- `user:{id}`

### Étape 4 — Préparer le stockage partagé des sessions

Les sessions actives doivent être stockées sous forme :

```json
{
  "SessionId": "...",
  "Participants": ["user1", "user2"],
  "CurrentQuestion": 2,
  "Status": "Running",
  "StartedAt": "..."
}
```

Cela permet de restaurer l’état sans dépendre uniquement de la mémoire du process.

### Étape 5 — Ajouter la configuration Docker Compose

Dans [ConnectLive.Core/docker-compose.yml](../docker-compose.yml) :

```yaml
redis:
  image: redis:7-alpine
  ports:
    - "6379:6379"
```

et ensuite dans le projet API :

```yaml
environment:
  ConnectionStrings__Redis: "redis:6379"
```

### Étape 6 — Validation

Vérifier :

- build OK
- `dotnet run` OK
- cache Redis accessible
- temps de réponse stable
- plusieurs instances de l’API partagent le même cache

## 4) Pourquoi SignalR ?

SignalR permet d’envoyer des mises à jour temps réel aux clients connectés :

- changement de question
- mise à jour leaderboard
- notifications utilisateur
- arrivée / départ d’un participant
- fin de session

## 5) Étapes d’implémentation SignalR

### Étape 1 — Ajouter le paquet

```bash
dotnet add package Microsoft.AspNetCore.SignalR
```

Dans ASP.NET Core moderne, ce package est inclus dans le runtime web, donc l’ajout dépend du projet cible.

### Étape 2 — Créer le hub

Créer un fichier `SessionHub.cs` :

```csharp
using Microsoft.AspNetCore.SignalR;

public class SessionHub : Hub
{
    public async Task JoinSession(string sessionId, string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        await Clients.Group(sessionId).SendAsync("UserJoined", userId);
    }

    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
    }
}
```

### Étape 3 — Enregistrer le hub

Dans [ConnectLive.Core/Program.cs](../ConnectLive.Core/Program.cs) :

```csharp
builder.Services.AddSignalR();
```

Et ensuite :

```csharp
app.MapHub<SessionHub>("/sessionHub");
```

### Étape 4 — Brancher les événements métier

Dès qu’une question change ou qu’un score est mis à jour :

```csharp
await hubContext.Clients.Group(sessionId).SendAsync("QuestionChanged", payload);
await hubContext.Clients.Group(sessionId).SendAsync("LeaderboardUpdated", payload);
```

### Étape 5 — Gérer la sécurité

- vérifier l’utilisateur authentifié
- valider le `sessionId`
- filtrer les groupes selon les droits d’accès
- éventuellement ajouter JWT / cookie / token pour sécuriser la connexion

### Étape 6 — Client SPA

Dans le front Blazor / SPA :

- créer un client SignalR
- ouvrir la connexion au démarrage
- s’abonner aux événements `QuestionChanged`, `LeaderboardUpdated`, `UserJoined`
- refléter en UI sans recharger la page

## 6) Bonnes pratiques de production

- utiliser Redis pour l’état partagé et le cache distribué
- utiliser SignalR pour la mise à jour live uniquement
- séparer les responsabilités : cache / state / hub / events
- ne pas exposer des données sensibles dans les messages temps réel
- sécuriser les endpoints avec auth si nécessaire
- ajouter journaux et métriques via Serilog + Seq

## 7) Ordre de mise en œuvre recommandé

1. Corriger les dépendances et la base saine du build
2. Ajouter Redis + configuration Docker
3. Migrer `IMemoryCache` vers `IDistributedCache`
4. Ajouter `SessionHub` et `MapHub`
5. Brancher les événements de quiz / leaderboard / notifications
6. Tester avec plusieurs clients connectés
7. Ajouter la surveillance (Seq + logs + métriques)

## 8) Références

- ASP.NET Core caching : https://learn.microsoft.com/aspnet/core/performance/caching/distributed
- StackExchange.Redis : https://stackexchange.github.io/StackExchange.Redis/
- SignalR ASP.NET Core : https://learn.microsoft.com/aspnet/core/signalr/introduction
- Redis : https://redis.io/docs/
- Docker Compose : https://docs.docker.com/compose/

## 9) Suivant

La prochaine étape concrète est d’implémenter :

- la configuration Redis dans [ConnectLive.Core/Program.cs](../ConnectLive.Core/Program.cs)
- le hub SignalR dans un nouveau fichier `SessionHub.cs`
- l’enregistrement dans l’API
- la validation manuelle d’un flux d’utilisateur connecté en temps réel
