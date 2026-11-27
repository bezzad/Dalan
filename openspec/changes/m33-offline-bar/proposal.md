# Proposal

## Why

When a bot's home computer is off, the chat header says only "last seen ..." (CLI-19) in small grey text. Users write anyway, wait, and think Dalan is broken. The owner wants the chat to say clearly that the bot does not answer now (2026-11-26). `docs/DESIGN.md` section 16 already asks to "show bot status and 'not delivered' clearly".

## What Changes

1. **Offline bar (CLI-91).** While the open chat's bot is offline (REL-33 `online` false, also a bot never seen), a bar above the input says: "@bot is offline and won't answer now. Your messages wait for it; if it doesn't come back in time, they show 'not delivered'." (Persian too.)
2. The bar uses the status the chat already asks for every `status_refresh_seconds` (CLI-19). It hides as soon as the bot is online. Before the first answer, or when the status call fails, it stays as it was (hidden at first). A suspended bot shows its own error (CLI-25) instead, not this bar.
3. Sending still works: messages wait in the queue as before (Q-02). Nothing is refused.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: new CLI-91.

## Impact

- Client: `chatHeader.ts` (the bar), `chat.ts` (shows it), `i18n.ts` (two texts), `app.css`, a test, `docs/UI.md`.
- No server change, no new settings.
