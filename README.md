# Dalan

Dalan (دالان) — run your bot or web app from a home computer, no public IP and no server. A relay platform hosted in Iran with a Telegram-compatible Bot API, phone-verified users, and site hosting.

**Status:** milestone 2 (core relay) — a bot can read user messages by long polling (`GET /v1/updates`) and answer (`POST /v1/messages`). There is no screen for users yet; that is milestone 3.

## Quick start

Needs the .NET 11 SDK, Node 26 and Docker (for PostgreSQL on your machine).

```bash
dotnet test -c Release
docker compose up -d
dotnet run --project src/Dalan.Host
curl http://localhost:5000/ready
```

## Docs

- [Design](docs/DESIGN.md) — what and why
- [Architecture](docs/ARCHITECTURE.md) — how the code is shaped
- [Testing](docs/TESTING.md) — how rules are tested
- [Specs](openspec/specs) — current rules
- [CLAUDE.md](CLAUDE.md) — commands, layout and rules for contributors and AI agents
