# Proposal

## Why

The owner (2026-11-25) wants the message box and the files a bot sends to work like Telegram:

- On a computer, Enter must send; on a phone, Enter must add a line. Today Enter always sends, so a phone user cannot write two lines.
- The box stays one row high, so long text is hard to read and edit, and a chat forgets its text when the user opens another chat.
- A photo sent with `sendDocument` shows as a link, and a photo cannot be zoomed.
- An HTML file (for example a bot's sprint review report: one file with base64 fonts and pictures, 0.5 to 3 MB, pages turned with ← → and buttons) can only be downloaded. On an iPhone a downloaded HTML file is hard to open.

## What Changes

1. **Enter like Telegram (CLI-85).** Fine pointer (mouse, real keyboard): Enter sends, Shift+Enter or Ctrl+Enter adds a line. Coarse pointer (phone, tablet): Enter adds a line, only the Send button sends. Never send while an IME is composing. Found with `matchMedia("(pointer: coarse)")`, never the user agent.
2. **The box grows (CLI-86)** up to 40% of the screen height, then scrolls. After a send it is one row again.
3. **Big editor (CLI-87).** Over 3 lines, a small "expand" button (two diagonal arrows) shows in the corner of the box. It opens a modal (phone: full screen; computer: large, in the middle) with the same text, Send and Close. Esc and Close keep the text. The CLI-85 keys work there too. Ctrl+B and Ctrl+I put `**…**` and `_…_` around the selected text.
4. **A draft per chat (CLI-88).** Each chat keeps its unsent text when the user opens another chat or reloads the page. `dir="auto"`, font, theme and the browser's undo stay as they are.
5. **Photos in the chat (CLI-89).** A photo, and a document of type JPEG, PNG, GIF or WebP, shows as a picture in the message. A tap opens a full-screen viewer with pinch and double-tap zoom, Download and Close.
6. **HTML files open in the app (CLI-90, REL-57, SEC-16).** An HTML document shows a card with name, size and "Open". Open shows the file full screen inside the app (Fullscreen API on a computer; a fixed full-screen layer with safe areas on an iPhone), with Close, Download and "Open in a new tab". The file runs in a sandboxed iframe from a separate origin (`view.<Sites:Domain>`), through a short-lived link, with a strict CSP: its script runs, but it never reaches the app's cookies, token, storage or page.
7. Other files (PDF and the rest) are downloaded as today (CLI-16).

Out of scope: a rich-text editor; PDF in the viewer (Chrome does not draw a PDF in a sandboxed frame, and iPhone Safari shows only its first page); SVG pictures (they can hold script, so they stay downloads); HTML that loads anything from the network (the CSP blocks it; one file with everything inside works).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: new CLI-85 to CLI-90; CLI-16 changed (picture documents and HTML documents are no longer plain links).
- `relay`: new REL-57 (view link for an HTML file).
- `security`: new SEC-16 (the view host cannot touch the app).

## Impact

- Client: `chat.ts` (keys, growing box, expand button, drafts), new `editor.ts` (big editor modal), `messages.ts` (picture and HTML card), new `viewer.ts` (picture viewer and HTML viewer), `api.ts`, `i18n.ts`, `app.css`, `docs/UI.md`.
- Relay: `POST /api/bots/{bot id}/files/{file id}/view` and `GET /v/{token}` on the view host.
- Host: routes the view host to Relay before the sites routing; adds `frame-src` for the view host to the app CSP; SITE-17 says yes for the view host.
- Sites: `view` joins `Sites:ReservedNames`.
- New settings: `Relay:FileViewHost` (empty = `view.<Sites:Domain>`; both empty = viewer off), `Relay:FileViewMinutes` 10.
- CI: a Playwright job (WebKit with iPhone emulation, and Chromium) opens a sample report in the viewer.
