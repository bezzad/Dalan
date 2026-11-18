# Proposal

## Why

The owner finds the login screen too complex and wants it simple like ngrok: one card, "Log in with GitHub" first, then email (owner, 2026-11-18). Developers, who build bots and sites, almost all have a GitHub account.

## What Changes

- **GitHub login (level 1):** `GET /api/auth/github` sends the browser to GitHub; `GET /api/auth/github/callback` comes back, reads the GitHub user and the primary verified email, logs in, and sends the browser to `/`. A user with the same email is the same user (the GitHub id is saved on it). A new user gets the email and the GitHub name. Like an email user, a GitHub user is level 1 and needs a verified phone to build (AUTH-26).
- **Legal record:** the callback writes the access log row (ADM-01) with the real address and port of the caller.
- **Login screen:** one card like ngrok: "Log in with GitHub" (when it is set up), "or", the email form (or SMS when email is off), and small links for SMS, password and development login.
- **Server setup:** `deploy/install.sh` takes the GitHub OAuth app id and secret from its environment and writes them into `/etc/dalan/dalan.env`, so no manual edit is needed.

GitHub may be unreachable from Iran during a shutdown: the callback then fails with `github_unavailable` and the screen says to use email or SMS. Email and SMS login do not change.

Out of scope: Google login (needs a second app at Google; later if wanted), unlinking GitHub, showing the GitHub account in the account screen.

## Capabilities

### Modified Capabilities

- `identity`: AUTH-01, AUTH-16; new AUTH-27 to AUTH-29.
- `client`: CLI-33; new CLI-35.
- `operations`: new OPS-13.

## Impact

- Migration: `identity.users.github_id` (unique, nullable).
- New settings: `Identity:GitHub:ClientId` (empty = off), `Identity:GitHub:ClientSecret`, `Identity:GitHub:CallbackUrl` (`https://dalanet.ir/api/auth/github/callback`), `Identity:GitHub:WebUrl` (`https://github.com`), `Identity:GitHub:ApiUrl` (`https://api.github.com`), `Identity:GitHub:StateSeconds` (600), `Identity:GitHub:TimeoutSeconds` (10).
- Docs: DESIGN section 9, ARCHITECTURE decisions log, SERVER.md (how to make the GitHub OAuth app).
