# Proposal

## Why

After milestone 2 a bot can read and answer messages, but no person can send one: only tests can. Milestone 3 (docs/DESIGN.md section 19) gives people a door: the PWA client and the Client API behind it.

## What Changes

- **Client API** in Relay (`/api/...`, the door for people): find a bot by username, list my bots, send a message, read history (sync), live messages by SSE, client settings.
- **Reply buttons**: a bot can send a reply keyboard with a message; the client shows it under the input.
- **Outbox** in the `relay` schema with its first reader: a worker that passes `MessageCreated` to the open SSE streams through the seam `IMessageNotifier`.
- **Session** in Identity: an ES256 JWT in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie. `GET /api/me`, `POST /api/logout`.
- **Development login**: phone number and name, no SMS. Off by default; works only in the Development environment.
- **PWA client** in plain TypeScript: login, bot list, chat with reply buttons, live messages and sync. Persian (right to left) first, English second. Built files are embedded in the server binary.
- **Rate limits**: the per-address limit moves to the host and now covers every endpoint; a new per-user limit on the Client API.
- **Security headers**: a Content-Security-Policy that lets the page load only from its own origin.

Out of scope: SMS login, password, TOTP, refresh token (milestone 4); the Telegram adapter (5); inline buttons, edit, delete, pin, commands, files (6); Web Push, unread counts, bot status, "not delivered" on screen (7); bot creation screens and admin (8); landing page and showcase (9); stop and block of a bot (no screen in this milestone sets them).

## Capabilities

### New Capabilities

- `live-updates`: the event stream (SSE) and the outbox that feeds it.
- `client`: the PWA: how it is served, languages, sync, reply buttons.

### Modified Capabilities

- `relay`: the Client API and reply keyboards.
- `identity`: session cookie, who-am-I, logout, development login.
- `security`: per-address limit on every endpoint, per-user limit, security headers, cross-site protection.
- `delivery`: CI also tests and builds the client; the release holds the client.

## Impact

- New runtime packages: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.Extensions.FileProviders.Embedded`.
- New client dev package: `vazirmatn` (the Persian font, served from our own server).
- The server build now needs the built client: `(cd client && npm ci && npm run build)` before `dotnet build`.
- Settings moved: `Relay:IpRequestLimit` becomes `Dalan:IpRequestLimit`; the window for it is `Dalan:IpLimitWindowSeconds`.
- New secret on the server: `Identity__SigningKey` in `/etc/dalan/dalan.env`.
- New database migration in `relay` (table `outbox`).
