# Questions, réponses des joueurs et classement

## Parcours complet d'une partie

```
Animateur                                       Joueurs
─────────                                       ───────
POST /Question/save  (×N)
POST /Session/create {questionIds}  ──▶ code
POST /Session/{id}/start  ───────────────▶ QuestionChanged (sans bonne réponse)
                                          POST /Session/{id}/answer {questionId, answer}
                                                 └─▶ correction serveur ─▶ LeaderboardUpdated (tous)
POST /Session/{id}/next   ───────────────▶ QuestionChanged … puis SessionEnded
                                          GET /Session/{id}/leaderboard   (resynchronisation)
```

## Questions

`POST /Question/save` (authentifié) — `CreateQuestionRequest` :

| Champ | Règle |
|---|---|
| `question` | obligatoire, ≤ 500 caractères |
| `category` | optionnelle, ≤ 100 caractères |
| `choice1` … `choice4` | obligatoires, ≤ 200 caractères, **distincts** |
| `correctAnswer` | obligatoire, **identique (casse comprise) à l'un des quatre choix** |
| `id` | **ignoré** : l'identifiant est généré par le serveur |

Réponse : `Result<CreateQuestionResponse>` avec l'`id` à passer à `POST /Session/create`. Invalide → **400**.

`GET /Question/all` renvoie **uniquement les questions de l'appelant**, avec `correctAnswer` (c'est l'auteur). Les
joueurs ne reçoivent jamais la bonne réponse : ils voient `SessionQuestionDto`.

Pas encore de modification ni de suppression de question (hors périmètre ; `UpdateQuestionRequest` reste inutilisé).

## Réponse d'un joueur

`POST /Session/{id}/answer` — corps `SubmitAnswerRequest { questionId, answer }`. Règles, vérifiées dans cet ordre
par `SubmitAnswerCommandHandler` :

| Règle | Échec |
|---|---|
| Session existante | 404 |
| L'appelant n'est **pas l'animateur** | 403 |
| Session `Running` | 409 |
| `questionId` = question **courante** (protège d'une réponse tardive à la question précédente) | 409 |
| `answer` est un des choix proposés | 400 |
| Une seule réponse par joueur et par question | 409 |

- **Correction côté serveur** : la bonne réponse ne quitte jamais l'API avant (hors vue auteur).
- **Barème** : `SubmitAnswerCommandHandler.PointsPerCorrectAnswer` = **100** points par bonne réponse, 0 sinon.
  Pas encore de bonus de rapidité (`AnsweredAt` est enregistré pour l'ajouter plus tard).
- **Unicité garantie en base** : index unique (`session-id`, `question-id`, `user-id`) sur `session_answers`. Deux
  requêtes simultanées du même joueur : la seconde reçoit 409, jamais un double score.
- Réponse : `AnswerResultResponse { questionId, isCorrect, points, totalScore }`, visible par le joueur seul.
  Il apprend donc si sa réponse est juste immédiatement ; il pourrait le répéter à un autre joueur hors de l'application
  (accepté pour cette version).

## Classement

- `GET /Session/{id}/leaderboard` (authentifié) → liste de `LeaderboardEntryDto { userId, nickname, rank, score }`.
  404 si la session n'existe pas ; liste vide si personne n'a répondu.
- **Temps réel** : à chaque réponse enregistrée, l'événement `LeaderboardUpdated` envoie le classement complet au
  groupe de la session (après la persistance, comme les transitions : la base fait foi).
- **Rang « compétition »** : les ex æquo partagent le rang (`1, 1, 3`). À égalité, ordre alphabétique sur le pseudo,
  pour une sortie stable (`Leaderboard.Rank`).
- Seuls les joueurs **ayant répondu au moins une fois** apparaissent.

Le type `LeaderboardModel` (Domain) est remplacé par `LeaderboardEntryDto` (Portal.Shared) dans
`ISessionNotifier.NotifyLeaderboardUpdatedAsync`. `LeaderboardModel` n'est plus utilisé.

## Modèle de données

Nouvelle table `session_answers` (migration `AddSessionAnswers`) :

| Colonne | Rôle |
|---|---|
| `id` | clé primaire |
| `session-id` | FK → `sessions` (suppression en cascade) |
| `question-id`, `user-id` | qui a répondu à quoi |
| `answer`, `is-correct`, `points` | réponse et correction **figées** au moment de la réponse |
| `answered-at` | horodatage UTC |

Appliquer : `dotnet ef database update --project ConntectLive.DAL --startup-project ConnectLive.Core\ConnectLive.Core.Api.csproj`.

## Fichiers

| Couche | Fichiers |
|---|---|
| DAL | `Entity/SessionAnswerEntity`, `ApplicationDbContext` (`SessionAnswers`), `IUnitOfWork.SessionAnswers`, migration |
| Portal.Shared | `Request/Questions/CreateQuestionRequest`, `Request/Sessions/SubmitAnswerRequest`, `Response/Questions/CreateQuestionResponse`, `Response/Sessions/AnswerResultResponse`, `Realtime/LeaderboardEntryDto` |
| Core.Api | `Commands/QuestionCommands`, `Queries/QuestionQueries`, `Questions/QuestionMapping`, `Commands/SessionCommands` (région `SubmitAnswer`), `Queries/SessionQueries` (`GetLeaderboard`), `Sessions/Leaderboard`, `Controllers/QuestionController`, `Controllers/SessionController` |

## Limites connues

- Pas de modification/suppression de questions, ni de contrôle de propriété à l'ajout dans une session.
- Notification du classement à **chaque** réponse, avec la liste complète : correct pour quelques dizaines de joueurs,
  à regrouper/limiter (ex. toutes les 500 ms) au-delà.
- Pas de révélation de la bonne réponse à la fin de chaque question, ni de délai maximum pour répondre.
- Un animateur qui rejoint une autre session comme joueur est traité comme n'importe quel joueur de *cette* session.

## Tests

`LeaderboardTests` (rang, ex æquo, stabilité), `AnswerHandlersTests` (correction, unicité, question courante, choix
invalide, session non démarrée, animateur interdit, classement diffusé et cumulé), `QuestionHandlersTests` (création,
propriété, validation) et `Integration/GameFlowTests` (partie complète via HTTP : questions → session → réponse →
classement, doublon 409, animateur 403, isolation des questions, 400).
