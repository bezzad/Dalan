# Proposal

## Why

The owner (2026-11-20): the bot list showed no "Create bot" button, because his account is not an admin or a builder (admin comes from `Identity:AdminPhones`, so it needs the verified phone). Also: no page may drop the site logo; it is the only way back to the home page.

## What Changes

- "Create bot" on the bot list for every user. A builder or an admin goes to "My bots"; anyone else to the account screen, which shows the next step (verify phone, ask to become a builder, or wait).
- Every app screen (bot list, chat, account, builder, admin) has the site logo and name on top, a link to `/`, the same as the public pages.

## Capabilities

### Modified Capabilities

- `client`: CLI-24.

## Impact

Client only. No new setting.
