// Génère architecture-couches.svg et architecture-detaillee.svg (docs/img). Voir docs/img/src/README.md.
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { rect, text, logoTile, arrow, path, markers, svgDocument } from './lib.mjs';

const outDir = join(dirname(fileURLToPath(import.meta.url)), '..');
const C = { card: '#15203a', deep: '#0f1830', line: '#2c3d63', mute: '#9fb0cf', blue: '#60a5fa', green: '#4ade80', orange: '#fb923c', red: '#f87171', violet: '#a78bfa', cyan: '#22d3ee', yellow: '#facc15' };
const defs = (extra = '') => `<defs><linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#0a1020"/><stop offset="1" stop-color="#101b33"/></linearGradient></defs>` +
  markers({ arr: '#9fb0cf', aBlue: C.blue, aGreen: C.green, aOrange: C.orange, aRed: C.red, aCyan: C.cyan, aViolet: C.violet });
const lines = (x, y, arr, { size = 19, fill = C.mute, gap = 26, anchor = 'start' } = {}) => arr.map((l, i) => text(x, y + i * gap, l, { size, fill, anchor })).join('');

/* ───────────────────────────── 1. Couches du code ───────────────────────────── */
{
  const W = 1500, H = 960;
  let s = `<rect width="${W}" height="${H}" fill="url(#bg)"/>`;
  s += text(40, 62, 'Couches du code — qui dépend de qui', { size: 40, weight: 800 });
  s += text(40, 98, 'Une flèche X → Y signifie « X dépend de Y ». Domain et Portal.Shared ne dépendent de rien.', { size: 21, fill: C.mute });

  const box = (x, y, w, h, title, sub, color, subs = []) =>
    rect(x, y, w, h, { fill: C.card, stroke: color, sw: 3 }) + text(x + 22, y + 40, title, { size: 28, weight: 800 }) +
    text(x + 22, y + 70, sub, { size: 20, fill: color, weight: 600 }) + lines(x + 22, y + 104, subs, { size: 19 });

  // Hôtes (racines de composition)
  s += text(40, 150, 'HÔTES  (démarrent l’application, branchent l’injection de dépendances)', { size: 20, weight: 700, fill: C.cyan });
  s += box(40, 170, 640, 250, 'ConnectLive.Core.Api', 'API principale — ASP.NET Core 8', C.blue,
    ['Controllers/ — routes HTTP minces', 'Commands/ Queries/ — handlers MediatR par fonctionnalité', 'Auth/ — JWT · Realtime/ — hub SignalR, présence, notifier', 'Extensions/ — CORS, Redis, santé, migrations · Program.cs']);
  s += box(710, 170, 360, 250, 'Notification.Api', 'Consommateur d’événements', C.orange,
    ['Consumer MassTransit', '(UserCreated → file users.service)', 'Serveur Hangfire « email »']);
  s += box(1100, 170, 360, 250, 'Newsletter.Api', 'Jobs planifiés', C.violet,
    ['Serveur Hangfire « newsletter »', 'Stockage des jobs : PostgreSQL']);

  // Bibliothèques
  s += text(1460, 500, 'BIBLIOTHÈQUES PARTAGÉES', { size: 20, weight: 700, fill: C.cyan, anchor: 'end' });
  s += box(510, 520, 440, 220, 'ConnectLive.Application', 'Briques transverses', C.green,
    ['MediatR : WatchBehavior, CacheBehavior', 'Middleware d’exceptions (ProblemDetails)', 'Messaging : config MassTransit unique', 'BusEvents : consumers']);
  s += box(40, 520, 440, 220, 'ConntectLive.DAL', 'Accès aux données', C.cyan,
    ['EF Core 9 + Npgsql', 'Entités : SessionEntity porte les règles', 'Repository + Unit of Work (async)', 'Migrations PostgreSQL']);
  s += box(980, 520, 480, 220, 'ConnectLive.Portal.Shared', 'Contrats d’API', C.yellow,
    ['Requêtes / réponses (DTO) · Result<T>', 'SessionHubEvents : noms d’événements temps réel', 'LeaderboardEntryDto, SessionQuestionDto…']);

  // Socle
  s += text(40, 820, 'SOCLE', { size: 20, weight: 700, fill: C.cyan });
  s += box(40, 836, 640, 96, 'ConnectLive.Domain', '', C.mute, []);
  s += text(62, 902, 'Contrats de messages · exceptions métier (409, 403…)', { size: 19, fill: C.mute });

  // Tests
  s += rect(710, 836, 750, 96, { fill: C.deep, stroke: C.line, dash: '8 6' });
  s += text(732, 870, 'Tests : Core.Api.Tests (133) · Application.Tests (15)', { size: 21, weight: 700 });
  s += text(732, 902, 'xUnit · NSubstitute · WebApplicationFactory · services réels en CI', { size: 19, fill: C.mute });

  // Flèches (dépendances)
  s += arrow(200, 420, 200, 518, { color: C.cyan, id: 'aCyan' });      // Core -> DAL
  s += arrow(480, 420, 600, 518, { color: C.green, id: 'aGreen' });    // Core -> Application
  s += arrow(640, 420, 1100, 518, { color: C.yellow, id: 'arr' });     // Core -> Portal.Shared
  s += arrow(840, 420, 840, 518, { color: C.green, id: 'aGreen' });    // Notification -> Application
  s += arrow(260, 742, 260, 834, { color: C.mute, id: 'arr' });        // DAL -> Domain
  s += arrow(640, 742, 500, 834, { color: C.mute, id: 'arr' });        // Application -> Domain

  writeFileSync(join(outDir, 'architecture-couches.svg'), svgDocument(W, H, defs() + s));
}

/* ───────────────────────── 2. Parcours d'une requête, composants et ports ───────────────────────── */
{
  const W = 1840, H = 1320;
  let s = `<rect width="${W}" height="${H}" fill="url(#bg)"/>`;
  s += text(40, 62, 'ConnectLive — architecture détaillée', { size: 42, weight: 800 });
  s += text(40, 100, 'Du navigateur à la base de données : ce que traverse une requête, et avec qui l’API parle.', { size: 22, fill: C.mute });

  // Clients
  s += text(40, 170, 'CLIENTS', { size: 20, weight: 700, fill: C.cyan });
  const client = (y, icon, t, l1, l2) => rect(40, y, 330, 150, { fill: C.card, stroke: C.line }) + logoTile(58, y + 18, 70, icon, { r: 14 }) +
    text(144, y + 54, t, { size: 24, weight: 800 }) + text(144, y + 84, l1, { size: 19, fill: C.mute }) + text(58, y + 128, l2, { size: 19, fill: C.mute });
  s += client(190, 'swagger', 'Swagger UI', 'OpenAPI · bouton Authorize', '/swagger (Development)');
  s += client(370, 'blazor', 'Blazor WASM', 'SPA · MudBlazor', 'http://localhost:5279');
  s += rect(40, 560, 330, 232, { fill: C.deep, stroke: C.line, dash: '8 6' });
  s += text(58, 596, 'Deux protocoles', { size: 21, weight: 800 });
  s += lines(58, 628, ['REST / JSON  (Bearer JWT)', 'WebSocket SignalR', '  /sessionHub?access_token=…', '', 'Erreurs : problem+json', '  (RFC 9457) + traceId'], { size: 19 });

  // Core.Api
  const cx = 440, cw = 760;
  s += rect(cx, 130, cw, 1150, { fill: '#0d1730', stroke: C.blue, sw: 3 });
  s += logoTile(cx + 20, 148, 64, 'dotnet', { r: 12 });
  s += text(cx + 102, 190, 'Core.Api — ASP.NET Core 8', { size: 30, weight: 800 });
  s += text(cx + cw - 20, 166, 'http://localhost:5101 · Docker :8080', { size: 18, fill: C.mute, anchor: 'end' });

  const step = (y, h, n, title, l, color) => rect(cx + 24, y, cw - 48, h, { fill: C.card, stroke: color, sw: 2 }) +
    `<circle cx="${cx + 58}" cy="${y + 34}" r="18" fill="${color}"/>` + text(cx + 58, y + 41, String(n), { size: 20, weight: 800, fill: '#0a1020', anchor: 'middle' }) +
    text(cx + 90, y + 40, title, { size: 23, weight: 800 }) + lines(cx + 90, y + 70, l, { size: 19 });
  const down = (y) => arrow(cx + cw / 2, y, cx + cw / 2, y + 28, { id: 'arr', width: 3 });

  s += step(220, 118, 1, 'Pipeline HTTP (Program.cs)', ['ExceptionHandler → CORS → HTTPS → RateLimiter →', 'Authentication (JWT) → Authorization'], C.orange);
  s += down(338);
  s += step(366, 96, 2, 'Controllers minces  ·  SessionHub [Authorize]', ['Traduisent HTTP → _mediator.Send(...) ; identité = User.GetUserId()'], C.blue);
  s += down(462);
  s += step(490, 118, 3, 'MediatR — pipeline de comportements', ['WatchBehavior (log + chrono) → CacheBehavior (IDistributedCache)', 'si la requête implémente ICachable'], C.violet);
  s += down(608);
  s += step(636, 96, 4, 'Handler (Commands/ · Queries/)', ['Reçoit IServiceProvider → GetRequiredService<T>() ; règles dans le domaine'], C.green);

  // Collaborateurs alignés avec l'infrastructure
  const collab = (y, title, l, color) => rect(cx + 24, y, cw - 48, 108, { fill: C.card, stroke: color, sw: 2 }) + text(cx + 46, y + 38, title, { size: 22, weight: 800 }) + lines(cx + 46, y + 68, l, { size: 19 });
  s += text(cx + 24, 770, 'Le handler s’appuie sur :', { size: 19, weight: 700, fill: C.cyan });
  s += collab(790, 'IUnitOfWork + IGenericRepository  →  EF Core 9', ['SessionEntity (règles, version = concurrence optimiste), CommitAsync(ct)'], C.cyan);
  s += collab(920, 'IDistributedCache · ISessionPresenceTracker · backplane SignalR', ['ISessionNotifier = ResilientSessionNotifier → SignalRSessionNotifier'], C.red);
  s += collab(1050, 'IPublishEndpoint (MassTransit)', ['UserCreated publié après commit — au mieux, perte tolérée (outbox = suite)'], C.orange);
  s += collab(1180, 'ILogger  (Serilog)  ·  ITokenService (JWT)  ·  TimeProvider', ['Logs structurés étiquetés par ClassInfo ; jetons HMAC-SHA256'], C.violet);

  // Infrastructure (colonne alignée)
  const ix = 1290, iw = 260;
  s += text(ix, 770, 'INFRASTRUCTURE', { size: 20, weight: 700, fill: C.cyan });
  const infra = (y, icon, t, l1, l2, color) => rect(ix, y, iw, 108, { fill: C.card, stroke: color, sw: 3 }) +
    (icon ? logoTile(ix + 14, y + 16, 60, icon, { r: 12 }) : rect(ix + 14, y + 16, 60, 60, { fill: '#fff', r: 12 }) + text(ix + 44, y + 56, 'Seq', { size: 22, fill: '#0f172a', weight: 800, anchor: 'middle' })) +
    text(ix + 88, y + 44, t, { size: 23, weight: 800 }) + text(ix + 88, y + 70, l1, { size: 18, fill: C.mute }) + text(ix + 14, y + 98, l2, { size: 17, fill: C.mute });
  s += infra(790, 'postgresql', 'PostgreSQL 16', 'données EF Core', ':5433 (hôte) → :5432', C.cyan);
  s += infra(920, 'redis', 'Redis 7', 'cache · backplane', ':6379 · présence', C.red);
  s += infra(1050, 'rabbitmq', 'RabbitMQ 4.1', 'événements', ':5672 · UI :15672', C.orange);
  s += infra(1180, null, 'Seq', 'logs Serilog', ':8199 · Adminer :8082', C.violet);
  s += arrow(1176, 844, 1288, 844, { color: C.cyan, id: 'aCyan' });
  s += arrow(1176, 974, 1288, 974, { color: C.red, id: 'aRed' });
  s += arrow(1176, 1104, 1288, 1104, { color: C.orange, id: 'aOrange' });
  s += arrow(1176, 1234, 1288, 1234, { color: C.violet, id: 'aViolet' });

  // Panneau « stack »
  s += text(ix, 170, 'STACK ET VERSIONS', { size: 20, weight: 700, fill: C.cyan });
  s += rect(ix, 190, 510, 540, { fill: C.card, stroke: C.line });
  const stack = [
    ['Web', 'ASP.NET Core 8 · Swashbuckle 6.4'],
    ['Sécurité', 'JwtBearer 8 · Rate limiting · CORS'],
    ['CQRS', 'MediatR 12.2 · AutoMapper 16.2'],
    ['Données', 'EF Core 9 · Npgsql 9 · PostgreSQL 16'],
    ['Messagerie', 'MassTransit 8.1.3 · RabbitMQ 4.1'],
    ['Temps réel', 'SignalR 8 · backplane StackExchange.Redis'],
    ['Cache', 'IDistributedCache (Redis ou mémoire)'],
    ['Logs', 'Serilog 3.1 → Seq 6'],
    ['Jobs', 'Hangfire 1.8.6 (stockage PostgreSQL)'],
    ['Tests', 'xUnit 2.9 · NSubstitute 5.3 · coverlet'],
    ['CI/CD', 'GitHub Actions · Docker · Dependabot']
  ];
  stack.forEach(([k, v], i) => { s += text(ix + 20, 232 + i * 44, k, { size: 19, weight: 800, fill: C.blue }) + text(ix + 150, 232 + i * 44, v, { size: 18, fill: '#dbe6ff' }); });

  // Services
  const sx = 1590, sw = 210;
  s += text(sx, 770, 'SERVICES', { size: 20, weight: 700, fill: C.cyan });
  s += rect(sx, 1020, sw, 168, { fill: C.card, stroke: C.orange, sw: 2 }) + text(sx + 16, 1056, 'Notification', { size: 23, weight: 800 }) +
    lines(sx + 16, 1086, ['consomme users.service', 'retry ×5 · file _error', ':8083 (Docker)'], { size: 18, gap: 24 });
  s += arrow(1552, 1104, 1588, 1104, { color: C.orange, id: 'aOrange' });
  s += rect(sx, 790, sw, 126, { fill: C.card, stroke: C.violet, sw: 2 }) + text(sx + 16, 826, 'Newsletter', { size: 23, weight: 800 }) +
    lines(sx + 16, 856, ['Serveur Hangfire', 'jobs → PostgreSQL', ':8085 (Docker)'], { size: 18, gap: 24 });
  s += path('M1590,850 L1560,850 L1560,874 L1552,874', { color: C.violet, id: 'aViolet', width: 2 });

  // Flèches clients -> pipeline
  s += path('M370,265 C404,265 408,262 438,262', { color: C.blue, id: 'aBlue' });
  s += text(378, 252, 'REST', { size: 16, fill: C.blue, weight: 700 });
  s += path('M370,430 C410,430 400,296 438,296', { color: C.blue, id: 'aBlue' });
  s += text(378, 422, 'REST', { size: 16, fill: C.blue, weight: 700 });
  s += path('M370,480 C420,480 396,324 438,324', { color: C.green, id: 'aGreen', dash: '8 6' });
  s += text(378, 502, 'WS', { size: 16, fill: C.green, weight: 700 });

  s += text(40, 1300, 'Logos : Simple Icons (CC0) — marques déposées de leurs propriétaires, utilisées pour désigner les technologies.', { size: 16, fill: '#6b7ea3' });
  writeFileSync(join(outDir, 'architecture-detaillee.svg'), svgDocument(W, H, defs() + s));
}
console.log('ok');
