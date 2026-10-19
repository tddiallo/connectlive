# Authentification et autorisation

## Principe

L'API utilise des **jetons JWT Bearer** (HMAC-SHA256) émis par elle-même. Il n'y a pas (encore) de mot de passe ni de
fournisseur d'identité externe : le modèle est celui d'un **quiz live « invité »** (type Kahoot) où l'on s'enregistre
avec nom, pseudo et email, et où le jeton reçu à ce moment est la preuve d'identité.

```
POST /User/save-profile ──▶ crée l'utilisateur ──▶ { id, …, signature: { time, secret = <JWT> } }
                                                         │
        Authorization: Bearer <JWT>  ◀───────────────────┘   (toutes les autres routes)
        /sessionHub?access_token=<JWT>                          (SignalR / WebSocket)
```

## Obtenir et utiliser un jeton

1. `POST /User/save-profile` (public, limité à 10 requêtes/minute/IP) avec `userFirstName`, `lastName`, `email`,
   `nickname` — tous obligatoires, email valide.
2. La réponse contient `signature.secret` : **c'est le JWT, il n'est délivré qu'une seule fois**. Il n'est ni stocké ni
   re-calculable. Le conserver côté client.
3. Envoyer `Authorization: Bearer <jwt>` sur toutes les routes protégées. Dans Swagger UI (Development) : bouton
   **Authorize**, coller le jeton.

Contenu du jeton : `sub` (identifiant utilisateur), `nickname`, `jti`, `iat`/`nbf`/`exp`, `iss`, `aud`.
Durée de vie : `Jwt:ExpiryMinutes` (480 min par défaut).

## Qui peut faire quoi

| Ressource | Règle | Échec |
|---|---|---|
| `POST /User/save-profile` | Public, limité en débit | 400 (invalide), 409 (email déjà pris), 429 |
| `GET /User/get-profile/{id}`, `get-profiles` | Jeton valide. Ne renvoient **plus aucun secret** (`signature` = `null`) | 401 |
| `POST /Session/create` | Jeton valide ; **l'appelant devient l'animateur** (`hostUserId` du corps est ignoré) | 401 |
| `GET /Session/{id}`, `by-code/{code}`, `…/leaderboard` | Jeton valide | 401, 404 |
| `POST /Session/{id}/start`, `next`, `end` | Jeton valide **et** être l'animateur de la session | 401, **403**, 404, 409 |
| `POST /Session/{id}/answer` | Jeton valide, pas l'animateur | 401, 403, 404, 409 |
| `/Question/*` | Jeton valide ; chacun ne voit que ses questions | 401 |
| Hub `/sessionHub` | Jeton valide (`?access_token=`) ; l'identité vient du jeton | 401 |

`401` = non identifié (jeton absent, falsifié, expiré). `403` = identifié mais pas autorisé
(`ForbiddenException`, voir `ExceptionHandlerMiddleware`).

## Configuration

Section `Jwt` (classe `JwtOptions`, validée **au démarrage** : une clé absente ou trop courte empêche l'API de démarrer).

| Clé | Défaut | Rôle |
|---|---|---|
| `Jwt:SigningKey` | *(aucun)* | Clé HMAC, **32 caractères minimum**. Jamais dans `appsettings.json`. |
| `Jwt:Issuer` | `connectlive` | Émetteur attendu |
| `Jwt:Audience` | `connectlive-clients` | Audience attendue |
| `Jwt:ExpiryMinutes` | `480` | Durée de vie (1 à 1440) |

- **Development** : `appsettings.Development.json` contient une clé **publique, réservée au développement**
  (`DEV-ONLY-…`). Ne jamais l'utiliser ailleurs.
- **QA / Production** : fournir `Jwt__SigningKey` par variable d'environnement, user-secrets ou coffre de secrets, avec
  une valeur aléatoire (ex. `openssl rand -base64 48`). Changer la clé invalide tous les jetons en circulation.

## SignalR

Les WebSockets ne peuvent pas envoyer d'en-tête `Authorization` : le jeton est donc lu dans `?access_token=`,
**uniquement sur le chemin du hub** (`AuthExtensions`). Avec le client .NET :

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://api/sessionHub", o => o.AccessTokenProvider = () => Task.FromResult<string?>(jwt))
    .WithAutomaticReconnect().Build();
```

`JoinSession(sessionId)` ne prend **plus de `userId`** : l'identité est celle du jeton. *Changement cassant* par rapport
à `JoinSession(sessionId, userId)`.

## CORS

`Security:AllowedOrigins` (séparées par `;` ou `,`) est maintenant **appliqué**, avec `AllowCredentials()`.
Sans configuration : tout est autorisé en Development uniquement ; **hors Development, aucune origine n'est autorisée**
(fail closed). Voir `CorsExtensions`.

## Limites connues (à traiter ensuite)

- **Pas de reconnexion** : un jeton perdu ou expiré ne se re-délivre pas (pas de mot de passe ni de lien magique).
  Prochaine étape naturelle : endpoint de connexion (mot de passe, lien envoyé par email) ou OIDC.
- **Pas de révocation** : un jeton reste valide jusqu'à expiration (ou changement de clé).
- **Inscription ouverte** : n'importe qui peut créer une identité (limitation de débit par IP seulement). Derrière un
  proxy/ingress, configurer `ForwardedHeaders` pour que la limite s'applique à l'IP réelle et non à celle du proxy.
- Le hub ne vérifie pas que la session existe avant `JoinSession` (rejoindre un groupe inconnu est sans effet).
- Un animateur peut inclure dans sa session l'identifiant d'une question d'un autre auteur (les identifiants sont des
  GUID non devinables, mais il n'y a pas de contrôle de propriété).

## Fichiers

| Fichier | Rôle |
|---|---|
| `ConnectLive.Core/Auth/JwtOptions.cs` | Options validées au démarrage |
| `ConnectLive.Core/Auth/TokenService.cs` | Émission des jetons (`ITokenService`) |
| `ConnectLive.Core/Auth/AuthExtensions.cs` | Branchement JWT Bearer, jeton SignalR, `GetUserId()` |
| `ConnectLive.Domain/Exceptions/ForbiddenException.cs` | → HTTP 403 |
| `ConnectLive.Core/Extensions/CorsExtensions.cs` | CORS piloté par la configuration |

## Tests

`TokenServiceTests` (signature, expiration, clé invalide, options), `SessionHandlersTests` (hôte = appelant, 403 pour un
tiers), `SessionHubTests` (identité obligatoire), `Integration/AuthenticationFlowTests` (parcours HTTP réel : 401 sans
jeton ou avec jeton falsifié, enregistrement 400/409, 403 pour un non-animateur, `hostUserId` du corps ignoré,
profils sans secret).
