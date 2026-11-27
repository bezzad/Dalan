# Proposal

## Why

When the client's live stream to Dalan is down (CLI-08), a grey bar with "Connecting…" sticks to the top of every screen (`body.offline::before` in `app.css`, `data-connecting` in `main.ts`). It pushes the page down (`body.offline main { height: calc(100dvh - 28px) }`) and looks like an error toast. The owner wants a quiet sign instead, like a messenger: a ring that turns around the bot's icon while connecting (owner, 2026-11-25).

## What Changes

1. **No bar (CLI-75).** The "Connecting…" bar and its height rule go away.
2. **A ring.** While the stream is down, a thin ring turns around the bot's icon in the chat header (CLI-45). On a screen with no bot icon (chat list, account, admin), the same ring turns around the Dalan logo in the header. When the stream is back, the ring goes away. With reduced motion the ring does not turn; it shows as a still dashed ring.
3. **Tooltip.** On a device with a mouse, hovering the icon shows "Connected" or "Connecting…" in the page language. Screen readers hear "Connecting…" once when it starts and "Connected" when it is back (a `role="status"` text, not shown).

Out of scope: the bot's own online state; it stays the status line under the name (CLI-19).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: new CLI-75.

## Impact

- Client: `main.ts` (a `connecting` class on the header icon instead of `body.offline`), `chatHeader.ts`, the header logo, `i18n.ts` ("Connected"), `app.css` (ring, reduced motion; bar removed), `docs/UI.md` header section.
- No server change, no new settings.
