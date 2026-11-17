# Design

## Context

Milestones 1 to 7 are merged. All services read settings through `IOptionsMonitor<T>`, so a configuration source that changes at run time reaches them with no code change. Bots are created only by tests or SQL. The access log exists but has no screen. The owner delegated the spec review; section 18 values are used as defaults.

## Goals / Non-Goals

**Goals:**
- What DESIGN section 19 lists for milestone 8: limits, builder approval, suspend, bot creation and token rotation.
- Every limit, expiry, size and domain can be changed from the panel (CLAUDE.md rule 4), including `Admin:AccessLogRetentionDays` (owner, 2026-11-17).
- The owner finds the address, port and time of a user, a bot or a phone number for a legal complaint (rule 13).

**Non-Goals:**
- Email, per-user and per-bot limit overrides, bot profile and showcase, deleting a bot, several admin roles, settings shared by several server instances.

## Decisions

1. **One admin door, in Admin.** All `/api/admin/...` endpoints live in the Admin service. Admin owns settings, the audit log and the access log; for users and bots it calls the contracts `IUserAdmin` (Identity.Contracts) and `IBotAdmin` (Relay.Contracts). Each service still owns its data. One door means one admin check, one access log call and one audit point.
2. **Who is admin.** A user whose phone number is in `Identity:AdminPhones` (comma separated, any Iranian form). It lives only in the file or environment, never in the panel, so a stolen admin session cannot make new admins. The admin door asks Identity on every call (`IUserAdmin.GetAccessAsync`), so removing a number works at once. Admin calls are rare, so one small query each is fine.
3. **Settings from the database.** `admin.settings` (key, value, time, admin). A configuration provider in Admin is added by the host as the last source, so a stored value wins over `appsettings.json` and the environment. It is loaded after Admin's migration and before the server takes traffic; Admin starts first. A change reloads the provider, and `IOptionsMonitor` gives the new value at once. A stored value that is no longer valid (unknown key, wrong kind) is skipped with a warning.
4. **Which settings are editable.** Every leaf under `Admin`, `Identity`, `Relay`, `Notification` and the two per-address limits of `Dalan`, except a fixed list: secrets (`Identity:SigningKey`, `Notification:Kavenegar:ApiKey`, `Notification:Push:PrivateKey`, `Notification:Push:PublicKey`), and start-only safety settings (`Identity:DevLogin`, `Identity:AdminPhones`, `Dalan:TrustedProxies`, `Notification:SmsProvider`, `Notification:Kavenegar:BaseUrl`, `Relay:FileFolder`, `Dalan:Version`, `Dalan:ShutdownTimeoutSeconds`, `Dalan:StartupRetrySeconds`). The list is computed from the configuration, so a new setting is editable with no extra code. The kind comes from the start value: a whole number, `true`/`false`, or text. A few are read only at start and are marked "after restart": `Dalan:IpRequestLimit`, `Dalan:IpLimitWindowSeconds`, `Identity:PasswordHashConcurrency`, `Notification:Push:QueueSize`, `Notification:Push:Workers`, `Notification:Push:TimeoutSeconds`.
5. **Value checks.** Number: a whole number from 1 to 2,147,483,647. Switch: `true` or `false`. Text: at most `Admin:MaxSettingLength` characters. No checks between settings (for example min below max): the admin is the owner.
6. **Audit log.** `admin.audit_log` (admin or user id, action, target, detail, time) through the contract `IAuditLog`. Written for every admin action, and for bot creation and token rotation by a builder. A setting change keeps the old and new value. Kept with no expiry: it is small. Security events (logins) stay log lines.
7. **Builder state** in `identity.users.builder`: `none`, `requested`, `approved`. Refusing sets it back to `none`. Admins count as builders. Relay asks `IUserAdmin.GetAccessAsync` before each builder call, so an approval works at once and no role sits in the session token.
8. **Suspend a user.** `identity.users.suspended_at`. Identity deletes the user's sessions and keeps the ids of suspended users in memory (loaded at start); the JWT handler refuses a token of a suspended user, so even an unexpired session token stops at once. SMS and password login answer 403 `suspended` before an SMS is sent. Admin then calls `IBotAdmin.EndUserStreams`, which closes the user's live streams. With more than one server this becomes the `UserSuspended` event (docs/ARCHITECTURE.md section 7).
9. **Suspend a bot.** `relay.bots.suspended_at`. `IBotGateway.EnterAsync` still records the access log for a valid token, then answers "suspended": the core Bot API gives 403 `bot_suspended`, the Telegram adapter 403 `Forbidden: bot was suspended`. A user who writes to, presses a button of or starts a suspended bot gets 403 `bot_suspended`. A poll already waiting ends at its timeout. Queued updates expire as usual.
10. **Builder bots.** `POST /api/builder/bots` in Relay's Client API group (session, access log, per-user limit hold). Username like a user's: 5 to 32 of `a-z 0-9 _`, starting with a letter, stored lower case, unique among bots. At most `Relay:MaxBotsPerBuilder` per owner. The token is in the answer once. Rotation replaces the hash; the old token stops at once.
11. **Search.** Users by exact id, phone (any Iranian form) or username, or a name prefix; bots by id, username or owner id. Access log by one of user id, bot id, phone or address (exact, IPv4 written as IPv6 is folded), newest first, `before=<row id>` for the next page, `Admin:PageSize` rows. Times are UTC.
12. **Client.** Two more overlays like the account screen: "My bots" (for builders) and "Admin" (for admins), opened from the account screen. Plain tables and forms, Persian and English.

## Starting values used

| Setting | Value | Source |
|---|---|---|
| `Identity:AdminPhones` | empty (owner sets it) | Owner |
| `Admin:PageSize` | 100 | Assumed |
| `Admin:MaxSettingLength` | 1000 | Assumed |
| `Relay:MaxBotsPerBuilder` | 20 | Assumed |
| `Relay:MaxBotNameLength` | 64 | Assumed |
| Builder needs | admin approval only, no email | Assumed (coordinator, 2026-11-17; email comes later) |

## Risks / Trade-offs

- [A stolen admin session can change limits] Secrets, the admin list and the proxy list are not in the panel; every change is in the audit log; sessions are short.
- [One server only] A setting change and a suspend reach only this process. A second server needs a shared signal (docs/ARCHITECTURE.md section 8).
- [A wrong value can hurt the server] For example a tiny `Relay:PollTimeoutMaxSeconds`. Reset returns the start value at once.
