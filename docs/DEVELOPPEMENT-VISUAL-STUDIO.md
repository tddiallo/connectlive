# Développement avec Visual Studio — profils de démarrage multi-projet

## Conception

**Besoin** : lancer en un clic (F5) les bons projets de la solution, sans configurer à la main chaque fois, et
partager cette configuration avec l'équipe.

**Décisions**

| Décision | Raison |
|---|---|
| Profils multi-projet dans `ConnectLive.Core.slnLaunch` (versionné) | Format natif de VS (≥ 2022 17.11) ; un profil *partagé* est un fichier JSON à côté du `.sln`, contrairement au `.suo` qui est personnel et ignoré par git |
| Un profil de lancement `local-infra` par hôte (`Properties/launchSettings.json`) | Les hôtes tournent sur la machine (débogage) mais leurs dépendances sont dans Docker : les noms d'hôtes du `appsettings.json` (`connectlive-db`, `seq`, `rabbitmq`) ne se résolvent qu'*à l'intérieur* du réseau Docker. Le profil surcharge ces valeurs par `localhost` via des variables d'environnement, sans toucher à la config de déploiement |
| Profil 1 sans aucune dépendance | L'API Core local utilise SQLite + cache mémoire (Hangfire et RabbitMQ y sont désactivés) : on peut développer sans Docker |
| `wwwroot/appsettings.Development.json` dans la SPA | La SPA visait `https://localhost:8081/` (inexistant) ; en Development elle vise l'API sur `http://localhost:5101/` |

## Prérequis

- Visual Studio 2022 **17.11+** (17.14 installé sur la machine de dev) ou Visual Studio 2026
- Les projets frères présents à côté de ce dépôt (`..\ConnectLive.SPA`, `..\Workers`, …) — voir `CLAUDE.md`
- **Activer la fonctionnalité** : *Tools ▸ Options ▸ Environment ▸ Preview Features ▸ Enable Multi-Project Launch Profiles*
  (VS 2026 : *Tools ▸ Options ▸ All Settings ▸ Preview Features*), puis rouvrir la solution
- Docker Desktop uniquement pour le profil 3

## Utilisation

1. Ouvrir `ConnectLive.Core.sln`.
2. Dans la barre d'outils, le menu déroulant du bouton ▶ liste les profils ci-dessous ; choisir puis **F5**.
3. Pour modifier : clic droit sur la solution ▸ *Configure Startup Projects…* (cocher *Share Profile* pour que
   l'ajout aille dans `ConnectLive.Core.slnLaunch`, versionné).

| Profil | Projets lancés | Dépendances |
|---|---|---|
| **1 - Local : API + SPA** *(défaut recommandé)* | `ConnectLive.Core.Api` (5101) + `ConnectLive.SPA` (5279) | aucune |
| **2 - Local : API seule** | `ConnectLive.Core.Api` (5101) | aucune |
| **3 - Stack complète** | Core.Api + Notification.Api (5223) + Newsletter.Api (5077) + SPA | Docker (ci-dessous) |

Swagger : <http://localhost:5101/swagger> · SPA : <http://localhost:5279> · Hub SignalR : `http://localhost:5101/sessionHub`.

## Profil 3 — démarrer les dépendances

```powershell
cd ConnectLive.Core
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d seq rabbitmq connectlive-db redis
```

| Service | Port hôte | Utilisé par |
|---|---|---|
| PostgreSQL (`connectlive-db`) | 5433 | Hangfire (Notification, Newsletter) |
| RabbitMQ | 5672 (AMQP), 15672 (UI : `user` / `password`) | MassTransit (Notification) |
| Redis | 6379 | cache distribué de Core.Api (`ConnectionStrings__Redis`) |
| Seq | 8199 | logs Serilog (`Serilog__SeqServerUrl`) |

Arrêt : `docker compose -f docker-compose.yml -f docker-compose.override.yml down`.

## Dépannage

- **Le menu des profils n'apparaît pas** : la préférence *Multi-Project Launch Profiles* n'est pas activée, ou la
  solution n'a pas été rouverte.
- **Notification/Newsletter plantent avec `Hôte inconnu`** : le profil `local-infra` n'est pas utilisé (lancement avec
  le profil `http`, qui garde le nom d'hôte Docker `connectlive-db`) ou Docker n'est pas démarré.
- **Port déjà utilisé** : un ancien processus tourne encore (`Get-Process ConnectLive*`).
- **La SPA n'atteint pas l'API** : vérifier `wwwroot/appsettings.Development.json` (`GatewayUrl`) et que
  `ASPNETCORE_ENVIRONMENT=Development` (profil `http` de la SPA).

## État de vérification

| Élément | Vérifié ? |
|---|---|
| Build de toute la solution (0 erreur), 23 tests | ✅ exécuté |
| Profil 1/2 : API (Swagger 200, `/User/get-profiles` 200, `/sessionHub/negotiate` 200) + SPA (index 200, config Development servie, CORS `*` vers l'API) | ✅ exécuté en ligne de commande avec les mêmes URLs/variables que les profils |
| Chargement du fichier `.slnLaunch` par l'IDE | ⚠️ non vérifié (pas d'IDE piloté ici) — format repris de la doc Microsoft |
| Profil 3 (Postgres, RabbitMQ, Redis réels) | ⚠️ **non exécuté** : Docker Desktop n'était pas démarré. Sans Postgres, Notification/Newsletter échouent (confirmé : `Hôte inconnu`) |
