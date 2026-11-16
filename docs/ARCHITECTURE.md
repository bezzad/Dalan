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

## 5. Data

- One PostgreSQL database, one schema per service, no foreign key or join across schemas. Each schema can later move to its own database.
- EF Core 10 with the official PostgreSQL provider `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x (version 11 of the provider was only a release candidate on 2026-11-16; upgrade when stable). Message content in `jsonb`.

## 6. Bot queue and delivery

- One in-memory queue per bot (`System.Threading.Channels`), with expiry. Rules in `openspec/specs/relay` (milestone 2).
- Every user message is saved in the database first. On start, the server reloads undelivered, unexpired messages into the queues, so a deploy or restart loses nothing.
- A message counts as **delivered** only after the bot confirms it with the next `getUpdates` offset, like Telegram. Delivery is at least once.

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
