# ConnectLive

Plateforme de quiz en direct : API ASP.NET Core 8 (CQRS/MediatR), SPA Blazor WebAssembly, temps réel SignalR,
cache et backplane Redis, PostgreSQL, jobs Hangfire et événements RabbitMQ (MassTransit).

## Démarrage rapide

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d connectlive-db   # Postgres (voir docs/POSTGRES-DOCKER.md)
dotnet build ConnectLive.Core.CI.slnf
dotnet test  ConnectLive.Core.CI.slnf
dotnet run --project ConnectLive.Core\ConnectLive.Core.Api.csproj --launch-profile http   # http://localhost:5101/swagger
```

En Development, une clé JWT de démonstration est fournie. **Hors Development, l'API refuse de démarrer sans
`Jwt__SigningKey`** (≥ 32 caractères) — voir [docs/AUTHENTIFICATION.md](docs/AUTHENTIFICATION.md). Pour tester dans
Swagger : `POST /User/save-profile`, copier `signature.secret`, bouton **Authorize**.

Avec Visual Studio : ouvrir `ConnectLive.Core.sln` et choisir un profil multi-projet →
[docs/DEVELOPPEMENT-VISUAL-STUDIO.md](docs/DEVELOPPEMENT-VISUAL-STUDIO.md).

> `ConnectLive.Core.sln` référence aussi des dossiers voisins non versionnés (SPA, Workers…) : pour le backend, utiliser `ConnectLive.Core.CI.slnf` (voir [CLAUDE.md](CLAUDE.md) et [docs/CI.md](docs/CI.md)).

## Documentation

| Document | Contenu |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Vue d'ensemble, couches, état des fonctionnalités |
| [docs/DEVELOPPEMENT-VISUAL-STUDIO.md](docs/DEVELOPPEMENT-VISUAL-STUDIO.md) | Profils de démarrage multi-projet, dépendances Docker, dépannage |
| [docs/AUDIT-360.md](docs/AUDIT-360.md) | Audit 360° : constats, ce qui est corrigé, ce qui reste, changements cassants |
| [docs/AUTHENTIFICATION.md](docs/AUTHENTIFICATION.md) | Jetons JWT, droits par route, configuration de la clé, SignalR, CORS |
| [docs/QUESTIONS-REPONSES.md](docs/QUESTIONS-REPONSES.md) | Questions, réponses des joueurs, correction serveur, classement |
| [docs/SESSIONS.md](docs/SESSIONS.md) | Sessions de quiz : conception, machine à états, API |
| [docs/POSTGRES-DOCKER.md](docs/POSTGRES-DOCKER.md) | Pourquoi Postgres partout, démarrage Docker, migrations, dépannage |
| [docs/MASSTRANSIT-FLUX.md](docs/MASSTRANSIT-FLUX.md) | Comment les messages circulent (MassTransit + RabbitMQ), avec schéma |
| [docs/SIGNALR-BACKPLANE-REDIS.md](docs/SIGNALR-BACKPLANE-REDIS.md) | Plusieurs instances SignalR : backplane Redis, présence partagée, schémas |
| [docs/CI.md](docs/CI.md) | Pipeline GitHub Actions, services de test, couverture, reproduction locale |
| [docs/REDIS-CACHE.md](docs/REDIS-CACHE.md) | Cache distribué des requêtes MediatR |
| [docs/SIGNALR-REALTIME.md](docs/SIGNALR-REALTIME.md) | Hub de sessions temps réel et contrat client |
| [docs/PLAN-REDIS-SIGNALR.md](docs/PLAN-REDIS-SIGNALR.md) | Plan initial Redis + SignalR |
| [docs/RABBITMQ-MASSTRANSIT-CONFIG.md](docs/RABBITMQ-MASSTRANSIT-CONFIG.md) | (remplacé par MASSTRANSIT-FLUX.md) |
| [docs/EXPLICATION-FONCTIONNEMENT.md](docs/EXPLICATION-FONCTIONNEMENT.md) | Fonctionnement général |
