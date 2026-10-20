// Génère docs/img/linkedin-architecture.svg (1080x1350, format portrait 4:5 recommandé pour LinkedIn).
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { rect, text, logoTile, arrow, markers, svgDocument } from './lib.mjs';

const W = 1080, H = 1350;
const C = { card: '#15203a', line: '#2c3d63', mute: '#9fb0cf', blue: '#60a5fa', green: '#4ade80', orange: '#fb923c', red: '#f87171', violet: '#a78bfa', cyan: '#22d3ee' };

let s = '';
s += `<defs><linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#0a1020"/><stop offset="1" stop-color="#101b33"/></linearGradient></defs>`;
s += markers({ arr: '#9fb0cf', arrGreen: C.green, arrBlue: C.blue, arrOrange: C.orange, arrRed: C.red });
s += `<rect width="${W}" height="${H}" fill="url(#bg)"/>`;

// ── En-tête
s += text(40, 82, 'ConnectLive', { size: 66, weight: 800 });
s += text(40, 128, 'Quiz live temps réel · API .NET 8', { size: 30, fill: C.blue, weight: 600 });
s += text(40, 166, 'CQRS · Event-driven · SignalR · CI verte', { size: 24, fill: C.mute });

const label = (x, y, t, color) => text(x, y, t, { size: 20, weight: 700, fill: color });

// ── Clients
s += label(40, 214, 'CLIENTS', C.cyan);
const client = (x, icon, title, sub) =>
  rect(x, 228, 490, 112, { fill: C.card, stroke: C.line }) + logoTile(x + 20, 248, 72, icon, { r: 14 }) +
  text(x + 112, 286, title, { size: 28, weight: 700 }) + text(x + 112, 318, sub, { size: 21, fill: C.mute });
s += client(40, 'blazor', 'Blazor WebAssembly', 'SPA · MudBlazor');
s += client(550, 'swagger', 'Swagger UI', 'OpenAPI · test des routes');

s += arrow(110, 342, 110, 398, { color: C.blue, id: 'arrBlue' });
s += text(126, 376, 'REST · JWT', { size: 21, fill: C.blue, weight: 600 });
s += arrow(330, 342, 330, 398, { color: C.green, id: 'arrGreen' });
s += text(346, 376, 'WS · SignalR', { size: 21, fill: C.green, weight: 600 });
s += arrow(620, 342, 620, 398, { color: C.blue, id: 'arrBlue' });
s += text(604, 376, 'REST · JWT', { size: 21, fill: C.blue, weight: 600, anchor: 'end' });

// ── API
s += label(40, 394, 'API', C.cyan);
s += rect(40, 404, 640, 326, { fill: C.card, stroke: C.blue, sw: 3 });
s += logoTile(60, 424, 74, 'dotnet', { r: 14 });
s += text(152, 460, 'Core.Api', { size: 32, weight: 800 });
s += text(152, 492, 'ASP.NET Core 8 · architecture en couches', { size: 21, fill: C.mute });

const pill = (x, y, w, t, color) => rect(x, y, w, 40, { fill: 'none', stroke: color, sw: 2, r: 20 }) + text(x + w / 2, y + 27, t, { size: 20, fill: color, weight: 600, anchor: 'middle' });
s += pill(60, 516, 100, 'JWT', C.orange) + pill(172, 516, 150, 'Rate limit', C.orange) + pill(334, 516, 100, 'CORS', C.orange) + pill(446, 516, 214, 'ProblemDetails', C.orange);

const step = (x, w, t, color) => rect(x, 580, w, 62, { fill: '#0f1830', stroke: color, sw: 2, r: 12 }) + text(x + w / 2, 619, t, { size: 21, weight: 700, anchor: 'middle' });
s += step(60, 126, 'Controllers', C.blue) + arrow(188, 611, 208, 611, { id: 'arr', width: 2 });
s += step(210, 168, 'MediatR CQRS', C.violet) + arrow(380, 611, 400, 611, { id: 'arr', width: 2 });
s += step(402, 122, 'Domaine', C.green) + arrow(526, 611, 546, 611, { id: 'arr', width: 2 });
s += step(548, 112, 'EF Core', C.cyan);

s += pill(60, 666, 280, 'Behaviors : log · cache', C.violet) + pill(352, 666, 190, 'Hub SignalR', C.green) + pill(554, 666, 106, 'DI', C.mute);

// ── Services
s += label(700, 394, 'SERVICES', C.cyan);
s += rect(700, 404, 340, 150, { fill: C.card, stroke: C.line });
s += text(720, 446, 'Notification.Api', { size: 26, weight: 800 });
s += text(720, 478, 'Consumer MassTransit', { size: 21, fill: C.mute });
s += text(720, 508, 'retry + file d’erreurs', { size: 21, fill: C.mute });
s += rect(700, 570, 340, 160, { fill: C.card, stroke: C.line });
s += text(720, 612, 'Newsletter.Api', { size: 26, weight: 800 });
s += text(720, 644, 'Hangfire · jobs planifiés', { size: 21, fill: C.mute });
s += text(720, 674, 'stockage PostgreSQL', { size: 21, fill: C.mute });

// ── Infrastructure
s += label(40, 800, 'INFRASTRUCTURE', C.cyan);
const infra = (x, icon, title, l1, l2, color) =>
  rect(x, 810, 235, 206, { fill: C.card, stroke: color, sw: 3 }) +
  (icon ? logoTile(x + 18, 828, 76, icon, { r: 14 }) : rect(x + 18, 828, 76, 76, { fill: '#fff', r: 14 }) + text(x + 56, 880, 'Seq', { size: 28, fill: '#0f172a', weight: 800, anchor: 'middle' })) +
  text(x + 104, 876, title, { size: 22, weight: 800 }) +
  text(x + 18, 940, l1, { size: 20, fill: C.mute }) + text(x + 18, 970, l2, { size: 20, fill: C.mute });
s += infra(40, 'postgresql', 'PostgreSQL', 'Données · migrations', 'tests sur base réelle', C.cyan);
s += infra(295, 'redis', 'Redis', 'Cache · backplane', 'SignalR · présence', C.red);
s += infra(550, 'rabbitmq', 'RabbitMQ', 'Événements', 'UserCreated', C.orange);
s += infra(805, null, 'Logs', 'Serilog → Seq', 'logs structurés', C.violet);

s += arrow(250, 732, 250, 806, { color: C.cyan, id: 'arrBlue' }) + text(266, 774, 'SQL', { size: 20, fill: C.cyan, weight: 700 });
s += arrow(385, 732, 385, 806, { color: C.red, id: 'arrRed' }) + text(401, 774, 'cache + pub/sub', { size: 20, fill: C.red, weight: 700 });
s += arrow(610, 732, 610, 806, { color: C.orange, id: 'arrOrange' }) + text(626, 774, 'publie', { size: 20, fill: C.orange, weight: 700 });
s += arrow(860, 806, 860, 732, { color: C.orange, id: 'arrOrange' }) + text(876, 774, 'consomme', { size: 20, fill: C.orange, weight: 700 });

// ── Qualité & livraison
s += label(40, 1056, 'QUALITÉ & LIVRAISON', C.cyan);
const q = (x, icon, title, sub) =>
  rect(x, 1068, 235, 128, { fill: C.card, stroke: C.line }) +
  (icon ? logoTile(x + 14, 1086, 56, icon, { r: 12 }) : rect(x + 14, 1086, 56, 56, { fill: '#fff', r: 12 }) + text(x + 42, 1124, 'xU', { size: 24, fill: '#0f172a', weight: 800, anchor: 'middle' })) +
  text(x + 82, 1122, title, { size: 22, weight: 800 }) + text(x + 14, 1168, sub, { size: 20, fill: C.mute });
s += q(40, 'githubactions', 'CI GitHub', 'build · tests · images');
s += q(295, 'docker', 'Docker', '3 images · Compose');
s += q(550, 'dependabot', 'Dependabot', '0 vulnérabilité');
s += q(805, null, 'xUnit', '148 tests · 90 % couv.');

// ── Pied
s += rect(40, 1226, 1000, 84, { fill: '#0f1830', stroke: C.line, r: 14 });
s += text(60, 1264, 'Tidiane Diallo · Développeur .NET', { size: 26, weight: 800 });
s += text(60, 1296, 'Architecture backend · projet personnel', { size: 22, fill: C.blue, weight: 600 });
s += text(1020, 1284, '.NET 8 · PostgreSQL · Redis · RabbitMQ', { size: 22, fill: C.mute, anchor: 'end', weight: 600 });

const out = join(dirname(fileURLToPath(import.meta.url)), '..', 'linkedin-architecture.svg');
writeFileSync(out, svgDocument(W, H, s));
console.log('écrit', out);
