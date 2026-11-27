# Proposal

## Why

The voice button in a bot chat is the emoji `🎤` (`chat.ts:44`, CLI-46). Each system draws its own emoji, most as an old stage microphone in colour, which does not match the rest of the chat bar. The owner wants a modern line microphone, like messengers use (2026-11-25, picture: a rounded capsule, an arc under it and a short stem).

## What Changes

1. **Line microphone (CLI-80).** A new `mic` icon in Dalan's icon set (`icon()` in `public.ts`, 24 by 24, line, `currentColor`), three paths:
   - capsule `M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3z`
   - arc `M19 10v2a7 7 0 0 1-14 0v-2`
   - stem `M12 19v3`
2. The voice button shows this icon instead of `🎤`, in `--on-brand` on the green button, the same size as the other icons of the chat bar. Its name for screen readers stays "Record".
3. `docs/UI.md` and the code comments say "the microphone button", not `🎤`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: new CLI-80.

## Impact

- Client: `public.ts` (icon), `chat.ts` (button), a test, `docs/UI.md`.
- No server change, no new settings.
