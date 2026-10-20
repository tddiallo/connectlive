# Intégration continue (CI)

![Pipeline CI](img/ci-pipeline.svg)

Workflow : [`.github/workflows/ci.yml`](../.github/workflows/ci.yml). Il se lance à chaque **push sur `main`** et à chaque
**pull request** (un nouveau push annule le run précédent de la même branche).

## Ce que fait chaque job

### Job 1 — `build-and-test`

| Étape | Détail |
|---|---|
| Services | **Postgres 16**, **Redis 7**, **RabbitMQ 4.1** démarrent à côté du job (avec *healthchecks*) |
| Restore / Build | `ConnectLive.Core.CI.slnf`, configuration Release |
| Tests | `dotnet test` avec les 4 variables ci-dessous : **aucun test n'est ignoré** en CI |
| Couverture | `coverlet.runsettings` (migrations exclues) ; **échec si < 80 %** des lignes (mesuré : ~88 %) |
| Rapports | artefact `test-results` conservé 7 jours ; la couverture apparaît dans le résumé du run |

Variables fournies aux tests (voir le workflow) :

| Variable | Active | Valeur en CI |
|---|---|---|
| `CONNECTLIVE_TEST_POSTGRES` | `PostgresMigrationTests` (migrations, `text[]`, unicité, concurrence) | `Host=localhost;Port=5432;Username=connectlive;Password=…` |
| `CONNECTLIVE_TEST_REDIS` | présence Redis + `SignalRBackplaneTests` (2 instances) | `localhost:6379` |
| `CONNECTLIVE_TEST_RABBITMQ` (+ `_USER`, `_PASSWORD`) | `RabbitMqTransportTests` | `rabbitmq://localhost:5672`, compte `ci` |

Pourquoi un compte `ci` pour RabbitMQ : l'utilisateur par défaut `guest` n'est accepté que depuis `localhost` *dans* le
conteneur ; le job se connecte depuis l'extérieur.

### Job 2 — `docker-images`

Construit (sans publier) les images **Core.Api**, **Notification.Api** et **Newsletter.Api**. Le dépôt est extrait dans
un sous-dossier `ConnectLive.Core/` du contexte de build, car les Dockerfiles attendent cette disposition (ils ont été
écrits pour une arborescence « un dossier parent contenant les dépôts »). Cela prouve que les images se construisent avec
**le dépôt seul**.

## Pourquoi `ConnectLive.Core.CI.slnf` et pas la solution complète

`ConnectLive.Core.sln` référence aussi `Workers`, `Connectlive.Proxy`, `Connectlive.WebHosting`, `ConnectLive.SPA`…
qui sont des **dossiers voisins non versionnés** (aucun dépôt git) : GitHub ne peut pas les récupérer.
Le filtre de solution (`.slnf`) ne garde que les 9 projets de ce dépôt.

Pour que cela soit possible, **les API ne dépendent plus des dossiers voisins** :
- `Core.Api` n'utilisait `Workers` et `Proxy` que pour des enregistrements DI inutilisés → références retirées ;
- `Notification.Api` utilisait `Connectlive.WebHosting` pour la configuration MassTransit → remplacée par
  `MessagingExtensions` ([MASSTRANSIT-FLUX.md](MASSTRANSIT-FLUX.md)) ;
- `Workers` n'était référencé (jamais utilisé) que par les `csproj` de Notification et Newsletter → retiré.

La solution complète continue d'exister pour Visual Studio (SPA, etc.).

## Reproduire la CI en local

```powershell
# Services de test (ports hôte aléatoires pour éviter tout conflit ; relever avec « docker port »)
docker run -d --name t-pg    -e POSTGRES_USER=connectlive -e POSTGRES_PASSWORD=pw -p 127.0.0.1::5432 postgres:16-alpine
docker run -d --name t-redis -p 127.0.0.1::6379 redis:7-alpine
docker run -d --name t-rmq   -p 127.0.0.1::5672 rabbitmq:4.1
docker port t-pg 5432; docker port t-redis 6379; docker port t-rmq 5672

$env:CONNECTLIVE_TEST_POSTGRES = "Host=localhost;Port=<port-pg>;Username=connectlive;Password=pw"
$env:CONNECTLIVE_TEST_REDIS    = "localhost:<port-redis>"
$env:CONNECTLIVE_TEST_RABBITMQ = "rabbitmq://localhost:<port-rmq>"     # guest/guest par défaut
dotnet test ConnectLive.Core.CI.slnf --settings coverlet.runsettings
```

Sans ces variables, les tests concernés sont **ignorés** (pas en échec) : `dotnet test` reste rapide et sans Docker.

## Mettre la CI au service du travail d'équipe

1. Pousser le dépôt sur GitHub : le workflow se déclenche tout seul.
2. *Settings → Branches → Branch protection* sur `main` : cocher **Require status checks** et choisir
   `Build + tests (Postgres, Redis, RabbitMQ)` et `Docker images (Core, Notification, Newsletter)`.
3. Une PR dont un test échoue (ou dont la couverture passe sous 80 %) ne peut alors plus être fusionnée.

## Limites connues

- **Premier run sur GitHub (2026-10-20)** : il a révélé deux défauts invisibles en local, corrigés au run suivant :
  une chaîne de connexion Postgres sans base précisée (Npgsql visait une base du nom de l'utilisateur) et des tags
  d'image Docker avec majuscules. Résultat du run vert : 145 tests sans aucun ignoré, couverture 90 %, 3 images construites.
- Avertissements restants dans le journal : actions GitHub encore sur Node 20 (migration forcée vers Node 24) et passage
  d'`ubuntu-latest` à Ubuntu 26 ; surveiller les montées de version des actions.
- Le seuil de couverture lit le plus gros rapport `cobertura` (celui de `Core.Api.Tests`) ; ajouter un projet de tests
  important demandera d'ajuster cette étape.
- Pas de déploiement (CD) : la CI s'arrête à la construction des images, sans les publier.
- Pas d'analyse de vulnérabilités des dépendances ni de scan des images (pistes : `dotnet list package --vulnerable`,
  Dependabot, Trivy).
