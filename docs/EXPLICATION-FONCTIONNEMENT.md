# ConnectLive — Explication du fonctionnement, de l’exécution et des mécanismes transverses

> ⚠ **Document historique.** Rédigé avant plusieurs évolutions (Postgres comme base unique, configuration MassTransit centralisée, backplane Redis, authentification JWT, CI). Pour l'état actuel, voir [AUDIT-360.md](AUDIT-360.md) et les documents thématiques listés dans le [README](../README.md).

Ce document explique comment l’application démarre, dans quel ordre les composants s’initialisent, comment les requêtes circulent, comment fonctionne le bus d’événements, les jobs en arrière-plan et la partie observabilité.

---

## 1) Vue d’ensemble du système

ConnectLive est une architecture ASP.NET Core orientée backend avec plusieurs services fonctionnels et un front SPA Blazor.

Les projets importants sont :

- `ConnectLive.Core/ConnectLive.Core` : API principale
- `ConnectLive.Core/ConnectLive.Application` : comportements applicatifs, middlewares, événements
- `ConnectLive.Core/ConnectLive.Domain` : modèles métier et contrats
- `ConnectLive.Core/ConntectLive.DAL` : Entity Framework Core + repository + UnitOfWork
- `ConnectLive.Core/ConnectLive.Notification.Api` : service de notifications
- `ConnectLive.Core/ConnectLive.Newsletter.Api` : service de newsletter
- `ConnectLive.SPA` : interface SPA Blazor
- `Workers` : jobs d’arrière-plan (email/newsletter)

La logique métier repose sur plusieurs principes :

- ASP.NET Core Web API
- CQRS avec MediatR
- injection de dépendances
- Repository + UnitOfWork
- logs structurés via Serilog
- patterns transverses via pipeline behaviors
- événements asynchrones via MassTransit + RabbitMQ
- jobs via Hangfire

---

## 2) Ordre d’exécution du démarrage de l’application

### 2.1. Démarrage du service principal

Le point d’entrée est :

- [ConnectLive.Core/ConnectLive.Core/Program.cs](../ConnectLive.Core/Program.cs)

Dans ce fichier, l’ordre est le suivant :

1. Le builder est initialisé avec `CreateWebHostBuilder<T>`.
2. Les services sont enregistrés dans le conteneur DI :
   - contrôleurs
   - Swagger
   - DbContext
   - repositories
   - UnitOfWork
   - AutoMapper
   - MediatR
   - cache mémoire
   - behaviors MediatR
   - CORS
3. Le `WebApplication` est construit.
4. Le pipeline HTTP est configuré :
   - Swagger en développement
   - CORS
   - HTTPS redirection en production
   - middleware d’exception personnalisée
   - mappe des contrôleurs
5. L’application écoute les requêtes.

### 2.2. Démarrage de l’extension personnalisée

La fonction de bootstrap custom est dans :

- [ConnectLive.Core/ConnectLive.Core/Extensions/HostExtensions.cs](../ConnectLive.Core/Extensions/HostExtensions.cs)

Cette extension fait plusieurs choses :

- crée un `WebApplicationBuilder`
- configure la configuration de l’application depuis :
  - `appsettings.json`
  - `appsettings.{Environment}.json`
  - variables d’environnement
  - arguments de ligne de commande
- active Serilog avec `UseSerilog`

Cela veut dire que la configuration de logging et la configuration de l’application sont centralisées avant le lancement réel du runtime.

---

## 3) Comment une requête HTTP circule dans l’application

### 3.1. Exemple de flux : `UserController`

Le point d’entrée concret est ici :

- [ConnectLive.Core/ConnectLive.Core/Controllers/UserController.cs](../ConnectLive.Core/Controllers/UserController.cs)

Le contrôleur ne fait pas la logique métier directement. Il fait juste :

1. reçoit une requête HTTP
2. construit un objet de commande ou de query
3. appelle `_mediator.Send(...)`
4. renvoie la réponse sous forme `Ok(...)` ou `NotFound(...)`

Par exemple :

- `GetProfiles()` envoie `GetUsersQuery`
- `GetProfile(Guid id)` envoie `GetUserQuery`
- `SaveUser(...)` envoie `SaveUserCommand`

### 3.2. Exemple de query

Voir :

- [ConnectLive.Core/ConnectLive.Core/Queries/UserQueries.cs](../ConnectLive.Core/Queries/UserQueries.cs)

La query `GetUsersQuery` est un objet qui représente une demande de lecture. Elle implémente :

- `IRequest<List<SaveUserResponse>>`
- `ICachable<T>`
- `IntegrationEvent`

Le handler associé exécute le vrai accès base de données avec EF Core :

- `ApplicationDbContext.Users.ToListAsync()`
- mapping AutoMapper vers les DTOs de réponse

### 3.3. Exemple de command

Voir :

- [ConnectLive.Core/ConnectLive.Core/Commands/UserCommands.cs](../ConnectLive.Core/Commands/UserCommands.cs)

La commande `SaveUserCommand` représente une écriture. Elle va :

1. capturer le DTO de requête
2. mapper vers `UserEntity`
3. appeler `IUnitOfWork.Users.AddAsync(entity)`
4. faire `Commit()`
5. récupérer l’utilisateur persisté
6. mapper la réponse

Donc le flux exact est :

`Controller -> MediatR -> Handler -> EF Core -> AutoMapper -> HTTP Response`

---

## 4) Le pattern CQRS et les behaviors transverses

### 4.1. MediatR

La configuration est dans :

- [ConnectLive.Core/ConnectLive.Core/Program.cs](../ConnectLive.Core/Program.cs)

La ligne suivante active automatiquement les handlers :

```csharp
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(MappingProfile).Assembly));
```

Cela signifie que tous les handlers `IRequestHandler<>` présents dans les assemblies chargées deviennent automatiquement disponibles dans le conteneur DI.

### 4.2. `WatchBehavior`

Voir :

- [ConnectLive.Core/ConnectLive.Application/WatchBehavior.cs](../ConnectLive.Application/WatchBehavior.cs)

Ce pipeline s’exécute avant et après chaque requête/commande. Il :

- détecte le nom de la requête
- garde un `eventId`
- mesure le temps d’exécution
- loggue le début et la fin

C’est un bon exemple de “cross-cutting concern” : le code de logging et de surveillance est déployé sans modifier chaque handler.

### 4.3. `CacheBehavior`

Voir :

- [ConnectLive.Core/ConnectLive.Application/CacheBehavior.cs](../ConnectLive.Application/CacheBehavior.cs)

Ce comportement apporte un cache générique basé sur `IMemoryCache`.

Le mécanisme est simple :

1. le type de requête vérifie s’il implémente `ICachable<T>`
2. si oui, le comportement regarde si la clé existe dans le cache
3. si oui : il répond directement depuis le cache
4. si non : il exécute le handler puis stocke le résultat avec un TTL

Exemple de logique :

```csharp
if (_cache.TryGetValue(cacheRequest.Key, out TResponse cachedData))
{
    return cachedData;
}

cachedData = await next();
_cache.Set(cacheRequest.Key, cachedData, options);
```

Le but est d’éviter les accès répétés à la base pour les lectures très fréquentes.

---

## 5) Le pattern event-driven / publish-subscribe

### 5.1. Événement de base

Le fichier :

- [ConnectLive.Core/ConnectLive.Application/IntegrationEvent.cs](../ConnectLive.Application/IntegrationEvent.cs)

La classe `IntegrationEvent` sert de base pour tout message de type événement. Chaque événement contient :

- `EventId` : identifiant unique

Cela permet de tracer et corréler des événements dans les logs et la chaîne d’exécution.

### 5.2. Exemple de contrat d’événement

Voir :

- [ConnectLive.Core/ConnectLive.Domain/Contracts/UserCreatedContractQuery.cs](../ConnectLive.Domain/Contracts/UserCreatedContractQuery.cs)

C’est le contrat qui est transporté dans le bus. Il contient des infos métier comme :

- `Email`
- `FullName`

### 5.3. Consumer RabbitMQ / MassTransit

Voir :

- [ConnectLive.Core/ConnectLive.Application/BusEvents/UserCreatedConsumer.cs](../ConnectLive.Application/BusEvents/UserCreatedConsumer.cs)

Le consumer implémente `IConsumer<UserCreatedContractQuery>`.

Cela signifie :

- il s’abonne aux messages de type `UserCreatedContractQuery`
- quand un message arrive, MassTransit le dépose dans la queue RabbitMQ
- le consumer exécute la méthode `Consume(...)`

Le code actuel ne fait qu’un log :

```csharp
_logger.LogInformation($"->->->-> [RabbitMQ] Welcome ({context.Message.FullName}) - ({context.Message.Email})");
```

C’est un exemple minimal d’événement poussé vers un autre service.

### 5.4. Comment le publish/subscribe fonctionne concrètement

Le bus est configuré dans deux endroits :

- [ConnectLive.Core/ConnectLive.Core/Extensions/HostExtensions.cs](../ConnectLive.Core/Extensions/HostExtensions.cs)
- [Connectlive.WebHosting/WebHostExtensions.cs](../../Connectlive.WebHosting/WebHostExtensions.cs)

La logique est la suivante :

1. le service appelle `AddMassTransit(...)`
2. on insère un consumer : `x.AddConsumer<UserCreatedConsumer>()`
3. on configure une queue RabbitMQ : `users.service`
4. sur cette queue, on “reçoit” les messages et on exécute le consumer

Schéma logique :

```text
Publisher (API) --> RabbitMQ --> queue "users.service" --> Consumer --> logique métier / logs / notifications
```

Le forward est donc asynchrone : le service appelant ne bloque pas sur le traitement du consumer.

### 5.5. Pourquoi le code est “prêt mais pas activé”

Dans [ConnectLive.Core/ConnectLive.Core/Program.cs](../ConnectLive.Core/Program.cs), la ligne de configuration du bus est commentée :

```csharp
//builder.Services.AddBusPublisherRegistration(builder.Configuration);
```

Et la configuration `Hangfire` est également commentée.

Cela veut dire que l’architecture est préparée, mais pas branchée en local dans le runtime principal.

Le projet a le bon squelette, mais le flux réel n’est pas encore activé dans les endpoints de production.

### 5.6. Exemple d’un système publish/subscribe complet

Le schéma typique serait :

```csharp
await publishEndpoint.Publish(new UserCreatedContractQuery
{
    Email = user.Email,
    FullName = user.FirstName + " " + user.LastName
});
```

Et le consumer reçoit ensuite le message sur `users.service` et exécute le traitement associé.

Le point important est que le producteur n’a pas besoin de connaître l’implémentation du consumer. Il publie juste un message : c’est le bus qui s’occupe de la distribution.

---

## 6) Les background jobs

### 6.1. Hangfire

Dans les services `Notification.Api` et `Newsletter.Api`, la configuration est active :

- [ConnectLive.Core/ConnectLive.Notification.Api/Program.cs](../ConnectLive.Notification.Api/Program.cs)
- [ConnectLive.Core/ConnectLive.Newsletter.Api/Program.cs](../ConnectLive.Newsletter.Api/Program.cs)

La configuration fait :

```csharp
builder.Services.AddHangfire(x => x.UsePostgreSqlStorage(...));
builder.Services.AddHangfireServer(options => options.Queues = ["email"]);
```

Ou :

```csharp
builder.Services.AddHangfireServer(options => options.Queues = ["newsletter"]);
```

### 6.2. Ce que cela signifie

- Hangfire stocke les jobs dans PostgreSQL
- le worker exécute les tâches en arrière-plan
- les jobs sont FIFO par queue
- un service peut traiter uniquement certaines files

### 6.3. Les workers

Voir :

- [Workers/EmailWorker.cs](../../Workers/EmailWorker.cs)

Le worker définit plusieurs méthodes avec des attributs :

```csharp
[Hangfire.Queue("email")]
public void SendEmail(...)
```

et :

```csharp
[Hangfire.Queue("newsletter")]
public void SendNewsletter(...)
```

Ainsi, le système sait dans quelle file déposer chaque job :

- email → queue `email`
- newsletter → queue `newsletter`

### 6.4. Exemple de fonctionnement

Le flux typique est :

1. un événement ou une requête demande l’envoi d’un email
2. le code envoie un job Hangfire
3. Hangfire le sauvegarde en base PostgreSQL
4. le worker `EmailWorker` le traite plus tard
5. la méthode loggue l’action ou déclenche l’envoi réel

Le code de la méthode est actuellement un log, donc c’est une base de démonstration d’archivage de job, pas encore la vraie couche d’envoi mail de production.

---

## 7) Les notifications

La notification est un concept transversal dans ce projet.

### 7.1. La notification “UserCreated”

Voir :

- [ConnectLive.Core/ConnectLive.Domain/Contracts/UserCreatedContractQuery.cs](../ConnectLive.Domain/Contracts/UserCreatedContractQuery.cs)

C’est le contrat d’événement utilisé lors de la création utilisateur.

### 7.2. Le service de notification

Le `Notification.Api` reçoit les jobs et/ou les messages de bus. Sa responsabilité est de traiter l’utilisateur créé pour :

- envoyer un message de bienvenue
- déclencher des notifications
- peut-être connecter avec le système de newsletter ou mailer

### 7.3. Rôle concret du consumer

Dans `UserCreatedConsumer`, le code consomme le message puis log une notification :

```csharp
_logger.LogInformation($"->->->-> [RabbitMQ] Welcome ({context.Message.FullName}) - ({context.Message.Email})");
```

Donc, à ce stade, la notification est construite comme un “écouteur” qui reçoit l’événement de création, puis réagit.

Le concept est très solide :

- “user created”
- consumer écoute ce message
- notification / welcome / job / autre service peuvent ensuite intervenir

---

## 8) Les extensions et méthodes d’extension

Les méthodes d’extension sont très importantes dans ce projet. Elles encapsulent la configuration réutilisable du runtime ASP.NET Core.

### 8.1. `CreateWebHostBuilder<T>`

Voir :

- [ConnectLive.Core/ConnectLive.Core/Extensions/HostExtensions.cs](../ConnectLive.Core/Extensions/HostExtensions.cs)

Cette méthode crée un `WebApplicationBuilder` avec :

- configuration principale
- environnement
- Serilog
- variables d’environnement
- arguments

Cela évite de réécrire la même logique dans chaque service.

### 8.2. `UseSerilog(...)`

La même classe ajoute un logger Serilog personnalisé à la pipeline de logging.

Elle configure :

- nom de l’application
- nom d’hôte
- IP
- enrichissement de log contextuel
- sortie vers Seq
- lecture à partir de la configuration

C’est le cœur de l’observabilité.

### 8.3. `UseCustomException(...)`

Voir :

- [ConnectLive.Core/ConnectLive.Application/Extensions/WebHostExtensions.cs](../ConnectLive.Application/Extensions/WebHostExtensions.cs)

Cette méthode d’extension ajoute un middleware de gestion d’exception au pipeline ASP.NET Core.

Le middleware :

- intercepte les exceptions non gérées
- identifie le type d’exception
- retourne un code HTTP adapté
- loggue avec Serilog
- masque les détails sensibles en production

### 8.4. `AddBusPublisherRegistration` / `AddBusRegistration`

Ce sont des méthodes d’extension qui centralisent le branchement du bus RabbitMQ / MassTransit.

Elles permettent de ne pas répéter la configuration dans chaque projet.

---

## 9) Observabilité et supervision

### 9.1. Serilog + Seq

Le projet est prêt pour la centralisation des logs :

- `UseSerilog` configure flux de logs
- `Seq` reçoit les traces structurées
- la configuration des services est conçue pour envoyer les logs vers Seq

La stack est donc organisée pour :

- debug local
- traçabilité des requêtes
- suivi des événements
- monitoring de la plateforme

### 9.2. `WatchBehavior`

Le pipeline `WatchBehavior` est un instrument de mesure de temps d’exécution. Il sert de mécanisme de monitoring applicatif.

### 9.3. `ExceptionHandlerMiddleware`

Le middleware centralise la gestion des erreurs. C’est un bon signal d’architecture de production : on ne laisse pas les exceptions “monter” vers le client de façon brute.

### 9.4. `OpenTelemetry` n’est pas encore centralisé

Le projet a un bon noyau de monitoring, mais pas encore une full observabilité moderne complète :

- metrics
- traces distribuées
- dashboards production
- corrélation cross-service

C’est le prochain grand niveau de maturité.

---

## 10) Comment exécuter l’application

### 10.1. Compiler la solution

Depuis le dossier racine :

```powershell
cd "c:\Users\TidianeDiallo\Desktop\projects\asp-net\connectLive\repos\ConnectLive.Core"
dotnet build "ConnectLive.Core.sln"
```

J’ai vérifié cette commande : elle compile correctement sans erreur de build.

### 10.2. Démarrer le support d’infra Docker

Les services externes sont décrits dans :

- [ConnectLive.Core/docker-compose.yml](../docker-compose.yml)

Pour démarrer les conteneurs (Seq, RabbitMQ, PostgreSQL, etc.) :

```powershell
cd "c:\Users\TidianeDiallo\Desktop\projects\asp-net\connectLive\repos\ConnectLive.Core"
docker compose up -d
```

### 10.3. Démarrer l’API principale

```powershell
cd "c:\Users\TidianeDiallo\Desktop\projects\asp-net\connectLive\repos\ConnectLive.Core"
dotnet run --project "ConnectLive.Core\ConnectLive.Core.Api.csproj"
```

### 10.4. Démarrer le service de notification

```powershell
cd "c:\Users\TidianeDiallo\Desktop\projects\asp-net\connectLive\repos\ConnectLive.Core"
dotnet run --project "ConnectLive.Notification.Api\ConnectLive.Notification.Api.csproj"
```

### 10.5. Démarrer le service de newsletter

```powershell
cd "c:\Users\TidianeDiallo\Desktop\projects\asp-net\connectLive\repos\ConnectLive.Core"
dotnet run --project "ConnectLive.Newsletter.Api\ConnectLive.Newsletter.Api.csproj"
```

### 10.6. Accéder à Swagger

L’API principale expose Swagger en mode développement :

- `https://localhost:<port>/swagger`

Les ports réels dépendent de la configuration de lancement locale.

---

## 11) Ce qui est déjà bien conçu et ce qu’il manque encore

### Ce qui est bien

- architecture en couches claire
- séparation API / Application / Domain / Infrastructure
- CQRS + MediatR
- behaviors de logs et cache
- exception handling centralisé
- sérilog + seq
- event-driven architecture prévue
- jobs Hangfire préparés
- services séparés pour notification/newsletter

### Ce qui manque encore pour le vrai produit

- activation effective du bus RabbitMQ dans le flux principal
- publication réelle des événements lors des créations
- authentification/autorisation
- SignalR pour les sessions temps réel
- Redis distribué pour le cache partagé
- tests automatisés unit/integration
- modèle métier complet Session / Question / Leaderboard
- vraie logique de notification / email / welcome

---

## 12) Résumé très simple du fonctionnement

Le fonctionnement de l’application est le suivant :

1. l’API démarre et enregistre ses services
2. une requête HTTP arrive sur un contrôleur
3. le contrôleur envoie une commande ou une query via MediatR
4. le handler traite la logique ou la lecture
5. EF Core accède à la base
6. une réponse est renvoyée au client
7. des behaviors transverses font du logging/cache
8. certains événements peuvent être publiés dans RabbitMQ
9. les consumers exécutent des traitements asynchrones
10. les jobs Hangfire traitent les tâches de fond (email, newsletter)
11. Serilog/Seq centralisent le monitoring

C’est une architecture moderne et saine, avec une base de fondations déjà solide, mais incomplète au niveau produit et runtime réel.

---

## 13) Conclusion

Le système montre clairement la bonne direction :

- architecture propre
- séparations de responsabilités
- comportement transversal bien pensé
- intégration message-driven
- background jobs préparés
- observabilité déjà en place

Le point clé est que le projet est plus un “socle d’architecture” qu’un produit complet. Le cœur du vrai produit “session live” reste à construire.

C’est exactement le bon moment pour continuer dans cette direction avec :

- SignalR
- Redis distribué
- auth
- session / leaderboard
- bus réellement utilisé
- tests et CI/CD



📚 Liens de documentation ajoutés dans le document
ASP.NET Core : https://learn.microsoft.com/aspnet/core
MediatR : https://github.com/jbogard/MediatR
EF Core : https://learn.microsoft.com/ef/core
RabbitMQ : https://www.rabbitmq.com/
MassTransit : https://masstransit.io/
Hangfire : https://www.hangfire.io/
Serilog : https://serilog.net/
Seq : https://datalust.co/seq
SignalR : https://learn.microsoft.com/aspnet/core/signalr
Redis distributed cache : https://learn.microsoft.com/aspnet/core/performance/caching/distributed
Blazor : https://learn.microsoft.com/aspnet/core/blazor
