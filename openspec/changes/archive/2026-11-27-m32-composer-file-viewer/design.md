# Design

## Keys (CLI-85)

- One pure function decides: `enterAction({ key, shift, ctrl, meta, composing, coarse })` returns `send`, `newline` or `none`. `chat.ts` and the big editor both call it, so the two places cannot differ.
- `coarse` is read from `matchMedia("(pointer: coarse)")` on every keydown, not once: a tablet can get a real keyboard while the page is open. No user-agent check.
- A browser adds no line for Ctrl+Enter in a textarea, so the code adds it with `document.execCommand("insertText", false, "\n")`. This keeps the browser's undo. When `execCommand` is missing, `setRangeText` is the fallback.
- `event.isComposing` (and `keyCode` 229 for old Safari) always gives `none`.

## Growing box and big editor (CLI-86, CLI-87)

- Growing: on `input`, set `height: auto`, then `height = scrollHeight`; CSS stops it at `--compose-max-height: min(30dvh, 10.5em)` (about six lines, like Telegram; owner, 2026-11-27, after 40vh looked too big) and scrolls past that. It is a UI token in `app.css` and `docs/UI.md`, not a server setting: a look, not a limit (rule 4 is about limits). The buttons beside it stay at the bottom (`align-items: flex-end`). The box has its own class, `.compose-box`: `.box` is the landing page's diagram box.
- Lines are counted as `round(scrollHeight / line height)`, so wrapped lines count too. Over 3 shows the expand button.
- The big editor is `dialog.modal` (docs/UI.md). It does not copy the text: it **moves the same textarea element** into the dialog and back on close. So the text, the selection, `dir="auto"` and the undo stack are never lost, and there is no sync code. Full screen when `(pointer: coarse)` or the screen is narrower than 600px; otherwise 80% of the screen, in the middle.
- Ctrl+B / Ctrl+I (Cmd on macOS) put `**` / `_` around the selection with `insertText`, so undo works. Dalan does not parse user formatting: the bot gets these marks as plain text.

## Drafts (CLI-88)

- `localStorage` key `dalan.draft.<user id>.<bot id>`, written on `input` (after 300 ms of quiet), read when the chat opens, removed on send. Logout removes every `dalan.draft.*` key. A user on a shared computer must not leave text for the next person.
- Only text. A waiting file (CLI-47) and the reply bar (CLI-52) are not kept.

## Kind of view (CLI-16, CLI-89, CLI-90)

One pure function `fileView(file)`:

- `image`: kind `photo`, or a document of type `image/jpeg`, `image/png`, `image/gif` or `image/webp` (the SEC-12 inline list). Not SVG: it can hold script.
- `html`: a document of type `text/html`, or with no clear type (`application/octet-stream`, empty) and a name ending `.html` or `.htm`.
- `audio`, `video`: as today.
- `file`: everything else, a download link as today.

## Picture viewer (CLI-89)

- A fixed full-screen layer over the chat, dark background, the picture from the same file address the message uses (SEC-12 shows these types inline).
- Zoom with pointer events and a CSS `transform`: two pointers pinch (scale 1 to 4), a double tap or double click switches 1 and 2.5 at that point, one pointer pans when zoomed, kept inside the picture's edges. `touch-action: none` on the layer, so the page does not zoom.
- Download is a link to the file with `download`. Close and Esc close it. Focus returns to the message.

## HTML viewer (CLI-90, REL-57, SEC-16)

**Why a separate host.** A `srcdoc` or `blob:` frame takes the app's CSP (`default-src 'self'`), so the report's inline script would not run. Opening the app's CSP to inline script would weaken the whole app. A page on the app's own domain could set cookies for it (decision in DESIGN.md section 11). So the file comes from `view.<Sites:Domain>`: a different registered domain, as GitHub keeps user content on `githubusercontent.com`.

**Flow.**

1. The user taps Open. The client calls `POST /api/bots/{bot id}/files/{file id}/view` (with the session, as every API call).
2. Relay checks REL-29 access and that the file is HTML, writes the access log, and returns `{ "url": "https://view.<sites domain>/v/<token>", "expires_at" }`.
3. The client opens a full-screen layer with `<iframe sandbox="allow-scripts allow-popups" src=url referrerpolicy="no-referrer">` and, on load, focuses the frame so ← → reach the report.
4. `GET /v/<token>` on the view host checks the token and REL-29 access again (a deleted message closes the link), writes the access log, and returns the bytes with the SEC-16 headers.

**Token.** ASP.NET Data Protection, time-limited protector, purpose `Relay.FileView`, over user id, bot id and file id, valid `Relay:FileViewMinutes` (10). No table. The token is the only key: the view host never reads or sets a cookie.

**Two walls.** The iframe `sandbox` without `allow-same-origin`, and the response header `Content-Security-Policy: sandbox allow-scripts allow-popups`, both give the page an opaque origin. The header keeps the wall when the link is opened in a new tab (no iframe there). `connect-src 'none'` and `form-action 'none'` stop the page from sending anything out, even what the user types into it.

**Full screen.**

- When `document.fullscreenEnabled` (or `webkitFullscreenEnabled`, iPad Safari), the layer asks for full screen; leaving full screen (Esc, the browser's button) closes the viewer.
- Otherwise (iPhone Safari has no Fullscreen API for anything but video) the layer is `position: fixed; inset: 0` with `padding: env(safe-area-inset-*)`, and the page under it does not scroll. Esc and Close close it.
- Swipe inside the report works because the frame gets the touches; the layer adds no gesture of its own over the frame.

**Buttons.** Close; Download (the existing file address, an attachment by SEC-12); Open in a new tab (`window.open(url, "_blank", "noopener,noreferrer")` with the same link).

**Viewer off.** When `Relay:FileViewHost` and `Sites:Domain` are both empty, the card shows only Download.

## Server wiring

- Host: a request whose host is the view host goes to Relay's `/v/{token}` and nothing else (every other path 404), before the sites routing.
- Host: the app CSP gets `frame-src https://<view host>`.
- SITE-17: the TLS ask says yes for the view host, so the front proxy gets its certificate.
- `view` is added to the default `Sites:ReservedNames`, so no builder can claim it.

## Tests

- Client (node, the fake DOM already used in `chatHeader.test.ts`): `enterAction` for fine and coarse pointers, Shift, Ctrl, IME; growing height and the 3-line expand button; the editor moves the same textarea in and out and keeps its text on Esc; drafts per chat and removal on logout; `fileView`; the picture viewer and the HTML viewer open and close and give focus back; the iframe `sandbox` is exactly `allow-scripts allow-popups`; Fullscreen API path and fixed-layer path.
- Relay and Host (.NET): REL-57 link rules; SEC-16 headers, expiry, no cookie, view host serves only `/v/`, CSP `frame-src`, SITE-17 yes.
- Browser (new CI job, Playwright, latest stable, WebKit with the "iPhone 15" device and Chromium): open the sample report `tests/client-e2e/report.html` (one file with base64 font and pictures, ~1 MB, pages turned by ← → and buttons) in the viewer; press →; swipe; check the page changed; check from inside the frame that `document.cookie`, `localStorage` and `parent.document` all throw or are empty, and that a `fetch` is blocked.
- Real iPhone: the owner opens a real sprint review report on an iPhone after deploy (the last task). WebKit in CI is close to iPhone Safari but not the same.
