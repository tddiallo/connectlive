# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository scope

This repo is **self-contained for the backend**: `ConnectLive.Core.CI.slnf` (a solution filter) builds and tests the three
APIs, the shared libraries and both test projects with nothing else checked out — that is what CI does
(`docs/CI.md`). The three APIs no longer reference sibling folders.

`ConnectLive.Core.sln` still lists projects from **sibling folders that are not versioned** (`..\Workers`,
`..\Connectlive.Proxy`, `..\Connectlive.WebHosting`, `..\ConnectLive.SPA`, `..\ConnectLive.SPA.Infrastructure`,
`..\WebHostExtensions`) for Visual Studio convenience. Building the full `.sln` without them fails on missing project
files — an environment issue, not a bug here. Prefer the `.slnf` for backend work.

Each Dockerfile expects this repo in a `ConnectLive.Core/` sub-folder of the build context (compose uses `context: ..`).

## Commands

- Build / test (what CI runs): `dotnet build ConnectLive.Core.CI.slnf` / `dotnet test ConnectLive.Core.CI.slnf`.
- Local infrastructure (Postgres 5433, Redis 6379, RabbitMQ 5672, Seq, Adminer):
  `docker compose -f docker-compose.yml -f docker-compose.override.yml up -d connectlive-db redis rabbitmq`.
  The `connectlive-db` / `redis` / `adminer` container names are fixed: if they already exist (another Compose
  project), reuse them rather than recreating. Full stack in Docker: same command with `up --build`.
- Run one service: `dotnet run --project ConnectLive.Core\ConnectLive.Core.Api.csproj` (also `ConnectLive.Notification.Api`,
  `ConnectLive.Newsletter.Api`). In Development the Core API **applies EF migrations at startup**.
- EF Core migrations (`DbContext` in `ConntectLive.DAL`, startup project `ConnectLive.Core.Api`):
  `dotnet ef migrations add <Name> --project ConntectLive.DAL --startup-project ConnectLive.Core\ConnectLive.Core.Api.csproj`.
  The database is **Postgres only** (`docs/POSTGRES-DOCKER.md`); migrations are Postgres-specific.
- Tests are xUnit + NSubstitute. Most run with no infrastructure (in-memory SQLite). Tests that need real services are
  **skipped unless an env var is set**: `CONNECTLIVE_TEST_POSTGRES` (migrations), `CONNECTLIVE_TEST_REDIS` (presence +
  two-instance SignalR backplane), `CONNECTLIVE_TEST_RABBITMQ` (+ `_USER`/`_PASSWORD`). CI sets all of them
  (`docs/CI.md`). Coverage settings: `coverlet.runsettings`.
- The API refuses to start without `Jwt:SigningKey` (≥ 32 chars) outside Development, where
  `appsettings.Development.json` supplies a public dev-only key. See `docs/AUTHENTIFICATION.md`.

## Architecture

### Three thin API hosts over a shared kernel

Three separate ASP.NET Core 8 Web APIs, each a small host wiring shared libraries:

- **ConnectLive.Core** (`ConnectLive.Core.Api`) — the main API: users, questions, sessions, answers, leaderboard,
  SignalR hub; owns the MediatR/CQRS pipeline and publishes events to the bus.
- **ConnectLive.Notification.Api** — consumes `UserCreated` from RabbitMQ (`UserCreatedConsumer`) and runs a Hangfire
  server pinned to the `"email"` queue.
- **ConnectLive.Newsletter.Api** — runs a Hangfire server pinned to the `"newsletter"` queue.

Shared libraries in this repo:

- **ConnectLive.Domain** — cross-service message contracts (`Contracts/`, e.g. `UserCreatedContractQuery`) and domain
  exceptions. No infrastructure dependencies; everything else depends on it.
- **ConntectLive.DAL** (name is intentionally spelled "Conntect" — keep it, don't "fix" the typo) — EF Core + Npgsql:
  `ApplicationDbContext`, `[Table]`/`[Column]` entities, `Migrations/`, generic repository + unit of work.
- **ConnectLive.Application** — cross-cutting building blocks: MediatR pipeline behaviors, `IntegrationEvent`,
  exception middleware, **messaging configuration** (`Messaging/MessagingExtensions`) and bus consumers (`BusEvents/`).
- **ConnectLive.Portal.Shared** — request/response DTOs (the wire contracts), including realtime DTOs and
  `SessionHubEvents`.

### CQRS via MediatR, grouped by feature

Controllers are thin: they translate an HTTP call into `_mediator.Send(...)`. Requests and their handlers are grouped
per feature into one static container class under `Commands/` or `Queries/` (e.g. `SessionCommands`,
`QuestionQueries`), each command/query pair kept together in a `#region` in the same file. Follow this grouping.

Handlers resolve dependencies from a single `IServiceProvider` passed to the constructor (`GetRequiredService<T>()`)
rather than constructor-injected parameters — the established pattern; match it. Optional services (e.g.
`IPublishEndpoint`, only present when a broker is configured) use `GetService<T>()`.

### Cross-cutting pipeline behaviors (`ConnectLive.Application`)

Two open-generic `IPipelineBehavior<,>` are registered for every MediatR request:

- `WatchBehavior<,>` — logs start/end and elapsed ms; correlates by `IntegrationEvent.EventId`.
- `CacheBehavior<,>` — opt-in response caching on `IDistributedCache` (Redis when `ConnectionStrings:Redis` is set,
  otherwise process memory) with sliding expiration, for requests implementing `ICachable<TResponse>`.

### Data access: generic repository + unit of work

`IGenericRepository<TEntity>` (async `GetByIdAsync`, `Query()` read-only LINQ, `AddAsync`, `Update`, `Remove`) is
registered as an open generic; `IUnitOfWork` exposes one repository per aggregate plus `CommitAsync(ct)` (one
`SaveChangesAsync`). Adding an entity means: a class in `ConntectLive.DAL/Entity` implementing `IEntity`, a property on
`IUnitOfWork`/`UnitOfWork`, a `DbSet<>` on `ApplicationDbContext`, and a migration.

### Messaging (`docs/MASSTRANSIT-FLUX.md`)

MassTransit 8 + RabbitMQ, configured **once** in `MessagingExtensions.AddConnectLiveMessaging(config, consume)`; it does
nothing unless `EventBusConnection` is set. Core publishes `UserCreated` after registering a user (best effort — the
event is lost if the broker is down; an outbox is the documented next step). Notification consumes it from queue
`users.service` with exponential retry, failures go to `users.service_error`. Do not reintroduce
`MassTransit.AspNetCore` / `Extensions.DependencyInjection` 7.x packages.

### Realtime (`docs/SIGNALR-REALTIME.md`, `docs/SIGNALR-BACKPLANE-REDIS.md`)

`SessionHub` handles presence only; business events are pushed through `ISessionNotifier`. Presence is behind
`ISessionPresenceTracker` (in-memory or `RedisSessionPresenceTracker`). With `ConnectionStrings:Redis`, SignalR uses a
Redis backplane (`AddConnectLiveRealtime`) so several API instances share messages and presence. Multi-instance
deployments still need sticky sessions or WebSockets-only.

### Security model (`docs/AUTHENTIFICATION.md`)

- JWT Bearer auth (`ConnectLive.Core/Auth`). Controllers are `[Authorize]` except `POST /User/save-profile`, which issues
  the token once (`signature.secret`) and is rate-limited. The authenticated user id comes from `User.GetUserId()` /
  `Context.UserIdentifier` — never from the request body or hub arguments.
- Domain errors map to HTTP in `ExceptionHandlerMiddleware` (wired first in the pipeline): `ArgumentException` 400,
  `UnauthorizedAccessException` 401, `ForbiddenException` 403, `KeyNotFoundException` 404, `ConflictException` 409.
- Session `Start`/`Next`/`End` are host-only (`SessionTransition` checks `HostUserId`); commands carry the requester id.
- CORS comes from `Security:AllowedOrigins` (`CorsExtensions`); with no config it is open only in Development.

### Gameplay flow (`docs/QUESTIONS-REPONSES.md`)

Questions → session → player answers (`SubmitAnswerCommand`, server-side correction, one answer per player and question
enforced by a unique index) → `LeaderboardUpdated` push. The correct answer never leaves the server except in the
author's own `GET /Question/all`.

### Host bootstrap is duplicated per service, not shared

`ConnectLive.Core.Api` and `ConnectLive.Notification.Api` each define a near-identical `CreateWebHostBuilder<T>()`
(`HostExtensions` / `MyCustomWebHosting`) wiring config sources and Serilog→Seq logging, tagged with the assembly name of a
marker type (`ClassInfo`). `ConnectLive.Newsletter.Api` still uses plain `WebApplication.CreateBuilder`. When changing
host bootstrap behavior, check all three.

### Known gaps — do not treat these as intended conventions to copy

Full status in `docs/AUDIT-360.md`. Notable open items:
- `SendExceptionToAdmin` only logs at Critical level (no real alerting), and only for 5xx.
- `Result<T>` is used by Session/Question controllers but `UserController` and `LeaderBoardController` still return raw
  `Ok(...)`. Errors are RFC 9457 problem details (`application/problem+json`: type/title/status/detail/traceId) with the legacy `error` field kept for old clients.
- `LeaderBoardController` is a stub (the real leaderboard is `GET /Session/{id}/leaderboard`).
- No token re-issue/revocation (a lost JWT cannot be recovered); no transactional outbox for bus events.
- Presence entries of a crashed API instance linger in Redis until their 12 h expiry.
