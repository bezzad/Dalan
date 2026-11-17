# Dalan

Dalan (دالان) — run your bot or web app from a home computer, no public IP and no server. A relay platform hosted in Iran with a Telegram-compatible Bot API, phone-verified users, and site hosting.

**Status:** milestone 3 (client) — a person can open the PWA, log in with the development login, start a bot, tap reply buttons and see answers live. A bot reads by long polling (`GET /v1/updates`) and answers (`POST /v1/messages`). Real login by SMS is milestone 4.

## Quick start

Needs the .NET 11 SDK, Node 26 and Docker (for PostgreSQL on your machine).

```bash
(cd client && npm ci && npm run build && npm test)   # the server embeds the built client
dotnet test -c Release
docker compose up -d
dotnet run --project src/Dalan.Host
```

Try it with the demo bot (a new database gives it id 1):

```bash
docker compose exec postgres psql -U dalan -c "INSERT INTO relay.bots (username, name, owner_user_id, token_hash, created_at) VALUES ('demo', 'Demo bot', 0, sha256('local-demo-secret-not-for-production'), now()) RETURNING id"
node scripts/demo-bot.mjs 1
```

Then open <http://localhost:5000/@demo> in Chrome or Firefox, log in with any Iranian mobile number, and press Start.

## Docs

- [Design](docs/DESIGN.md) — what and why
- [Architecture](docs/ARCHITECTURE.md) — how the code is shaped
- [Testing](docs/TESTING.md) — how rules are tested
- [Specs](openspec/specs) — current rules
- [CLAUDE.md](CLAUDE.md) — commands, layout and rules for contributors and AI agents
