# Design

## Context

Decisions already made are in `docs/ARCHITECTURE.md` (services, one schema per service, queue in memory and reloaded on start, delivered only after offset confirm). This file says how milestone 2 builds them, and records the choices made on the way. The owner delegated the spec review for this milestone and said to use the section 18 starting values as defaults.

## Goals / Non-Goals

**Goals:**
- A bot can read user messages by long polling and answer them, with every queue rule tested.
- No message is lost on restart or deploy.

**Non-Goals:**
- No HTTP door for users yet. `Chats.ReceiveFromUser` is the entry the Client API will call in milestone 3; tests call it directly.
- No bot creation screen. `Bots.Create` is the entry the builder area will call in milestone 8.
- No Relay contract yet. The Telegram adapter (milestone 5) is its first reader; the types move to `Dalan.Relay.Contracts` then.
- No outbox, no audit log, no per-bot or per-user override of limits (milestone 8).
- Only the "active" membership state is written. Stop and block arrive with the screens that cause them.

## Decisions

1. **Versions (checked 2026-11-16):** `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 (version 11 is still a release candidate), EF Core 10.0.15, `EFCore.NamingConventions` 10.0.1 (snake_case names), Testcontainers.PostgreSql 4.17.0, `Microsoft.Extensions.TimeProvider.Testing` 11.0.0, PostgreSQL 18 (19 is a release candidate).
2. **Migrations:** EF Core migrations, one set per service, history table inside the service's own schema. The server applies them at start, so the Iran server needs no tool. `Microsoft.EntityFrameworkCore.Design` is referenced only in Debug builds, so the release stays small.
3. **The queue is a sorted list, not a `Channel`.** `docs/DESIGN.md` named `System.Threading.Channels`, but the rules decided after it need to keep an update until the bot confirms it, remove one from the middle (expiry, latest command wins) and hand the same update out again. A `Channel` can do none of these. Each bot gets a small class with a lock, a `SortedDictionary` by update id, and a signal that wakes the waiting poll. It sits behind `IUpdateQueue`, the agreed seam for a later shared queue.
4. **Update id = id of the stored message row.** It is durable and always grows, so a bot's saved offset stays valid after a restart. Ids are shared by all bots, so one bot sees gaps; Telegram libraries only use "last id + 1".
5. **Never confirm what was never handed out.** Two messages can be saved almost together and reach the queue in the other order. So an offset confirms only updates that were already returned to the bot; an older update that was not returned yet is still returned. Nothing can be dropped unseen.
6. **Expiry and confirm.** An update is offered to the bot only until its expiry. Once it was handed out, a confirm is accepted even after the expiry (a slow bot that works for two minutes still gets "delivered"). A handed-out update that is not confirmed is offered again only while it is not expired. A sweep (every `Relay:ExpirySweepSeconds`) marks unread expired updates "not delivered" while the bot is away. A handed-out update of a bot that never calls again stays "pending" until that bot's next call or the next restart; milestone 7 (bot status on screen) can tighten this if it needs to.
7. **Latest command wins** replaces only updates that were not handed out yet, and is one global setting now (`Relay:LatestCommandWins`, on). The per-bot switch comes with the builder area. A replaced message is marked "not delivered"; there is no separate "replaced" state because no screen needs it.
8. **Delivery state** is stored only for user messages (pending, delivered, not delivered). For bot messages it is null: delivery to the browser is by sync and has no state yet.
9. **Per-chat sequence number:** `memberships.last_seq` is raised in the same transaction that inserts the message. The row lock makes numbers in one chat commit in order.
10. **Token:** `<bot id>:<35 characters>`, 26 random bytes as base64url, the same shape Telegram uses. Stored as SHA-256. A slow hash is for weak human passwords; this secret has 208 random bits and is checked on every request, so a fast hash is right. Compared in constant time.
11. **Bot API shape:** `GET /v1/updates?offset=&limit=&timeout=` and `POST /v1/messages` with `{"user_id":1,"text":"hi"}`. JSON in snake_case. Errors are `{"error":"<code>","message":"<what went wrong and how to fix it>"}`.
12. **User name in an update:** Relay asks Identity through `IUserDirectory` (a sync contract call) when the message arrives. The profile type has no phone field, so a bot cannot get one by mistake.
13. **Rate limits** use the built-in `System.Threading.RateLimiting` inside one endpoint filter: per IP first (before the token is checked), then per bot (after it, so nobody can use up another bot's limit with a wrong token). Both are "N calls per window" with one window length setting. A changed limit applies to a bot or address after its current limiter goes idle. The host reads the real client IP from `X-Forwarded-For`, trusted only from the local proxy.
14. **Start-up:** a background worker in the host calls each service's `StartAsync` (apply migrations; Relay also reloads the queues) and retries every `Dalan:StartupRetrySeconds` while the database is down. `/health` works from the first moment; `/ready` and all other requests answer 503 until the worker is done. `/ready` does not call the database on each request: under systemd nothing reads it after the deploy.
15. **Stopping:** a waiting poll returns an empty list when the server starts to stop, so a deploy does not wait for a 50-second poll.
16. **Settings** are read through `IOptionsMonitor<RelayOptions>` (section `Relay`), so a changed setting file is picked up without a restart. The admin panel (milestone 8) will feed the same options.
17. **Tests** use real PostgreSQL: Testcontainers by default, or the server in `DALAN_TEST_POSTGRES` when Docker is not there. Each test gets a new database. Time is a `FakeTimeProvider`.

## Starting values used

| Setting | Value | Source |
|---|---|---|
| `Relay:UpdateExpirySeconds` | 60 | Owner |
| `Relay:PollTimeoutDefaultSeconds` / `PollTimeoutMaxSeconds` | 30 / 50 | Section 18, assumed |
| `Relay:MaxTextLength` | 4096 | Section 18, assumed |
| `Relay:LatestCommandWins` | true | Section 10 |
| `Relay:MaxUpdatesPerPoll` | 100 | New, Telegram-like |
| `Relay:ExpirySweepSeconds` | 5 | New |
| `Relay:RateLimitWindowSeconds` | 1 | New |
| `Relay:BotRequestLimit` (per window) | 30 | New, Telegram-like |
| `Relay:IpRequestLimit` (per window) | 100 | New |
| `Dalan:StartupRetrySeconds` | 5 | New |

## Risks / Trade-offs

- [At least once] A bot can get the same update twice (after a crash of the bot or a restart of the server) → documented; the bot must cope, as with Telegram.
- [Update ids are 64-bit and shared] Some Telegram libraries keep the update id in 32 bits. At real load this lasts for years → look again in milestone 5.
- [Cannot build .NET 11 on the dev machine] → checked locally on .NET 10 in a copy; CI is the source of truth.

## Migration Plan

First release with a database: create the database and user on the server, put `ConnectionStrings__Dalan` in `/etc/dalan/dalan.env`. The server creates the schemas itself. Rollback: the old release ignores the new schemas.
