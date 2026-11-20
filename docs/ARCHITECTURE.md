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
| **Notification** | push subscriptions | **async**, except the login SMS (`ISmsSender`) and the subscription calls (`IWebPush`), which are sync. It gets "send this to this receiver by this channel" and knows no business. Channels: Web Push and SMS, later Email |
| **Admin** | settings the admin changed, audit log, access log; the one admin door `/api/admin/...` | sync. Other services write the access log and audit log through `IAccessLog` and `IAuditLog`; Admin calls `IUserAdmin` (Identity) and `IBotAdmin` (Relay) for users and bots |

Relay has two thin HTTP doors, both inside Relay:

- **Client API** (`dalanet.ir`): the door for **people** (PWA). Send a message or button press (POST), live messages (SSE), history sync, file upload. It never runs bot logic; the bot at home does.
- **MCP door** (`/api/mcp`, inside the Client API group): an MCP server for builders' AI agents, stateless, with the official C# SDK. Its tools and the builder REST calls both call `BuilderBots`, so they share one set of rules.
- **Bot API** (`api.dalanet.ir`): the door for **bots**. Token auth, `getUpdates` long polling, `sendMessage` and the rest.

**Telegram adapter** is a plugin, not a service: no data, no domain. The host mounts it at `/bot<token>/<method>`, at the root like Telegram (the `Telegram.Bot` library keeps only the host of its base address). It turns Telegram-shaped calls into calls of `IBotGateway` (in `Dalan.Relay.Contracts`) and back. Rules in `openspec/specs/telegram`; it follows Telegram Bot API 10.4 and serves only what the core can do.

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
deploy/                               deploy.sh, server setup, systemd units, Caddyfile, backup, drill (docs/SERVER.md)
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
- Events: `MessageChanged` (→ SSE, and Web Push for a new bot message); planned: `UserSuspended` (Identity → all).
- The login SMS is **not** an event: Identity calls `ISmsSender` (in `Dalan.Notification.Contracts`) and waits. A code lives 2 minutes, so a late retry is useless and the person must know at once when the SMS failed.
- Built so far: `relay.outbox` with `MessageCreated`. One worker in Relay takes the oldest rows, passes them to `IMessageNotifier`, and deletes them, all in one transaction (at least once). The writer wakes the worker in memory after its commit; a poll is the safety net.
- Relay learns a user's name from Identity through `IUserDirectory`. That profile has no phone number. `IUserDirectory.GetAccessAsync` says whether a user is admin, builder or suspended; it is asked on every admin and builder call, so a change works at once.
- Only Admin uses `IUserAdmin`, the one contract with phone numbers (AUTH-02).
- Push: `MessageChanged` has a `new` flag. For each new bot message the outbox worker calls `IWebPush.Send` (Notification.Contracts), which only puts the push in an in-memory queue; a few workers in Notification send it. A push lost in a restart is not sent again; the message arrives by sync.

## 5. Data

- One PostgreSQL database, one schema per service, no foreign key or join across schemas. Each schema can later move to its own database.
- EF Core 10 with the official PostgreSQL provider `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x (version 11 of the provider was only a release candidate on 2026-11-16; upgrade when stable). Message content in `jsonb`. Table and column names are snake_case.
- Each service has its own EF Core migrations and its own history table in its schema. The server applies them at start (`/ready` is 503 until then), so the Iran server needs no tool.
- New migration: `dotnet ef migrations add <Name> --project src/Relay/Dalan.Relay --startup-project src/Dalan.Host --context RelayDb --output-dir Data/Migrations` (the same for Identity with `IdentityDb` and Admin with `AdminDb`).

## 6. Bot queue and delivery

- One in-memory queue per bot behind `IUpdateQueue`: a small sorted list with a lock (not a `Channel`, because an update must stay until it is confirmed and can be removed from the middle). Rules in `openspec/specs/bot-queue`.
- Every user message is saved in the database first. Its row id is the update id, so a bot's offset stays valid after a restart. On start, the server reloads undelivered, unexpired messages into the queues, so a deploy or restart loses nothing.
- A message counts as **delivered** only after the bot confirms it with the next `getUpdates` offset, like Telegram. Delivery is at least once. An offset confirms only updates the bot was really given.
- An update is offered to the bot until its expiry. After that the message is "not delivered"; a late confirm of an update the bot already got still counts.
- "Not delivered" takes the next version of the chat and an event, so the user sees the mark live and after a sync. "Delivered" is a quiet update (`DeliveryMarks`).
- Bot status: every get-updates call, through any door, passes `BotPresence` (in memory: calls running, last call). `relay.bots.last_seen_at` is written at most once per `Relay:BotOnlineSeconds`. With more than one server it becomes shared, like the queue.
- Core Bot API, token in `Authorization: Bearer`: `GET /v1/updates`; `POST /v1/messages` (text, keyboards, a file); `PATCH` and `DELETE /v1/messages/{user}/{id}`; `PUT` and `DELETE /v1/chats/{user}/pin`; `POST /v1/callbacks/{id}/answer`; `PUT` and `GET /v1/commands`; `GET /v1/files/{id}`.
- A button press is an update like a message, but it is not stored: a press waiting during a restart is lost, and the user presses again.
- Files live on the local disk in `Relay:FileFolder` (production: `/var/lib/dalan/files`), with a row in `relay.files`, and are deleted after `Relay:FileRetentionDays`. To grow past one server, this folder moves to shared storage.
- Every bot door (core and Telegram) is a thin HTTP layer over `IBotGateway`: token check, limit per bot, long poll, send. So both doors share one queue and one set of rules.
- The bot token is part of the Telegram path, so no request path may be written to the log (`TG-10`).
- Rate limits: per client address in the host, in front of every service; per bot and per user in Relay.

## 7. Identity and sessions

- JWT access token + refresh token. The only server state is one row per logged-in device (`identity.sessions`).
- The access token is kept in the cookie `__Host-dalan_session`: `HttpOnly`, `Secure`, `SameSite=Strict` (only a storage place; JavaScript cannot read it). Signed with ES256; the key is the secret setting `Identity:SigningKey`.
- Identity registers the standard JWT bearer handler. Other services only ask ASP.NET Core who the user is (`sub` claim) and need no Identity code.
- Development login (`Identity:DevLogin`) works only in the Development environment; with the setting on anywhere else the server refuses to start.
- Refresh token: random, in the cookie `__Host-dalan_refresh`, stored as SHA-256, replaced on every use (`POST /api/auth/refresh`). The row keeps the hash of the current token and of the one before it. An older token that comes back after a short grace time deletes the row (that device is logged out); inside the grace it only gets 401, because two tabs can refresh at the same moment.
- Ways to log in: GitHub or email code (level 1), SMS code (level 2), password, development login. All end in the same place: one session row and two cookies. The access token says how the session started (`amr`).
- SMS login: `POST /api/auth/challenge` → the browser solves a small proof of work → `POST /api/auth/sms` → `POST /api/auth/sms/verify`. The challenge needs no server state (a MAC ties it to the phone and an expiry). The rows of `identity.sms_codes` of the last 24 hours are the counters of the SMS limits (per number, per address, whole server), so they survive a restart.
- Email login: the same steps with `email` (`/api/auth/challenge`, `/api/auth/email`, `/api/auth/email/verify`). `identity.email_codes` counts apart from `identity.sms_codes`; both are `LoginCode` rows handled by one generic `LoginCodes<T>`. A user has a phone, an email, or both; **level 2 = has a phone**. `POST /api/account/phone` adds a phone with an SMS code. Email senders live in Notification behind `IEmailSender`; setting `Notification:EmailProvider` (`smtp`, `log`, or empty).
- GitHub login: `GET /api/auth/github` sets a state cookie (`__Host-dalan_github`, `SameSite=Lax`) and sends the browser to GitHub; `GET /api/auth/github/callback` checks the state, calls GitHub with `HttpClient` (no package; a call with no answer is sent again, `Identity:GitHub:Tries`), finds the user by `github_id`, else by the primary verified email, else makes one, writes the access log row and sends the browser to `/`. Errors go to `/?login_error=<code>`. On when `Identity:GitHub:ClientId` and `ClientSecret` are set; the callback URL is a setting (`Identity:GitHub:CallbackUrl`).
- Passwords: Argon2id in PHC text form; only a few hashes run at once. Password tries are limited per login name and per client address, in memory.
- Keys for the MACs (proof of work, SMS codes) are derived from `Identity:SigningKey`, so there is no second secret.
- SMS providers live in Notification behind `ISmsSender`; setting `Notification:SmsProvider` (`kavenegar`, `log` for Development only, or empty).
- Security events (login, failed login, SMS sent or refused, refresh reuse, password change) are log lines until the audit table comes with Admin.
- **Admins** are the users whose phone is in `Identity:AdminPhones` (file or environment only, never the panel).
- **Builders**: `identity.users.builder` is `none`, `requested` or `approved`; the admin approves. Only a user with a phone may ask (`phone_required`).
- **Access keys** (for terminals and AI agents): `identity.access_keys`, one row per key with its SHA-256. The `AccessKey` scheme reads `Authorization: Bearer dalan_...`. Only the policy `SessionOrKey` (name in `Dalan.Identity.Contracts`) accepts it: the builder calls and the MCP door. Every other call takes only the session cookie; the session handler never reads the `Authorization` header. Keys are made and deleted only with a session.
- **Blocking is by user id.** A suspend deletes the user's sessions and puts the id in an in-memory deny list (`SuspendedUsers`, loaded at start) that the JWT handler checks, so an unexpired token stops at once; Admin then asks Relay to end the user's live streams. With more than one server this becomes a `UserSuspended` event.
- Access token lifetime is an admin setting.

## 7a. Settings the admin changes

- `admin.settings` holds only the values the admin changed. The host adds them as the last configuration source (`AddAdminSettings`), loaded before traffic; Admin starts first.
- A change reloads that source; every service reads settings through `IOptionsMonitor`, so it applies at once. A few settings are read only at start and are marked "after restart".
- Not in the panel: secrets, `Identity:DevLogin`, `Identity:AdminPhones`, `Dalan:TrustedProxies`, `Notification:SmsProvider`, `Notification:Kavenegar:BaseUrl`, `Relay:FileFolder` and the other `Dalan` keys. Change those in `/etc/dalan/dalan.env`.
- With more than one server, each one must reload on a shared signal.

## 8. Scale

- Design and load-test target for version 1: **100 bots, 100,000 users, about 10,000 online at once, about 500 messages per second**, on one 2-core server.
- First-year real load is far lower (about 10 users, 1,000 to 10,000 requests a day).
- To grow past one process: give a service its own host, and swap the in-memory bot queue and the SSE notifier for shared ones (PostgreSQL `LISTEN/NOTIFY`, or Redis if ever needed). The outbox already works across processes.
- A load balancer in front (for example Envoy) must be listed in `Dalan:TrustedProxies`, or every client gets its address: section 11.
- Container-ready from day one: settings from environment, logs on stdout, `/health` and `/ready`, graceful shutdown, a `Dockerfile`. Production in Iran still runs one binary under systemd (Docker Hub refuses Iranian IP addresses).

## 9. Live messages and the client

- Client API paths are under `/api`: Identity owns `/api/me`, `/api/logout`, `/api/auth/...` (no session needed), `/api/account/...` and the development login; Relay owns `/api/bots...`, `/api/events`, `/api/settings`, `/api/push...` (the door; the subscriptions live in Notification) and `/api/builder/...`; Admin owns `/api/admin/...`. Rules in `openspec/specs/relay` and `openspec/specs/live-updates`.
- `GET /api/events` is one SSE stream per user for all of that user's chats, behind `IMessageNotifier` (the second seam; in memory now). A stream ends at session expiry, at server stop, when it is too slow, or when the user opens too many; the client then reconnects.
- The client never trusts the stream alone: every time it opens, the client asks for the changes after the highest `version` it has. Every new message and every edit, delete or pin takes the next version of its chat, so a sync sees changes too.
- The PWA (`client/`) is plain TypeScript compiled to ES modules, no bundler. `client/dist` is embedded in `Dalan.Host`, so the release is still one file. Only `/` and `/@<bot>` return the page.
- Every response carries a Content-Security-Policy that allows only our own origin.

## 10. Decisions log

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
| 15 | 2026-11-16 | Session checked by the standard JWT bearer handler, token in a `__Host-` cookie; development login needs the setting and the Development environment |
| 16 | 2026-11-16 | Per-address rate limit lives in the host and covers every endpoint |
| 17 | 2026-11-16 | One SSE stream per user; over the stream limit the newest wins |
| 18 | 2026-11-16 | Client is embedded in the host binary; no bundler, no framework |
| 19 | 2026-11-16 | Reply keyboard lives in the message content; the client takes it from the newest bot message that has one |
| 20 | 2026-11-17 | Login SMS is a sync call through `ISmsSender`, not an outbox event |
| 21 | 2026-11-17 | One session row per device with the current and the previous refresh token hash; reuse after a grace time ends that session |
| 22 | 2026-11-17 | Proof of work is stateless (MAC over phone and expiry); SMS limits are counts over `identity.sms_codes` |
| 23 | 2026-11-17 | Argon2id through the package `Konscious.Security.Cryptography.Argon2`; hash stored as PHC text |
| 24 | 2026-11-17 | First SMS provider is Kavenegar; TOTP is left out of milestone 4 and comes as its own change |
| 25 | 2026-11-17 | Client address from `X-Forwarded-For` only behind the proxies in `Dalan:TrustedProxies`; no PROXY protocol |
| 26 | 2026-11-17 | Access log in Admin: address, port and time per user and bot, one row per address per interval, kept 180 days |
| 27 | 2026-11-17 | Every bot door calls one contract, `IBotGateway`; the Telegram adapter is a plugin on top of it |
| 28 | 2026-11-17 | Telegram calls at the root (`/bot<token>/<method>`), not under `/tg`; snapshot Telegram Bot API 10.4 |
| 29 | 2026-11-17 | Adapter serves only what the core has; formatting (`parse_mode`) is ignored until the core stores entities |
| 30 | 2026-11-17 | Sync by a per-chat `version`, not the sequence number, so edits, deletes and pins reach a syncing client; a delete keeps a stub |
| 31 | 2026-11-17 | One pinned message per chat (Telegram allows several) |
| 32 | 2026-11-17 | Button presses are not stored; their answers go straight to the live stream, not through the outbox |
| 33 | 2026-11-17 | Files on the local disk with expiry; a user upload is the raw body with an `X-File-Name` header (SEC-05 holds) |
| 34 | 2026-11-17 | Downloaded files never run as a page: `CSP: sandbox`, `nosniff`, `attachment` unless a safe image, audio or video |
| 35 | 2026-11-17 | Every new bot message is pushed; the service worker hides it when the chat is in front (a locked phone keeps its connection open) |
| 36 | 2026-11-17 | Push door in Relay's Client API, subscriptions and sender in Notification behind `IWebPush`; pushes wait in memory |
| 37 | 2026-11-17 | Push only to hosts in `Notification:Push:AllowedHosts` (no request to an address a user chose) |
| 38 | 2026-11-17 | Web Push through `Lib.Net.Http.WebPush` (built-in .NET crypto), not our own RFC 8291 code |
| 39 | 2026-11-17 | Unread = bot messages after `memberships.last_read_seq`; the read position only moves forward |
| 40 | 2026-11-17 | Bot status in memory, asked by the client every few seconds; no live status event (it would go to every user of a bot) |
| 41 | 2026-11-17 | "Not delivered" makes a new version and an event; "delivered" stays quiet |
| 42 | 2026-11-17 | Admin settings are a configuration source read from `admin.settings`, added last, so `IOptionsMonitor` sees changes with no service code |
| 43 | 2026-11-17 | Editable settings are computed from the configuration minus a fixed list of secrets and start-only safety settings |
| 44 | 2026-11-17 | One admin door in the Admin service; Identity and Relay keep their data behind `IUserAdmin` and `IBotAdmin` |
| 45 | 2026-11-17 | Admins by phone number in `Identity:AdminPhones`; no role in the session token, access asked on each call |
| 46 | 2026-11-17 | Builder needs only admin approval for now (no email) |
| 47 | 2026-11-17 | Suspended users and bots are kept in memory sets loaded at start, so the hot paths need no extra query |
| 48 | 2026-11-17 | Public pages (landing, privacy, FAQ, showcase) live in the PWA; the host serves the same page at their addresses |
| 49 | 2026-11-17 | The numbers the public pages print come from `GET /api/public/settings` in Admin, never from the text |
| 50 | 2026-11-17 | Showcase entries in `relay.showcase`; every builder change goes back to review; icons (PNG, JPEG, WebP, no SVG) kept in the row |
| 51 | 2026-11-17 | Anonymous public endpoints write no access log row (they know no caller); the per-address limit holds |
| 52 | 2026-11-17 | History kept per chat by a sweeper (`Relay:HistoryMessages`, `Relay:HistoryDays`); sequence numbers keep counting |
| 53 | 2026-11-17 | Terminals and AI agents use an access key (`dalan_...`, SHA-256 stored, expires), a second auth scheme in Identity that only the builder doors accept |
| 54 | 2026-11-17 | MCP door `/api/mcp` with the official C# SDK (`ModelContextProtocol.AspNetCore`), stateless; tools call the same `BuilderBots` class as REST |
| 55 | 2026-11-17 | CLI is one Node file with no packages, served by Dalan at `/dalan.mjs`; `/llms.txt` served by Admin with the addresses from settings |
| 56 | 2026-11-18 | Two levels: email code login (level 1, use bots) and a verified phone (level 2, may become a builder). SMTP through `System.Net.Mail` (no package); replaces decision 46 |
| 57 | 2026-11-18 | Login emails from our own server: Postfix (send only, loopback only) plus OpenDKIM, set up by `setup.sh`; no outside mail service |
| 58 | 2026-11-18 | "Log in with GitHub" (OAuth web flow, no package) on a one-card login screen like ngrok; a verified GitHub email is needed and the same email is the same user. No Google for now |
| 59 | 2026-11-19 | Faster CI and deploy: one build job keeps the release and the built tests as run files; each test project runs on its own machine in parallel; the deploy reuses that release and sends only the changes with rsync |

## 11. Client address and access log

The owner must be able to tell the cyber police from which address a user or a bot worked (`docs/DESIGN.md` section 10). Two things can silently break that: a proxy that hides the client, and a client that fakes its address. Rules `SEC-10`, `SEC-11`, `ADM-01` to `ADM-04`.

- **One source of the address.** The host makes `HttpContext.Connection.RemoteIpAddress` and `RemotePort` the real client's (`ClientAddress.cs`, the framework's forwarded-headers middleware). All other code reads the connection and never the `X-Forwarded-For` header.
- **The header is believed only from `Dalan:TrustedProxies`** (addresses or CIDR ranges, comma separated; read at start). The header is read from the right until the first address that is not trusted, so one proxy or several work the same, and what a client wrote is never believed.
- **Caddy replaces the header** with the client's `address:port` (`header_up X-Forwarded-For {remote}`).
- **Adding a load balancer or a second proxy:** (1) add its address or range to `Dalan:TrustedProxies` on every instance; (2) make the first hop from the internet write the client's `address:port` and drop the client's own header (Envoy: set `X-Forwarded-For` to `%DOWNSTREAM_REMOTE_ADDRESS%`); (3) a proxy behind it must keep that header (Caddy: list the balancer in `trusted_proxies` and remove the `header_up` line). If step 1 is forgotten the server logs a warning that names the proxy (SEC-11).
- **Access log:** table `admin.access_log` (actor `User`, `Bot`, `Phone` or `Email`, actor id, phone, email, address, port, time). Relay's two door filters call `IAccessLog` for every accepted call, so a new endpoint in those groups (also the MCP door `/api/mcp`) is covered with no extra code. The access key calls of Identity write a row too. A new door that knows its caller must call `IAccessLog` too. The anonymous public endpoints (`/api/public/settings`, `/api/showcase`, `/llms.txt`) know no caller and write no row. A row is written for a new address at once and for a known one after `Admin:AccessLogIntervalMinutes`; "last written" is in memory per instance. A call whose row cannot be written fails.
- **SMS and email codes:** Identity calls `IAccessLog.RecordSmsAsync` / `RecordEmailAsync` for every login code that was sent: actor `Phone` or `Email`, the number or address, and the address of the caller who asked. A logged-in email user is recorded by user id like everyone else.
- **Retention:** a sweep deletes rows older than `Admin:AccessLogRetentionDays`.
- **Admin screen:** the access log tab searches by user, bot, phone, email or address (`GET /api/admin/access-log`, ADM-10), with times in UTC. Plain SQL still works: `SELECT address, port, at FROM admin.access_log WHERE actor = 'Bot' AND actor_id = 5 ORDER BY at`.
