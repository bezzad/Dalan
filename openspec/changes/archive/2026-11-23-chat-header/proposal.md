# Proposal

## Why

The owner (2026-11-23): the chat page shows the Dalan header and a second row with "Back". A bot's page should look like the bot's own app, served by Dalan: only the bot's information on top, and no Back button (the browser and the Dalan mark go back).

## What Changes

- CLI-45 (new): the chat page has one header: the bot's icon (its showcase icon, else its first letter), its name and a line with `@username` and its status (CLI-19), then search, theme, the user menu as a round initial, and the Dalan mark to the home page. No Back button, no language button, no Dalan name on this page.
- Search shows only the loaded messages whose text or file name has the words; Escape or ✕ closes it and shows all messages again.
- Other pages keep the site header.

## Capabilities

### New Capabilities

- `client`: CLI-45.

## Impact

Client only: chat.ts, a new chatHeader.ts, main.ts, public.ts (search icon, Dalan mark), app.css, i18n (search texts). No server change: the icon is the public showcase icon.
