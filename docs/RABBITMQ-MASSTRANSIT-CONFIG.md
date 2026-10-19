# RabbitMQ + MassTransit

> **Ce document est remplacé par [MASSTRANSIT-FLUX.md](MASSTRANSIT-FLUX.md)** (vocabulaire, schéma du trajet d'un
> message, topologie réelle relevée sur RabbitMQ, exemple de message, retry et file d'erreurs, configuration, tests).

Ce qui a changé par rapport à l'ancienne version de ce fichier :

| Avant | Maintenant |
|---|---|
| Configuration du bus dupliquée dans 3 fichiers (dont un dépôt voisin non versionné) | une seule : `ConnectLive.Application/Messaging/MessagingExtensions.cs` |
| MassTransit 8.1.3 mélangé avec 7.3.1, `AddBus` obsolète | MassTransit 8.1.3 uniquement, `UsingRabbitMq` |
| Publication non active (ligne commentée) | `UserCreated` publié par Core après l'inscription dès que `EventBusConnection` est renseignée |
| Aucun test | harnais en mémoire + test sur un vrai RabbitMQ |

## Démarrage local rapide

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d rabbitmq
# UI de gestion : http://localhost:15672  (user / password)
```

Variables : `EventBusConnection=rabbitmq://localhost:5672`, `EventBusUserName=user`, `EventBusPassword=password`.
