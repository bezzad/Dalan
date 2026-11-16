# Dalan

Dalan (دالان) — run your bot or web app from a home computer, no public IP and no server. A relay platform hosted in Iran with a Telegram-compatible Bot API, phone-verified users, and site hosting.

**Status:** milestone 1 (skeleton) — the server starts and reports health; no features yet.

## Quick start

Needs the .NET 11 SDK and Node 26.

```bash
dotnet test -c Release
dotnet run --project src/Dalan.Host
curl http://localhost:5000/health
```

## Docs

- [Design](docs/DESIGN.md) — what and why
- [Architecture](docs/ARCHITECTURE.md) — how the code is shaped
- [Testing](docs/TESTING.md) — how rules are tested
- [Specs](openspec/specs) — current rules
- [CLAUDE.md](CLAUDE.md) — commands, layout and rules for contributors and AI agents
