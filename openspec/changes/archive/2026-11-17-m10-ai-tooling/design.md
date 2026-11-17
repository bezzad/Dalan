# Design

## Context

Milestones 1 to 9 are merged. Builder calls (`/api/builder/...`) live in Relay inside the Client API group: session cookie, access log, per-user limit, builder check on every call. The session is a JWT in an `HttpOnly` cookie, so a terminal or an agent cannot use it. The owner delegated the spec review; section 18 values are used as defaults.

## Goals / Non-Goals

**Goals:**
- DESIGN section 12, version 1 part: MCP server and CLI for create bot, token, status; `llms.txt`; ready prompt.
- One set of rules for every builder door (REST, MCP, CLI): the CLI only calls REST, MCP calls the same code.
- A key can do no more than a builder can do in "My bots".

**Non-Goals:**
- OpenAPI, profile and commands from MCP, sites, domains, logs, OAuth, publishing packages to npm.

## Decisions

1. **Access key, not a login from the terminal.** A key is like a GitHub token: `dalan_` and 43 base64url characters (256 random bits), shown once, stored as SHA-256 (long and random, so a fast hash is enough, like the bot token and the refresh token). Table `identity.access_keys`: `id`, `user_id`, `name`, `key_hash` (unique), `created_at`, `expires_at`. At most `Identity:MaxAccessKeys` per user; lifetime `Identity:AccessKeyDays`. Made, listed and deleted only with a session (`/api/account/keys`), never with a key, so a stolen key cannot make more keys. Making and deleting go to the audit log (`key.create`, `key.delete`). An SMS or password login from a terminal would need the proof of work and would put a password in a shell history; a key is simpler and can be deleted.
2. **The key is a second authentication scheme, owned by Identity.** Identity adds a small handler `AccessKey` that reads `Authorization: Bearer dalan_...`, looks up the hash (one row by unique index), refuses an expired key and a suspended user (AUTH-20), and gives the same `sub` claim as a session plus `amr` `key`. The scheme name is a constant in `Dalan.Identity.Contracts`. Relay asks for it only on the builder group and the MCP door (a policy with both schemes). Every other call keeps the default policy (session cookie only), so a key there gets 401. The session handler now reads only the cookie and never the `Authorization` header.
3. **Builder rules stay in one place.** The builder calls of Relay move into one small class (`BuilderBots`: list, one, create, rotate). The REST endpoints and the MCP tools both call it and only turn its answer into HTTP or MCP.
4. **MCP door with the official C# SDK, stateless.** `ModelContextProtocol.AspNetCore` (the official SDK, 2.4.0) speaks MCP 2026-07-28 (stateless, no handshake) and 2025-11-25, so current and slightly older agents work. Writing the protocol by hand would mean following two versions ourselves. Mapped at `/api/mcp` inside the Client API group with the builder filter: access log (ADM-01, rule 13), per-user limit (SEC-03) and builder check (REL-36) hold as for REST. `Stateless = true`: no server memory per agent, so it fits more than one server later. Tools: `list_bots`, `create_bot(username, name)`, `rotate_token(bot_id)`, `bot_status(bot_id)`. A tool answers the JSON of its REST call as text; a refused call is a tool error with the same English message as REST, so an agent can fix and retry.
5. **CLI is one Node file served by Dalan.** `client/public/dalan.mjs`, Node 18 or newer, no packages, built into the client and served at `/dalan.mjs`. A builder gets it with `curl -O <site>/dalan.mjs`, from Dalan itself, so it works when international internet is cut and needs no npm. It reads `DALAN_URL` and `DALAN_KEY` from the environment, calls the REST builder calls, prints the JSON answer and exits 0; on an error it prints `{"error", "message"}` to stderr and exits 1; on a wrong command it prints the usage and exits 2.
6. **Addresses are settings.** `Admin:SiteUrl` (start `https://dalanet.ir`) and `Admin:BotApiUrl` (start `https://api.dalanet.ir`) are admin settings (rule 4: domains are configuration). `/api/public/settings` adds `site_url` and `bot_api_url`; the ready prompt uses them. Admin serves `/llms.txt` from a text file embedded in Admin, with the two addresses filled in on each call. Like the other public calls, it needs no session and writes no access log row.
7. **`llms.txt` is short and in English** (agents read it), plain text in the llms.txt shape (title, one-line summary, sections). It says: what Dalan is; connect a Telegram bot by changing only the server address and token; the core API in a few lines; how to get a key; MCP config for Claude Code and others; the CLI; the main limits and errors.

## Starting values used

| Setting | Value | Source |
|---|---|---|
| `Identity:AccessKeyDays` | 90 | Assumed |
| `Identity:MaxAccessKeys` | 10 | Assumed |
| `Admin:SiteUrl` | `https://dalanet.ir` | DESIGN (site) |
| `Admin:BotApiUrl` | `https://api.dalanet.ir` | DESIGN (Bot API) |

## Risks / Trade-offs

- [A leaked key can make and rotate bots of its builder] It cannot read chats, change the account or make keys; it expires; the builder can delete it; a suspend stops it at once.
- [One more database read per key call] Builder calls are few; one indexed row.
- [A new package] Official SDK, kept to Relay; the protocol changes often, so the SDK is cheaper to follow than our own code.
- [The CLI needs Node] Agents and most builders have it; the REST calls are in `llms.txt` for anyone without it.
