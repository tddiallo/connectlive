# Audit 360° — constats et suites données

Audit réalisé le 2026-10-19 sur l'état du dépôt à `2087ea0`, puis traité en deux vagues (sécurité/fonctionnel, puis infrastructure/industrialisation). Ce document dit **ce qui a été
trouvé, ce qui est corrigé, et ce qui reste**. Les détails de chaque évolution sont dans les documents liés.

## Verdict initial

Socle de domaine solide (règles dans `SessionEntity`, concurrence optimiste, `TimeProvider`, notifier abstrait,
bonne réponse jamais envoyée aux joueurs) mais **API non exposable** (aucune authentification) et **produit incomplet**
(pas de création de questions, de réponses ni de classement).

## Suivi des constats

Légende : ✅ corrigé · ⏳ reste à faire

### Sécurité

| # | Constat | État | Détail |
|---|---|---|---|
| 1 | Aucune authentification : n'importe qui pilote n'importe quelle session ; `HostUserId` fourni par le client | ✅ | JWT Bearer ; l'hôte est l'appelant ; start/next/end réservés à l'animateur (403) — [AUTHENTIFICATION.md](AUTHENTIFICATION.md) |
| 2 | Hub SignalR : `userId` déclaré par le client (usurpation) | ✅ | Hub `[Authorize]`, identité = `sub` du jeton ; `JoinSession(sessionId)` sans `userId` (changement cassant) |
| 3 | Jeton `SHA256(email)` sans sel, **exposé par `get-profiles`** | ✅ | Supprimé. Vrai JWT émis une fois à l'inscription ; les profils n'exposent aucun secret |
| 4 | CORS `AllowAnyOrigin`, `Security:AllowedOrigins` ignoré | ✅ | Origines configurées + credentials ; sans config : permissif en Development, **fermé** sinon |
| 5 | Inscription sans limitation de débit | ✅ | 10 requêtes/minute/IP (⏳ `ForwardedHeaders` derrière un proxy) |

### Fonctionnel

| # | Constat | État | Détail |
|---|---|---|---|
| 6 | Impossible de créer des questions par l'API (contrôleur mock) | ✅ | CRUD partiel réel (création + liste des siennes) — [QUESTIONS-REPONSES.md](QUESTIONS-REPONSES.md) |
| 7 | Pas de réponses des joueurs ni de classement | ✅ | Correction serveur, unicité en base, `LeaderboardUpdated` temps réel |
| 8 | `SaveUser` sans validation ; email dupliqué → 500 | ✅ | Validation (400), email normalisé et unique (409) |
| 9 | Cache `AllUsers` jamais invalidé après `SaveUser` | ✅ | Invalidation à l'enregistrement |

### Hygiène et exploitation

| # | Constat | État | Détail |
|---|---|---|---|
| 10 | `connectlive.db` versionné malgré le `.gitignore` | ✅ | Retiré de l'index git (le fichier local est conservé) |
| 11 | `LogCritical` + « alerte admin » pour **toutes** les exceptions (404/409 inclus) | ✅ | 4xx → avertissement ; alerte réservée aux 5xx |
| 12 | Middleware d'exceptions placé après l'authentification | ✅ | Déplacé en tête du pipeline |
| 13 | `CLAUDE.md` / `ARCHITECTURE.md` périmés | ✅ | Mis à jour |
| 14 | Tests : 56, aucun test HTTP de bout en bout | ✅ | **148 tests** (dont intégration HTTP `WebApplicationFactory`, et tests sur services réels) |

### Deuxième vague : infrastructure et industrialisation

| # | Constat | État | Détail |
|---|---|---|---|
| 15 | SQLite en dev mais Postgres dans le compose : migrations jamais validées sur la base cible | ✅ | Postgres partout, migrations régénérées, tests sur Postgres réel — [POSTGRES-DOCKER.md](POSTGRES-DOCKER.md) |
| 16 | Mélange MassTransit 8.1.3 / 7.3.1 et `AddBus` obsolète ; configuration du bus dupliquée en 3 endroits | ✅ | Une configuration unique (`MessagingExtensions`), 8.1.3 uniquement, `UsingRabbitMq`, retry + file d'erreurs — [MASSTRANSIT-FLUX.md](MASSTRANSIT-FLUX.md) |
| 16b | Aucun événement réellement publié (publication commentée) | ✅ | `UserCreated` publié par Core après l'inscription (si un broker est configuré) |
| 17 | Couche données pseudo-asynchrone | ✅ | `CommitAsync(ct)`, `GetByIdAsync` réellement async, `Query()` ; méthodes inutilisées supprimées |
| 19 | Mono-instance : présence en mémoire, pas de backplane SignalR | ✅ | Backplane Redis + présence Redis (`ISessionPresenceTracker`) — [SIGNALR-BACKPLANE-REDIS.md](SIGNALR-BACKPLANE-REDIS.md) |
| 22 | Code mort | ✅ | Supprimés : 3 × `WeatherForecast`, anciens repositories (`NotImplementedException`), `IArticleUnitOfWork`, `IUnitOfWork<C>`, `IbrahimaException`, modèles de domaine inutilisés, filtre Hangfire, références inutiles à `Workers`/`Proxy`/`WebHosting` |
| 24 | Pas de CI ni de mesure de couverture | ✅ | GitHub Actions avec Postgres/Redis/RabbitMQ réels, seuil de couverture 80 % (~88 % mesuré), build des images — [CI.md](CI.md) |
| 18 | Notification SignalR après commit sans filet (échec ⇒ 500 alors que l'état a changé) | ✅ | `ResilientSessionNotifier` journalise l'échec et ne le propage pas ; les clients se resynchronisent via `GET` |
| 21a | Pas de health checks | ✅ | `/health/live` et `/health/ready` (Postgres, Redis si configuré), sans détail interne (voir `HealthExtensions`, [SIGNALR-BACKPLANE-REDIS.md](SIGNALR-BACKPLANE-REDIS.md)) |
| 26 | **Nouveau** : le dépôt ne compilait pas seul (dossiers voisins non versionnés) | ✅ | `Core.Api`, `Notification.Api`, `Newsletter.Api` sont autonomes ; filtre de solution `ConnectLive.Core.CI.slnf` |

### Reste à faire

| # | Constat | Priorité | Piste |
|---|---|---|---|
| 20 | Pas de reconnexion/révocation de jeton ; inscription ouverte | Moyenne | Connexion par lien magique/mot de passe ou OIDC — **décision produit requise** ([AUTHENTIFICATION.md](AUTHENTIFICATION.md)) |
| 21 | Succès hétérogènes (`Result<T>` ou objets bruts) ; pas de versionnage d'API | Moyenne | Uniformiser les réponses de succès ; versionner avant de publier l'API à des tiers |
| 21b | Erreurs : formats multiples (`{error}`, validation ASP.NET) | ✅ | Toutes les erreurs sont des problem details RFC 9457 (champ `error` conservé) |
| 23 | `Nullable` désactivé dans le DAL ; avertissements CS8618 | Basse | Activer progressivement |
| 25 | Modification/suppression de questions ; bonus de rapidité ; révélation de la bonne réponse ; délai max de réponse | Produit | Voir « Limites » de [QUESTIONS-REPONSES.md](QUESTIONS-REPONSES.md) |
| 27 | **Nouveau** : publication d'événements « au mieux » (perdus si RabbitMQ est en panne) | Moyenne | Outbox transactionnelle ([MASSTRANSIT-FLUX.md](MASSTRANSIT-FLUX.md), « Limites ») |
| 28 | **Nouveau** : fantômes de présence si une instance est tuée (expiration 12 h) | Basse | Heartbeat par instance ou TTL court rafraîchi |
| 29 | **Nouveau** : sessions collantes (sticky) ou WebSockets seuls à configurer sur le répartiteur de charge | À faire au déploiement | [SIGNALR-BACKPLANE-REDIS.md](SIGNALR-BACKPLANE-REDIS.md) |
| 30 | Images Docker et workflow GitHub non vérifiés | ✅ | Premier run GitHub : 2 défauts trouvés et corrigés, puis vert (148 tests, 90 % de couverture, 3 images) — [CI.md](CI.md) |
| 32 | **Nouveau** : clé étrangère `questions → users` mal mappée (colonne fantôme `UserEntityId`, `UserId` sans contrainte) | Moyenne | Déclarer la relation dans `OnModelCreating`, migration, adapter les jeux de test ([GUIDE-ARCHITECTURE.md](GUIDE-ARCHITECTURE.md), §21) |
| 31 | Pas d'analyse de vulnérabilités | ✅ (dépendances) | 9 paquets transitifs vulnérables corrigés, contrôle en CI, Dependabot — [SECURITE-DEPENDANCES.md](SECURITE-DEPENDANCES.md). Reste : scan des images Docker (Trivy) et CD |

## Changements cassants à connaître (clients / SPA / déploiement)

1. Toutes les routes sauf `POST /User/save-profile` exigent `Authorization: Bearer <jwt>` (le jeton est dans
   `signature.secret` de la réponse d'inscription).
2. Hub : `JoinSession(sessionId)` — le paramètre `userId` est supprimé ; connexion avec `?access_token=`.
3. `LeaderboardUpdated` envoie `LeaderboardEntryDto[]` (`userId`, `nickname`, `rank`, `score`).
4. `CreateSessionRequest.hostUserId` est ignoré.
5. `CreateQuestionResponse` contient maintenant `category` et `correctAnswer` (vue auteur).
6. L'API **refuse de démarrer sans `Jwt:SigningKey`** (≥ 32 caractères) hors Development.
7. **La base est Postgres uniquement** : plus de SQLite. Les anciennes migrations ont été remplacées par `InitialSchema` :
   une base créée avec les anciennes migrations doit être recréée (`docker compose down -v`, ou nouvelle base).
8. En production : fournir `ConnectionStrings__ConnectLiveContext` (sans mot de passe dans le dépôt) et appliquer les
   migrations par une étape de déploiement (`Database:MigrateOnStartup` vaut `false` hors Development).
9. Le bus d'événements ne démarre que si `EventBusConnection` est renseignée ; la configuration du bus n'est plus
   fournie par le dépôt voisin `Connectlive.WebHosting`.
10. Avec `ConnectionStrings:Redis`, SignalR utilise le backplane Redis : Redis devient indispensable au temps réel.
11. `IUnitOfWork.Commit()` est remplacé par `CommitAsync(ct)` ; `IUnitOfWork<C>` et les anciens repositories
    (`GetAll`, `Delete`…) sont supprimés.

## Vérification

`dotnet test ConnectLive.Core.CI.slnf` : **148 tests** (15 Application, 133 Core.Api). Mesures faites sur la machine de
développement avec de **vrais** Postgres 16, Redis 7 et RabbitMQ 4.1 (conteneurs Docker) : 0 échec, 0 ignoré, ~88 % de
couverture de lignes (migrations exclues). Sans ces services, 10 tests sont ignorés (pas de Redis ni RabbitMQ) ou 13 sans Postgres non plus (pas en échec). Le dépôt copié seul
dans un dossier vierge (sans dossiers voisins) se compile et passe ses tests.

La CI GitHub est verte depuis le 2026-10-20 (148 tests sans aucun ignoré, couverture 90 %, 3 images Docker construites),
après correction de deux défauts qu'elle a révélés.

**Non vérifié** : consommation de la file d'erreurs `users.service_error`, comportement du backplane sous charge,
déploiement derrière un répartiteur de charge réel.
