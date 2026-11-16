# Design

## Context

Milestones 1 and 2 are merged: one process, PostgreSQL, the bot queue, the core Bot API. This milestone adds the door for people. Decisions already made are in `docs/ARCHITECTURE.md` (Client API inside Relay, POST + SSE, outbox, JWT in a cookie, the seams `IUpdateQueue` and `IMessageNotifier`). The owner delegated the spec review and said to use section 18 starting values as defaults.

## Goals / Non-Goals

**Goals:**
- A person can open `dalanet.ir/@bot`, press Start, tap reply buttons, and see the bot's answers live.
- Nothing is lost when a live message is missed: sync by sequence number.
- The session is the real one (ES256 JWT in a cookie), so milestone 4 only adds new ways to log in.

**Non-Goals:**
- No SMS, password, TOTP or refresh token (milestone 4). A session ends after its lifetime and the user logs in again.
- No stop or block of a bot. No screen sets these states yet; the states stay in the model.
- No unread counts, bot status, "not delivered" on screen, or Web Push (milestone 7). The service worker only makes the app installable and opens the shell when offline.
- No messages kept on the device. The client holds them in memory and loads the newest page on open.
- No Relay contract project content yet; `MessageCreated` stays inside Relay until Notification reads it (milestone 7).
- No browser test in CI. Client logic is tested as pure modules with the Node test runner; the full flow was checked by hand in a real browser.

## Decisions

1. **Versions (checked 2026-11-16):** `Microsoft.AspNetCore.Authentication.JwtBearer` 11.0.0, `Microsoft.Extensions.FileProviders.Embedded` 11.0.0, TypeScript 7.0.2 (unchanged), `vazirmatn` 33.0.3.
2. **Session = the standard JWT bearer handler, token read from a cookie.** Identity signs with ES256 and registers the handler; the host runs authentication; Relay only asks ASP.NET Core "who is the user" (`sub` claim). Relay needs no Identity code and no key. Hand-written JWT code was rejected: the library is the reviewed path.
3. **Cookie:** `__Host-dalan_session`, `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/`. The `__Host-` prefix makes the browser refuse it without `Secure` or with a `Domain`. `Secure` is always on; browsers accept it on `http://localhost` (Chrome, Firefox).
4. **Signing key:** setting `Identity:SigningKey`, a P-256 private key as PKCS#8 base64 (one line, fits an env file). Make one with `openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -outform DER | base64 -w0`. Without it: a temporary key and a warning, so local work needs no setup.
5. **Development login is behind two switches:** the setting `Identity:DevLogin` (off in `appsettings.json`, on in `appsettings.Development.json`, which is not in the release) and the Development environment. Setting on without Development: the server throws at start, so the deploy script rolls back. It finds or creates the user by phone number, like the real flow without the SMS. Milestone 4 replaces it.
6. **Cross-site protection:** `SameSite=Strict` cookie, and bodies must be JSON (a cross-site form cannot send JSON without a CORS preflight, and no CORS is allowed). Hosted sites (Layer 2) live on another registered domain, so they are never "same site".
7. **Per-address limit moves to the host** as the built-in ASP.NET Core rate limiter, after static files and before authentication. There are now three doors (Bot API, Client API, Identity) and later more; one place covers them all. Settings move from `Relay` to `Dalan`. The per-bot limit stays in the Bot API filter; the new per-user limit sits in the Client API filter.
8. **Client API paths:** all under `/api`. Identity owns `/api/me`, `/api/logout`, `/api/dev/login`; Relay owns `/api/bots...`, `/api/events`, `/api/settings`. JSON is snake_case, errors are `{"error","message"}`, as in the Bot API.
9. **Message shape for people:** `{"id": <seq>, "from": "user"|"bot", "text", "date", "reply_keyboard"?}`. The client knows a message by bot id and `id`.
10. **Reply keyboard:** rows of button texts inside the message content (`jsonb`, no new column). Absent = no change, empty list = remove, like Telegram's keyboard that stays until replaced. The client takes the keyboard from the newest loaded bot message that carries one. Known limit: if that message is older than the loaded page, the buttons are not shown until the bot sends them again. A tap sends the button's text as a normal message.
11. **Outbox:** table `relay.outbox` (id, type, payload `jsonb`, created_at). The message insert and the outbox insert are one `SaveChanges` in one transaction. The worker runs `DELETE ... WHERE id IN (SELECT ... ORDER BY id LIMIT n FOR UPDATE SKIP LOCKED) RETURNING ...` inside a transaction, passes the events on, then commits; a failure rolls back and the events stay. The writer wakes the worker in memory after commit, so the normal delay is one database round trip; the poll (`Relay:OutboxPollSeconds`) is only the safety net. Delivery is at least once; the client drops duplicates by `id`.
12. **`IMessageNotifier`** is the second seam: `Publish(event)` and `Subscribe(userId)`. In memory now: per user a short list of subscriptions, each a bounded `Channel` (here a `Channel` is right: events are only passed on, never confirmed). For more than one server, swap it for PostgreSQL `LISTEN/NOTIFY`.
13. **One stream per user, not per chat.** `GET /api/events` carries the messages of all the user's chats. Written by hand (`text/event-stream`, `event:`/`data:` lines, `: ping` comments): a few lines, and full control over heartbeat and end of stream.
14. **Stream limit: newest wins.** Over `Relay:MaxStreamsPerUser` the oldest stream is closed, as with long polls (Q-05). Refusing the new one would lock out a phone whose old connections died silently.
15. **Stream end:** at session expiry (the token's `exp`), at server stop, when the buffer is full, or when replaced. The client then reconnects with a growing wait and syncs.
16. **Sync:** on open the client loads the newest page; on every stream open it asks `after=<newest id>` until a page is not full. The stream is opened first and sync runs after it is open, so no message can fall between the two.
17. **Client build:** plain TypeScript compiled by `tsc` to ES modules, no bundler and no framework. `client/build.mjs` (Node, works on Windows too) runs `tsc`, copies `client/public` and the font to `client/dist`. Screens are small modules that build DOM nodes; logic that can be wrong (message list, sync, keyboard, reconnect wait, languages) is in pure modules with tests.
18. **Client tests:** Node's built-in test runner runs the TypeScript tests directly (`node --test`), no test package.
19. **Client inside the binary:** `Dalan.Host` embeds `client/dist/**`; the host serves it with the embedded file provider. `dotnet build` fails with a clear message when `client/dist` is missing. One mode for development and production.
20. **Routes of the page:** only `/` and `/@<username>` return the page. No catch-all, so a wrong address is a real 404.
21. **Font:** Vazirmatn (OFL), variable `woff2`, from the npm package at build time, served by us.
22. **Languages:** two text lists (`fa`, `en`) with the same keys; the choice is kept in `localStorage`. CSS uses logical properties, so one style sheet serves both directions. Dates use `Intl` (Persian calendar and digits in Persian).
23. **Client settings from the server** (`GET /api/settings`): the client hard-codes no limit (CLAUDE.md rule 4).
24. **Security headers** are set by the host on every response. The CSP makes rule 7 (nothing from a foreign host) a browser-enforced fact, not only a habit.

## Starting values used

| Setting | Value | Source |
|---|---|---|
| `Identity:AccessTokenMinutes` | 15 (720 in `appsettings.Development.json`) | New |
| `Identity:DevLogin` | false (true in `appsettings.Development.json`) | New |
| `Identity:SigningKey` | empty (secret, set on the server) | New |
| `Dalan:IpRequestLimit` / `Dalan:IpLimitWindowSeconds` | 100 / 1 | Moved from `Relay` |
| `Relay:UserRequestLimit` (per `Relay:RateLimitWindowSeconds`) | 10 | New |
| `Relay:MaxMessagesPerSync` | 100 | New |
| `Relay:MaxKeyboardButtons` | 100 | New |
| `Relay:MaxButtonTextLength` | 64 | New |
| `Relay:MaxStreamsPerUser` | 5 | New |
| `Relay:StreamHeartbeatSeconds` | 20 | New |
| `Relay:StreamBufferSize` | 100 | New |
| `Relay:OutboxPollSeconds` | 5 | New |
| `Relay:OutboxBatchSize` | 100 | New |
| `Relay:ClientReconnectMinSeconds` / `ClientReconnectMaxSeconds` | 1 / 30 | New |

## Risks / Trade-offs

- [No refresh token yet] A session ends after its lifetime → development uses 12 hours; milestone 4 adds refresh.
- [Temporary signing key in production by mistake] Users are logged out at each restart → warning in the log; the deploy notes name the setting.
- [More than 5 open tabs] The tabs take turns reconnecting → bounded by the per-user rate limit; a "replaced" event can stop it later if it ever matters.
- [`Secure` cookie on `http://localhost` in Safari] Safari refuses it → use Chrome or Firefox for local work.
- [No browser test in CI] A broken screen can pass CI → logic is in tested modules; a browser test can be added when the screens grow (milestone 6).
- [Cannot build .NET 11 on the dev machine] → checked locally on .NET 10 in a copy; CI is the source of truth.

## Migration Plan

New migration `Outbox` in `relay`. On the server, add `Identity__SigningKey=<key>` to `/etc/dalan/dalan.env` before the first deploy of this release. Rollback: the old release ignores the new table.
