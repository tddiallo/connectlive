# Temps réel (SignalR)

## Vue d'ensemble

```
Client (SPA)  ──JoinSession/LeaveSession──▶  SessionHub  ──▶  SessionPresenceTracker
     ▲                                          │
     │  UserJoined / UserLeft                   │ groupe "session:{id}"
     │  QuestionChanged / LeaderboardUpdated    │
     └──────────── SignalRSessionNotifier ◀── ISessionNotifier ◀── handlers MediatR
```

- **Endpoint** : `/sessionHub` (`SessionHubEvents.HubPath`), mappé dans `Program.cs`.
- **Un groupe SignalR par session** : `SessionHub.GroupName(sessionId)` → `session:{sessionId}`.
- Le **hub** ne gère que la présence. Les **événements métier** sont poussés côté serveur par
  `ISessionNotifier`, ce qui garde les handlers indépendants de SignalR (et testables).

## Fichiers

| Fichier | Rôle |
|---|---|
| `ConnectLive.Portal.Shared/Realtime/SessionHubEvents.cs` | Chemin du hub + noms d'événements (partagés avec la SPA) |
| `ConnectLive.Core/Realtime/SessionHub.cs` | Méthodes appelables par les clients, validation des identifiants |
| `ConnectLive.Core/Realtime/ISessionPresenceTracker.cs` | Contrat de présence ; implémentations `SessionPresenceTracker` (mémoire) et `RedisSessionPresenceTracker` |
| `ConnectLive.Core/Realtime/ISessionNotifier.cs` | Abstraction d'envoi d'événements métier |
| `ConnectLive.Core/Realtime/SignalRSessionNotifier.cs` | Implémentation via `IHubContext<SessionHub>` |

## Contrat client

Méthodes serveur (appelées par le client) :

| Méthode | Effet | Retour |
|---|---|---|
| `JoinSession(sessionId)` | Entre dans le groupe, diffuse `UserJoined` (identité = `sub` du jeton) | Participants connectés |
| `LeaveSession(sessionId)` | Sort du groupe, diffuse `UserLeft` | — |

Événements (reçus par le client) : `UserJoined(userId)`, `UserLeft(userId)`, `QuestionChanged(SessionQuestionDto)` (jamais la bonne réponse),
`LeaderboardUpdated(LeaderboardEntryDto[])` (`userId`, `nickname`, `rank`, `score`), `SessionEnded(sessionId)`.

Une déconnexion brutale (onglet fermé, réseau coupé) déclenche automatiquement `UserLeft`.

Exemple client (.NET) :

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://api/sessionHub", o => o.AccessTokenProvider = () => Task.FromResult<string?>(jwt))
    .WithAutomaticReconnect().Build();
connection.On<string>(SessionHubEvents.UserJoined, userId => /* maj UI */);
await connection.StartAsync();
var participants = await connection.InvokeAsync<string[]>("JoinSession", sessionId);
```

## Utiliser le notifier depuis un handler

```csharp
// injecter ISessionNotifier (singleton) puis :
await _notifier.NotifyQuestionChangedAsync(sessionId, questionDto, cancellationToken);
// Le cycle de vie complet (start/next/end) est déjà branché : voir docs/SESSIONS.md
```

## Sécurité et limites connues

- **Validation** : `sessionId` limité à `[A-Za-z0-9_-]`, 64 caractères max (il compose un nom de groupe) ; sinon
  `HubException`.
- **Authentification** : le hub est `[Authorize]`. Le jeton JWT est transmis par `?access_token=` (uniquement sur
  `/sessionHub`) et l'identité du participant est celle du jeton, jamais une valeur fournie par le client
  ([AUTHENTIFICATION.md](AUTHENTIFICATION.md)).
- **CORS** : origines de `Security:AllowedOrigins` avec `AllowCredentials()` ; hors Development, aucune origine
  n'est autorisée sans configuration.
- **Multi-instances** : avec `ConnectionStrings:Redis`, un backplane Redis relaie les messages entre instances et la présence est
  stockée dans Redis — voir [SIGNALR-BACKPLANE-REDIS.md](SIGNALR-BACKPLANE-REDIS.md) (schémas, configuration, limites).

## Tests

`ConnectLive.Core.Api.Tests` : `SessionHubTests` (join/leave/déconnexion/validation), `PresenceTrackerContract` (mémoire + Redis), `SignalRBackplaneTests`,
`SignalRSessionNotifierTests`. Lancer : `dotnet test ConnectLive.Core.Api.Tests`.
