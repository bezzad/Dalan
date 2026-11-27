# Tasks

Each task: write the failing tests tagged with the rule IDs, then the code, then run them on GitHub Actions (rule 11).

## 1. Message box

- [x] 1.1 `enterAction` and the keydown in `chat.ts`; Ctrl+Enter line with undo (CLI-85)
- [x] 1.2 Growing box, `--compose-max-height`, one row after send (CLI-86)
- [x] 1.3 Expand button over 3 lines; `editor.ts` modal that moves the same textarea; Send, Close, Esc; Ctrl+B / Ctrl+I (CLI-87)
- [x] 1.4 Draft per chat in `localStorage`; removed on send and logout (CLI-88)

## 2. Files in the chat

- [x] 2.1 `fileView` and the message views; CLI-16 test for the new kinds (CLI-16, CLI-89, CLI-90)
- [x] 2.2 Picture viewer: pinch, double tap, pan, Download, Close, Esc (CLI-89)
- [x] 2.3 Relay: `POST /api/bots/{bot id}/files/{file id}/view`, token, settings, access log (REL-57)
- [x] 2.4 Relay and Host: `GET /v/{token}` on the view host with the SEC-16 headers; host routing before sites; app CSP `frame-src`; SITE-17 yes; `view` reserved (SEC-16)
- [x] 2.5 HTML card and viewer: Fullscreen API or fixed layer with safe areas; Close, Download, Open in a new tab; frame focus (CLI-90)

## 3. Browser test

- [x] 3.1 Sample report (~1 MB, base64 font and pictures, ← → and buttons, swipe): built at test time by `tests/client-e2e/report.mjs` from the client's fonts and icons, so the repository keeps no 1 MB file
- [x] 3.2 Playwright job (latest stable) in `ci.yml` and `scripts/ci.sh` (kept equal, DEL-01): WebKit as "iPhone 15" and Chromium; turn pages by key and swipe; nothing reaches the app (CLI-90, SEC-16)

## 4. Docs and check

- [x] 4.1 `docs/UI.md` chat section (keys, growing box, editor, picture and HTML viewers); DESIGN.md section 18 (`Relay:FileViewHost`, `Relay:FileViewMinutes`); ARCHITECTURE decision (view host on the sites domain)
- [x] 4.2 After deploy: the owner opens a real sprint review report on an iPhone in Safari and turns its pages

Archived 2026-11-27 with its code merged; the open checks moved to `openspec/OWNER-CHECKS.md`.
