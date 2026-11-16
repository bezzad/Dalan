# Dalan — Architecture

Short and current. `docs/DESIGN.md` says what we build and why. This file says how the code is shaped. Rules live in `openspec/specs/`.

## 1. Big picture

- One PostgreSQL database, one process in version 1.
- The code is split into **services**. Each service has its own projects, its own database schema and its own `DbContext`. Services talk only through contracts and events.
- Later, any service can run as its own process by giving it its own host. The code of the service does not change.

```
            Caddy (TLS, routing by domain)
                     │
           ┌─────────┴──────────┐
           │     Dalan.Host     │  one process, wires everything
           └─────────┬──────────┘
   ┌──────────┬──────┴─────┬──────────────┐
Identity    Relay      Notification     Admin
(identity)  (relay)    (notification)   (admin)      ← PostgreSQL schema
              ▲
        Telegram adapter (plugin, no data)
```

## 2. Services

| Service | Owns | Talks by |
|---|---|---|
| **Identity** | users, phone numbers, password hashes, TOTP, refresh tokens, builder approval | **sync** calls (a token is needed now) |
| **Relay** (core) | bots, memberships, messages, the in-memory queue of each bot | sync in; **async** out to bots (queue + long polling) |
| **Notification** | push subscriptions, send log | **async** only. It gets "send this to this receiver by this channel" and knows no business. Channels are strategies: Web Push, SMS, later Email |
| **Admin** | settings and limits, audit log | sync. Other services read settings from it |

Relay has two thin HTTP doors, both inside Relay:

- **Client API** (`dalanet.ir`): the door for **people** (PWA). Send a message or button press (POST), live messages (SSE), history sync, file upload. It never runs bot logic; the bot at home does.
- **Bot API** (`api.dalanet.ir`): the door for **bots**. Token auth, `getUpdates` long polling, `sendMessage` and the rest.

**Telegram adapter** is a plugin, not a service: no data, no domain. The host mounts it on the Bot API at `/tg/bot<token>/<method>`. It turns Telegram-shaped calls into Relay contract calls and back.

## 3. Projects

```
src/
  Dalan.Host/                         the one process: Program.cs, settings, /health, /ready
  Identity/Dalan.Identity(.Contracts)
  Relay/Dalan.Relay(.Contracts)
  Notification/Dalan.Notification(.Contracts)
  Admin/Dalan.Admin(.Contracts)
  Plugins/Dalan.Telegram
tests/                                one test project per area
client/                               PWA, plain TypeScript
deploy/                               deploy.sh, systemd unit, Caddyfile
```

Inside a service, code is grouped by **feature folder** (endpoint + logic + data access side by side). No Repository, Mediator or extra layers.

Dependency rules (enforced by architecture tests, see `openspec/specs/architecture`):

- A service references only other services' `.Contracts`.
- `.Contracts` hold only interfaces and data types.
- Relay never mentions Telegram.
- Only the host references implementations and plugins.

## 4. Communication

- **Sync**: call the other service's contract interface. In one process this is a normal method call.
- **Async**: events through an **outbox table** in PostgreSQL. The event is written in the same transaction as the data, so they never disagree. A worker reads with `SELECT … FOR UPDATE SKIP LOCKED`. Survives restarts and works the same when services become separate processes.
- Events so far: `MessageCreated` (→ SSE, Notification), `SmsRequested` (Identity → Notification), `UserSuspended` (Identity → all).
- The outbox table is not built yet. It comes with its first reader (milestone 3).
- Relay learns a user's name from Identity through `IUserDirectory`. That profile has no phone number.

## 5. Data

- One PostgreSQL database, one schema per service, no foreign key or join across schemas. Each schema can later move to its own database.
- EF Core 10 with the official PostgreSQL provider `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x (version 11 of the provider was only a release candidate on 2026-11-16; upgrade when stable). Message content in `jsonb`. Table and column names are snake_case.
- Each service has its own EF Core migrations and its own history table in its schema. The server applies them at start (`/ready` is 503 until then), so the Iran server needs no tool.
- New migration: `dotnet ef migrations add <Name> --project src/Relay/Dalan.Relay --startup-project src/Dalan.Host --context RelayDb --output-dir Data/Migrations` (the same for Identity with `IdentityDb`).

## 6. Bot queue and delivery

- One in-memory queue per bot behind `IUpdateQueue`: a small sorted list with a lock (not a `Channel`, because an update must stay until it is confirmed and can be removed from the middle). Rules in `openspec/specs/bot-queue`.
- Every user message is saved in the database first. Its row id is the update id, so a bot's offset stays valid after a restart. On start, the server reloads undelivered, unexpired messages into the queues, so a deploy or restart loses nothing.
- A message counts as **delivered** only after the bot confirms it with the next `getUpdates` offset, like Telegram. Delivery is at least once. An offset confirms only updates the bot was really given.
- An update is offered to the bot until its expiry. After that the message is "not delivered"; a late confirm of an update the bot already got still counts.
- Core Bot API: `GET /v1/updates` and `POST /v1/messages`, token in `Authorization: Bearer`. Rate limits per IP and per bot.

## 7. Identity and sessions

- JWT access token + refresh token. No server session state.
- The access token is kept in an `HttpOnly`, `Secure`, `SameSite` cookie (only a storage place; JavaScript cannot read it). Signed with ES256, so services hold only the public key.
- Refresh token: random, stored hashed, replaced on every use. Reuse of an old one revokes that device's tokens.
- **Blocking is by user id.** Refresh checks the user's status. `UserSuspended(userId)` puts the id in a small in-memory deny list in every service until the last token expires; SSE for that user closes at once.
- Access token lifetime is an admin setting.

## 8. Scale

- Design and load-test target for version 1: **100 bots, 100,000 users, about 10,000 online at once, about 500 messages per second**, on one 2-core server.
- First-year real load is far lower (about 10 users, 1,000 to 10,000 requests a day).
- To grow past one process: give a service its own host, and swap the in-memory bot queue and the SSE notifier for shared ones (PostgreSQL `LISTEN/NOTIFY`, or Redis if ever needed). The outbox already works across processes.
- Container-ready from day one: settings from environment, logs on stdout, `/health` and `/ready`, graceful shutdown, a `Dockerfile`. Production in Iran still runs one binary under systemd (Docker Hub refuses Iranian IP addresses).

## 9. Decisions log

| # | Date | Decision |
|---|---|---|
| 1 | 2026-11-16 | Scale target: 100 bots, 100k users, ~10k online, ~500 msg/s |
| 2 | 2026-11-16 | EF Core + official PostgreSQL provider |
| 3 | 2026-11-16 | Queue in memory, reloaded from the database on start |
| 4 | 2026-11-16 | Separate service code, one process in version 1 |
| 5 | 2026-11-16 | Delivered to bot only after offset confirm (at least once) |
| 6 | 2026-11-16 | JWT + refresh token; blocking by user id |
| 7 | 2026-11-16 | Async events through a PostgreSQL outbox |
| 8 | 2026-11-16 | Every spec rule has an ID; test first; CI checks coverage |
| 9 | 2026-11-16 | Specs with OpenSpec 2.0 |
| 10 | 2026-11-16 | Bot queue is a sorted list behind `IUpdateQueue`, not a `Channel` |
| 11 | 2026-11-16 | Update id = id of the stored message row; an offset never confirms an update the bot did not get |
| 12 | 2026-11-16 | Bot token secret is stored as SHA-256 (long random secret, checked on every call) |
| 13 | 2026-11-16 | Migrations run at start in the background; `/ready` and all traffic wait for them |
| 14 | 2026-11-16 | Section 18 starting values are used as defaults without asking; the admin panel changes them later |
