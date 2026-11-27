# m36: a bot's shortcut has the bot's address, icon and name

## Why
Owner (2026-11-27): "Add to Home Screen" on a bot's page gave the bot's icon but Dalan's home address. Safari reads
the manifest only when the page loads, but the icon link when the shortcut is made. A bot's chat opened inside the app
(no page load) kept Dalan's manifest. Chromium follows the swapped link (checked with `Page.getAppManifest`).

## What changes
- CLI-55: in a browser tab, going between a bot's chat and any other screen loads the page anew, so every browser
  reads that page's own manifest; the chat's title is the bot's name. In an installed app nothing reloads.
