# Proposal

## Why

The owner (2026-11-20): after login the user is stuck on the bot list; the home page should stay the first page, the same as for a visitor. Also the owner (an admin) could not find where to create a bot: it was only under Account, "Build bots".

## What Changes

- `/` is the landing page for everyone. Logged in, its button says "My bots" and opens `/bots`.
- `/bots` is the bot list (new page address on the server). A visitor there gets the login. The chat's back button goes to `/bots`.
- The bot list has a "Create bot" button for a builder or an admin, and a "Home" link. The builder screen's back goes to the bot list.

## Capabilities

### Modified Capabilities

- `client`: CLI-01, CLI-24, CLI-26.

## Impact

Client, and one more page address in the host. No new server setting.
