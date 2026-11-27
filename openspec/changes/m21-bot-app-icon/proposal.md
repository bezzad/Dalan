# Proposal

## Why

A friend of the owner installed their own bot from its chat page (PWA Install, and Chrome "Create shortcut"); the app got Dalan's icon, not the bot's (owner, 2026-11-25). Each bot has its own icon (REL-51), so its app must show it.

Causes in the code on main:

1. **The manifest offers Dalan's icons beside the bot's** (REL-54, `BotIconEndpoints.cs`): the bot icon `512x512` "any", then Dalan's `icon-192.png` "any" and `icon-512.png` "any maskable". Chrome takes the icon closest to the size it needs (often 192), and Android takes the maskable one: both are Dalan's.
2. **The page keeps Dalan's icon links.** The host swaps only the manifest link on `/@username` (`ClientApp.cs`), and the client only that link on a chat (CLI-55, `main.ts`). `<link rel="icon">` and `<link rel="apple-touch-icon">` stay Dalan's; Chrome "Create shortcut", Safari, and iPhone "Add to Home Screen" use them.
3. **The declared size can be false.** The manifest always says `512x512`, but a bot may set its icon with `setMyProfilePhoto` at any size (the 512 cut is only in the web picker, CLI-50). A browser drops an icon whose real size does not match.

## What Changes

1. **REL-54 (modified).** When the bot has an icon fit for an app (square, at least 192 by 192), the manifest lists only that icon, with its real size and purpose "any". Dalan's icons come only when it has none or it does not fit.
2. **New address `/@{username}/icon`** (in REL-54): the bot's app icon, or Dalan's `icon-192.png` when it has none that fits. No session, the same short cache as REL-51.
3. **The real size.** When an icon is saved (web picker or `setMyProfilePhoto`), the server reads its width and height from the PNG, JPEG or WebP header (no image package) and keeps them.
4. **CLI-55 (modified).** On `/@username` the page (from the host and after a move in the client) links the bot's manifest, `<link rel="icon">` and `<link rel="apple-touch-icon">` to `/@username/icon`; every other screen links Dalan's again.

Out of scope: a picture made from the first letter for a bot with no icon (Dalan's icon stays); cutting or scaling an icon on the server.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `relay`: REL-54.
- `client`: CLI-55.

Both are still in the unarchived change `m13-replies-formatting-install`; archive m13 first, or fold these texts into it.

## Impact

- Relay: `BotIcons.cs` (read width and height), `Bot` (two columns, migration), `BotIconEndpoints.cs` (manifest, `/@{username}/icon`).
- Host: `ClientApp.cs` (swap the two icon links too).
- Client: `main.ts`, `messages.ts` (`manifestFor` becomes the three links).
- No new settings (192 is the browsers' own least size for an installed app).
