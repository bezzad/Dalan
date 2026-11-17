# Proposal

## Why

Most builders will not connect a project by hand; an AI coding agent will do it (DESIGN section 12). Today a builder can create a bot and rotate its token only in the web app, with a browser session. An agent has no way in, and no page tells it how Dalan works. Milestone 10 (DESIGN section 19) ships the small version 1 set: an MCP server and a CLI that create a bot, give or rotate its token and read its status, plus `llms.txt` and a ready prompt.

## What Changes

- **Access keys**: a builder makes a key in "My bots" (shown once, stored as a hash, expires, can be deleted). The key works only at the builder doors, never for chats or the account.
- **Bot status**: `GET /api/builder/bots/{id}` gives one bot with `online`; the list gives `online` too.
- **MCP server**: `/api/mcp`, Streamable HTTP, stateless, built with the official C# SDK. Tools: `list_bots`, `create_bot`, `rotate_token`, `bot_status`. Same rules as the builder REST calls.
- **CLI**: `dalan.mjs`, one Node file with no packages, downloaded from the site itself. `bots`, `create`, `token`, `status`; JSON out.
- **`llms.txt`**: one plain page for agents at `/llms.txt`: how to connect a bot (Telegram-compatible or core API), keys, MCP, CLI. The site and Bot API addresses come from settings.
- **Ready prompt**: in "My bots", text with the real addresses that a builder copies into an agent.

Out of scope: an OpenAPI description, setting a bot's profile and commands from MCP, sites and domains (Layer 2), an npm package, a key per bot or with chosen rights, OAuth for MCP.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `identity`: access keys.
- `relay`: bot status, MCP door.
- `admin`: public addresses and `llms.txt`.
- `client`: keys and ready prompt in "My bots"; the CLI file.

## Impact

- Database: `identity.access_keys`. One migration.
- New package: `ModelContextProtocol.AspNetCore` (official MCP C# SDK) in Relay.
- `Dalan.Identity.Contracts` gets the name of the key scheme, so Relay can ask for it without knowing Identity's code.
- New settings: listed in design.md.
