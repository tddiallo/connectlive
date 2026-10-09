# ConnectLive

Plateforme de quiz en direct : API ASP.NET Core 8 (CQRS/MediatR), SPA Blazor WebAssembly, temps réel SignalR,
cache Redis, jobs Hangfire et événements RabbitMQ.

## Démarrage rapide

```powershell
dotnet build ConnectLive.Core.sln
dotnet test  ConnectLive.Core.sln
dotnet run --project ConnectLive.Core\ConnectLive.Core.Api.csproj --launch-profile http   # http://localhost:5101/swagger
```

Avec Visual Studio : ouvrir `ConnectLive.Core.sln` et choisir un profil multi-projet →
[docs/DEVELOPPEMENT-VISUAL-STUDIO.md](docs/DEVELOPPEMENT-VISUAL-STUDIO.md).

> La solution référence des dépôts frères (`..\ConnectLive.SPA`, `..\Workers`, …) : voir [CLAUDE.md](CLAUDE.md).

## Documentation

| Document | Contenu |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Vue d'ensemble, couches, état des fonctionnalités |
| [docs/DEVELOPPEMENT-VISUAL-STUDIO.md](docs/DEVELOPPEMENT-VISUAL-STUDIO.md) | Profils de démarrage multi-projet, dépendances Docker, dépannage |
| [docs/SESSIONS.md](docs/SESSIONS.md) | Sessions de quiz : conception, machine à états, API |
| [docs/REDIS-CACHE.md](docs/REDIS-CACHE.md) | Cache distribué des requêtes MediatR |
| [docs/SIGNALR-REALTIME.md](docs/SIGNALR-REALTIME.md) | Hub de sessions temps réel et contrat client |
| [docs/PLAN-REDIS-SIGNALR.md](docs/PLAN-REDIS-SIGNALR.md) | Plan initial Redis + SignalR |
| [docs/RABBITMQ-MASSTRANSIT-CONFIG.md](docs/RABBITMQ-MASSTRANSIT-CONFIG.md) | Bus d'événements |
| [docs/EXPLICATION-FONCTIONNEMENT.md](docs/EXPLICATION-FONCTIONNEMENT.md) | Fonctionnement général |
