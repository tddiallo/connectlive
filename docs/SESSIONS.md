# Sessions de quiz — conception

## Objectif

Permettre à un animateur de **créer** une session à partir de questions existantes, de la **démarrer**, de **faire
défiler les questions** puis de la **terminer**, pendant que les joueurs rejoignent via un **code** court et
reçoivent chaque question en temps réel (voir [SIGNALR-REALTIME.md](SIGNALR-REALTIME.md)).

Hors périmètre de cette itération (prévu ensuite) : réponses des joueurs, scores et classement, authentification.

## Machine à états

```
 Lobby ──Start──▶ Running ──Next──▶ Running … ──Next (dernière question)──▶ Ended
   │                 │
   └──────End────────┴──────End──────────────────────────────────────────▶ Ended
```

| Transition | Précondition | Effet | Événement temps réel |
|---|---|---|---|
| `Create` | titre 1–100 car., ≥ 1 question, questions distinctes et existantes | session en `Lobby`, code unique | — |
| `Start` | `Lobby` | `Running`, question n°0, `StartedAt` | `QuestionChanged` |
| `Next` | `Running` | question suivante ; si c'était la dernière → `Ended` | `QuestionChanged` ou `SessionEnded` |
| `End` | `Lobby` ou `Running` | `Ended`, `EndedAt` | `SessionEnded` |

Une transition invalide lève `ConflictException` → **HTTP 409**. Les règles vivent **dans `SessionEntity`**
(méthodes `Start`/`MoveNext`/`End`), donc testables sans base ni HTTP.

## Modèle de données

| Table | Colonnes principales |
|---|---|
| `sessions` | `id`, `code` (unique, 6 car.), `title`, `host-user-id`, `status` (texte), `current-question-index` (−1 avant le démarrage), `created-at`, `started-at?`, `ended-at?`, `version` |
| `session_questions` | `id`, `session-id` (FK, cascade), `question-id`, `order` ; unique (`session-id`, `order`) |

- **Code de session** : 6 caractères tirés de `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (sans `I O 0 1` ambigus) par
  `RandomNumberGenerator`. Collision → nouveau tirage (index unique en filet de sécurité).
- **`version`** (`[ConcurrencyCheck]`) est incrémenté à chaque transition : deux animateurs qui cliquent « suivant » en
  même temps → le second reçoit une `DbUpdateConcurrencyException` plutôt qu'un saut de deux questions.
- Les questions d'une session sont **référencées** (pas copiées) : modifier une question en cours de session se voit
  immédiatement. Choix assumé pour l'instant.

## API

Les succès utilisent l'enveloppe `Result<T>` (`successed`, `data`, `messages`). Les erreurs sont renvoyées par
`ExceptionHandlerMiddleware` sous la forme `{ "error": "…" }` avec le code HTTP correspondant (le message métier est
conservé pour les 4xx ; les 500 restent opaques hors Development).

| Verbe & route | Rôle | Erreurs |
|---|---|---|
| `POST /Session/create` | Crée la session (`CreateSessionRequest`) | 400 validation / question inconnue |
| `GET /Session/{id}` | État d'une session | 404 |
| `GET /Session/by-code/{code}` | Résolution d'un code saisi par un joueur (insensible à la casse) | 404 |
| `POST /Session/{id}/start` | `Lobby → Running` | 404, 409 |
| `POST /Session/{id}/next` | Question suivante / fin | 404, 409 |
| `POST /Session/{id}/end` | Fin anticipée | 404, 409 |

Correspondance exceptions → HTTP dans `ExceptionHandlerMiddleware` : `ArgumentException` 400, `KeyNotFoundException` 404,
`ConflictException` 409.

## Sécurité des données temps réel

Le modèle `QuestionModel` contient `CorrectAnswer`. Pour ne **jamais** envoyer la bonne réponse aux joueurs,
`ISessionNotifier.NotifyQuestionChangedAsync` prend désormais un `SessionQuestionDto` (texte, catégorie, choix,
position/total) **sans** réponse correcte. La correction restera côté serveur.

## Découpage du code

| Couche | Fichiers |
|---|---|
| Domain | `Exceptions/ConflictException` |
| DAL | `Entity/SessionEntity` (+ règles), `SessionQuestionEntity`, `SessionStatus`, `ApplicationDbContext`, migration `AddSessions` |
| Portal.Shared | `Request/Sessions/CreateSessionRequest`, `Response/Sessions/SessionResponse`, `Realtime/SessionQuestionDto` |
| Core.Api | `Commands/SessionCommands`, `Queries/SessionQueries`, `Sessions/SessionCodeGenerator`, `Sessions/SessionMapping`, `Controllers/SessionController` |

## Limites connues

- Pas d'authentification : n'importe qui peut démarrer/avancer une session (le `HostUserId` est enregistré mais non
  contrôlé). À traiter avec l'authentification.
- `Next` envoie l'événement après le `Commit` ; si l'envoi SignalR échoue, l'état en base a déjà avancé (les clients
  peuvent se resynchroniser avec `GET /Session/{id}`).

## Vérification

| Niveau | Couverture |
|---|---|
| Règles de domaine (`SessionEntityTests`) | création (validation titre/questions), `Start`, `MoveNext`, `End`, `CurrentQuestionId`, versionnage |
| Handlers sur SQLite en mémoire (`SessionHandlersTests`) | création + code, lecture par id/code, cycle complet, notifications émises (et pas de `CorrectAnswer`), 404/409, **conflit de concurrence réel** |
| Générateur de code (`SessionCodeGeneratorTests`) | longueur, alphabet sans caractères ambigus, variabilité |
| Exécution réelle (API + SQLite + client SignalR) | création → by-code → start → next → fin ; 409 (démarrage en double, `next` en `Lobby`/`Ended`), 404, 400 ; le client reçoit `QuestionChanged` ×2 puis `SessionEnded` |

Mise en route locale : `dotnet ef database update --project ConntectLive.DAL --startup-project ConnectLive.Core\ConnectLive.Core.Api.csproj`.
Les questions ne peuvent pas encore être créées via l'API (`QuestionController` est un mock) : insérer des lignes dans
`questions` pour tester.
