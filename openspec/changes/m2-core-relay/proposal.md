# Proposal

## Why

Milestone 1 gave a server that starts but does nothing. Milestone 2 (docs/DESIGN.md section 19) builds the heart of Dalan: a bot at home can read messages by long polling and answer them, and nothing is lost on a restart.

## What Changes

- PostgreSQL arrives. Each service owns one schema and applies its own migrations at start: `identity` (users) and `relay` (bots, memberships, messages).
- Bot tokens in the Telegram-compatible form `<bot id>:<secret>`, shown once, stored only as a hash.
- One in-memory queue per bot with expiry, "latest command wins", confirm by offset (at least once), and reload from the database on start.
- Core Bot API with Dalan's own names: `GET /v1/updates` (long polling) and `POST /v1/messages`, token in the `Authorization` header.
- Rate limits on the Bot API, per IP and per bot.
- `/ready` answers 200 only after the schema is applied and the queues are reloaded; other requests get 503 until then.
- `compose.yaml` with PostgreSQL for local work.

Out of scope: the Client API and PWA (milestone 3; until then only tests create users and user messages), login (4), the Telegram adapter (5), buttons, edit, pin and files (6), bot status and "not delivered" on screen (7), bot creation screens, token rotation and settings in the admin panel (8), the outbox (it comes with its first reader, the SSE notifier in milestone 3).

## Capabilities

### New Capabilities

- `bot-queue`: how updates wait for a bot, expire, are confirmed and survive a restart.
- `relay`: bots, tokens, chats, messages and the core Bot API.
- `security`: rate limits on public endpoints.
- `identity`: the user record and what other services may learn about a user.

### Modified Capabilities

- `operations`: readiness now waits for the database; schema is applied at start.

## Impact

- New runtime dependencies: EF Core 10 with the official PostgreSQL provider (as decided in docs/ARCHITECTURE.md section 5) and `EFCore.NamingConventions`.
- New test dependencies: Testcontainers for PostgreSQL, `Microsoft.Extensions.TimeProvider.Testing`.
- The server now needs `ConnectionStrings__Dalan` in `/etc/dalan/dalan.env`.
- `docs/DESIGN.md` and `docs/ARCHITECTURE.md`: the queue is no longer a `Channel` (see design.md decision 3).
