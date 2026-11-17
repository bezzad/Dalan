# Design

## Context

Milestones 1 to 3 are merged. The session is already the real one (ES256 JWT in a cookie); only the way to get it is a development login. This milestone adds the real ways in. Decisions already made: `docs/DESIGN.md` section 9 and `docs/ARCHITECTURE.md` section 7. The owner delegated the spec review and said to use section 18 starting values as defaults.

## Goals / Non-Goals

**Goals:**
- A real person logs in with a phone number and an SMS code, then stays logged in for weeks without a new SMS.
- SMS cannot be used to burn money: every SMS costs the caller work and is counted.
- Password login for people who set one.

**Non-Goals:**
- No TOTP, no "SMS on every login", no email. They need an account screen with more parts; they come as their own change.
- No audit table. Security events go to the log now; the table comes with the Admin service (milestone 8).
- No user suspend. Refresh only checks that the user still exists.
- No second SMS provider or fallback. One more class when it is needed.

## Decisions

1. **Versions (checked 2026-11-17):** `Konscious.Security.Cryptography.Argon2` 1.3.1.
2. **SMS is a sync call through a contract, not an outbox event.** `docs/ARCHITECTURE.md` planned `SmsRequested` through the outbox. Changed, because a login code lives 2 minutes: a late retry is useless, and the person must know at once when the SMS failed. Identity calls `ISmsSender` (in `Dalan.Notification.Contracts`); Notification owns the providers. A failed send frees the slot, so it costs the user nothing.
3. **Providers:** setting `Notification:SmsProvider`. `log` writes the code to the log and is allowed only in Development (the server refuses to start with it elsewhere, like the development login). `kavenegar` calls Kavenegar "verify lookup" (`/v1/<key>/verify/lookup.json`, with the template name and the code). Empty: every send fails. The API key is part of the address, so this HTTP client writes no request log.
4. **Proof of work, no server state.** `POST /api/auth/challenge` with the phone gives a challenge: `<expiry>.<salt>.<mac>`, where the MAC covers the phone, the expiry and the salt. The browser finds a `nonce` so that SHA-256 of `<challenge>.<nonce>` starts with `Identity:PowDifficultyBits` zero bits. A challenge works only for its phone and until its expiry. It can be used again inside its short life, but the limit of 1 SMS per minute per number still holds, so a replay gains nothing.
5. **Keys for MACs** come from the signing key (HKDF, one label per use), so two servers with the same `Identity:SigningKey` agree, and there is no new secret.
6. **SMS codes** are rows in `identity.sms_codes` (phone, MAC of the code, address, times, tries, used). The limits are counts over this table for the last minute and the last 24 hours, so they survive a restart. Rows older than a day are deleted when a new code is made. A PostgreSQL advisory lock on the phone makes "check, then insert" safe.
7. **Only the newest code of a number is valid**, for `Identity:SmsCodeSeconds`, for at most `Identity:SmsCodeTries` tries, once.
8. **New user:** the first right code for an unknown number answers `name_required` and does not use up the code; the client asks the name and sends the same code again with it.
9. **Refresh token:** 32 random bytes in the cookie `__Host-dalan_refresh` (same flags as the session cookie). One row per device in `identity.sessions` holds the SHA-256 of the current token and of the one before it. A refresh swaps them in one `UPDATE`, so two calls cannot both win. An older token that comes back after `Identity:RefreshReuseGraceSeconds` deletes the row (stolen token). Inside the grace it only gets 401: two tabs that refresh at the same moment must not log the person out.
10. **All logins start a session the same way** (SMS, password, development): one session row, two cookies. The access token carries how the session started (`amr`: `sms`, `pwd`, `dev`).
11. **Password:** Argon2id, stored as the standard PHC text (`$argon2id$v=19$m=…,t=…,p=…$salt$hash`), so the cost can change later without breaking old hashes. At most `Identity:PasswordHashConcurrency` hashes run at once, because each one takes memory.
12. **Changing a password** needs the current one, except when there is none yet or the session started with an SMS code (that is the "forgot password" path). It ends every other session of the user.
13. **Password tries** are limited per login name and per client address in memory (`Identity:PasswordTries` and `Identity:PasswordTriesPerIp` per `Identity:PasswordTriesWindowMinutes`). An unknown name and a wrong password get the same answer and take the same time.
14. **Username:** 5 to 32 of `a-z`, `0-9`, `_`, starting with a letter; unique without regard to case (AUTH-01).
15. **JSON:** Identity answers in snake_case like Relay. Identity paths: `/api/auth/...` (no session needed) and `/api/account/...` (session needed).
16. **Client:** any call that gets 401 asks for a refresh once and tries again; calls that wait at the same moment share one refresh. The proof of work uses the browser's own SHA-256 (`crypto.subtle`), no package.
17. **Security log:** log lines with a fixed event name (`login`, `login_failed`, `sms_sent`, `sms_refused`, `refresh_reuse`, `password_set`). Phone numbers are masked; codes and passwords never appear.

## Starting values used

| Setting | Value | Source |
|---|---|---|
| `Identity:RefreshTokenDays` | 30 | New, assumed |
| `Identity:RefreshReuseGraceSeconds` | 10 | New, assumed |
| `Identity:PowDifficultyBits` | 14 | New, assumed (about 16,000 hashes: near 1 second in a browser) |
| `Identity:PowChallengeSeconds` | 120 | New, assumed |
| `Identity:SmsCodeDigits` | 6 | New, assumed |
| `Identity:SmsCodeSeconds` | 120 | New, assumed |
| `Identity:SmsCodeTries` | 5 | New, assumed |
| `Identity:SmsWaitSeconds` | 60 | Section 18 (1 per minute, owner) |
| `Identity:SmsPerPhonePerDay` | 5 | Section 18 (assumed) |
| `Identity:SmsPerIpPerDay` | 200 | New, assumed (high: a safety net only) |
| `Identity:SmsPerDay` | 1000 | New, assumed (the global budget) |
| `Identity:PasswordMinLength` / `PasswordMaxLength` | 8 / 128 | New, assumed |
| `Identity:PasswordMemoryKb` / `PasswordIterations` | 19456 / 2 | New (OWASP minimum for Argon2id) |
| `Identity:PasswordHashConcurrency` | 2 | New, assumed (2-core server) |
| `Identity:PasswordTries` / `PasswordTriesWindowMinutes` | 5 / 15 | New, assumed |
| `Identity:PasswordTriesPerIp` | 100 | New, assumed (high: a safety net) |
| `Notification:SmsProvider` | empty (`log` in `appsettings.Development.json`) | New |
| `Notification:Kavenegar:BaseUrl` | `https://api.kavenegar.com` | New |
| `Notification:Kavenegar:ApiKey` | empty (secret, set on the server) | New |
| `Notification:Kavenegar:Template` | `dalan-login` | New |
| `Notification:Kavenegar:TimeoutSeconds` | 10 | New, assumed |
| `Identity:AccessTokenMinutes` in Development | back to 15 (was 720) | Changed: refresh makes the long value needless |

## Risks / Trade-offs

- [SHA-256 proof of work is cheap for a native program] It stops simple scripts, not a serious attacker → the real guards are the limits per number and the global budget.
- [Kavenegar sender was not run against the real service] No account yet → tested against a fake HTTP server; check once with the real account before real users.
- [Access token lives on after logout from another device] Up to `AccessTokenMinutes` → as planned in `docs/ARCHITECTURE.md`; the deny list comes with suspend (milestone 8).
- [Password try limit is in memory] It resets at restart and is per process → fine for one process; move to the database if a second process comes.
- [Budget reached] Nobody new can log in by SMS until rows age out → an error log line; password logins and open sessions keep working.

## Migration Plan

New migration `Login` in `identity`. On the server, before real users: set `Identity__SigningKey`, `Notification__SmsProvider=kavenegar`, `Notification__Kavenegar__ApiKey`, and make the OTP template `dalan-login` (one token) in the Kavenegar panel. Rollback: the old release ignores the new tables and column.
