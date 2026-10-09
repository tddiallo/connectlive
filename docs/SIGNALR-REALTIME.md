# Temps réel (SignalR)

## Vue d'ensemble

```
Client (SPA)  ──JoinSession/LeaveSession──▶  SessionHub  ──▶  SessionPresenceTracker
     ▲                                          │
     │  UserJoined / UserLeft                   │ groupe "session:{id}"
     │  QuestionChanged / LeaderboardUpdated    │
     └──────────── SignalRSessionNotifier ◀── ISessionNotifier ◀── handlers MediatR (à venir)
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
| `ConnectLive.Core/Realtime/SessionPresenceTracker.cs` | Suivi connexion → (session, utilisateur), thread-safe |
| `ConnectLive.Core/Realtime/ISessionNotifier.cs` | Abstraction d'envoi d'événements métier |
| `ConnectLive.Core/Realtime/SignalRSessionNotifier.cs` | Implémentation via `IHubContext<SessionHub>` |

## Contrat client

Méthodes serveur (appelées par le client) :

| Méthode | Effet | Retour |
|---|---|---|
| `JoinSession(sessionId, userId)` | Entre dans le groupe, diffuse `UserJoined` | Participants connectés |
| `LeaveSession(sessionId)` | Sort du groupe, diffuse `UserLeft` | — |

Événements (reçus par le client) : `UserJoined(userId)`, `UserLeft(userId)`, `QuestionChanged(SessionQuestionDto)` (jamais la bonne réponse),
`LeaderboardUpdated(LeaderboardModel[])`, `SessionEnded(sessionId)`.

Une déconnexion brutale (onglet fermé, réseau coupé) déclenche automatiquement `UserLeft`.

Exemple client (.NET) :

```csharp
var connection = new HubConnectionBuilder().WithUrl("https://api/sessionHub").WithAutomaticReconnect().Build();
connection.On<string>(SessionHubEvents.UserJoined, userId => /* maj UI */);
await connection.StartAsync();
var participants = await connection.InvokeAsync<string[]>("JoinSession", sessionId, userId);
```

## Utiliser le notifier depuis un handler

```csharp
// injecter ISessionNotifier (singleton) puis :
await _notifier.NotifyQuestionChangedAsync(sessionId, questionDto, cancellationToken);
// Le cycle de vie complet (start/next/end) est déjà branché : voir docs/SESSIONS.md
```

## Sécurité et limites connues

- **Validation** : `sessionId` / `userId` limités à `[A-Za-z0-9_-]`, 64 caractères max (ils composent un nom de groupe) ;
  sinon `HubException`.
- **Pas d'authentification** pour l'instant : `userId` est déclaré par le client et non vérifié. À remplacer par
  l'identité du `Context.User` dès que l'authentification JWT/OIDC existe (voir plan de sprint).
- **CORS** : la politique actuelle (`AllowAnyOrigin`, temporaire) est compatible avec SignalR sans cookies ; passer à
  une liste d'origines + `AllowCredentials()` si l'auth par cookie est adoptée.
- **Multi-instances** : la présence est en mémoire de process et les groupes sont locaux à chaque instance. Pour
  scaler, ajouter le backplane Redis (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`, réutilisant
  `ConnectionStrings:Redis`) et déplacer le tracker vers Redis.

## Tests

`ConnectLive.Core.Api.Tests` : `SessionHubTests` (join/leave/déconnexion/validation), `SessionPresenceTrackerTests`,
`SignalRSessionNotifierTests`. Lancer : `dotnet test ConnectLive.Core.Api.Tests`.
