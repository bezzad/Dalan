# Design

## Decisions

1. **Only the bot's icon in its manifest.** A browser picks one icon by size and purpose; any Dalan icon in the list can win. So Dalan's icons are a fallback, never a second choice.
2. **No "maskable" for a bot icon.** A bot's picture may have content to its edges; a mask would cut it. Android then puts the "any" icon on a plain plate, which keeps the whole picture.
3. **Read the size, do not scale.** PNG (IHDR), JPEG (SOF0/SOF2) and WebP (VP8, VP8L, VP8X) keep width and height in the first bytes; a few lines of code, no package (rule 1). Old icons: a one-time pass in the migration reads them.
4. **"Fit for an app"** = square and at least 192 by 192. Chrome needs 144 or more for install and 192 for a sharp desktop icon; one number keeps it simple.
5. **One stable address `/@{username}/icon`.** The host rewrites `index.html` by plain text and knows no bot data (rule 2: Host must not read Relay's data). With an address by username, the host only swaps strings, as it does for the manifest today. The client uses the same address after an in-app move.
