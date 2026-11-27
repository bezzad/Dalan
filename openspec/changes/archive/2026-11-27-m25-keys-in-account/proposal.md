# Proposal

## Why

"My bots" (`/bots`) shows an "AI assistants" card under the bot list: access keys, the key form and the ready prompt (CLI-30, `agents.ts`, added in `builder.ts`). The owner asks why it is there (2026-11-25, screenshot): the keys are not about one bot or even about bots only. They belong to the account (AUTH-21, `/api/account/keys`) and also log in the site agent (`dalan login <key>`, "My apps"). They are account settings.

## What Changes

1. **CLI-30 (modified).** The "AI assistants" part (keys with name and expiry and delete, the make-a-key form that shows the key once, the ready prompt) moves to the account screen (`/account`), under the builder part (CLI-23). It shows only for an approved builder or an admin, like the "My bots" button there.
2. "My bots" and "My apps" no longer show the part. Each has one small link "Access keys for AI assistants" to `/account#keys`.
3. The texts, the API and the keys do not change.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: CLI-30.

## Impact

- Client: `builder.ts` (part out, link in), `apps.ts` (link), `account.ts` (part in, `#keys` anchor), `i18n.ts` (link text), `docs/UI.md`.
- `/llms.txt` and the guide steps that say where to make a key: "Account", not "My bots".
- No server change, no new settings.
