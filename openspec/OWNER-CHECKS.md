# Owner checks

The merged milestones were archived on 2026-11-27, so their rules are in `openspec/specs`. What is left is checks on
dalanet.ir that only the owner can do, and work that is not specced yet. Tick a line when it is done; a check that
finds a bug becomes a new change.

## On dalanet.ir
- [ ] m11: drill from Iranian mobile data with no VPN: SMS login, ArcaneBot chat and buttons; fix what it finds
- [ ] m12: @mofidbot with ArcaneBot beside Telegram Desktop: keyboard place and rows, ➕ green, voice todo through whisper, pasted screenshot, icon, `/bots` pages, Parsa's flow
- [ ] m12: screenshot Dalan and Telegram Desktop at the same width (Persian and Latin, light and dark, phone and desktop); set the final `--message-*` values (CLI-48)
- [ ] m13: reply to an AI answer, press skeleton, add-todo edit, install the bot, bold text
- [ ] m19: `journalctl -u dalan` shows `Stopping` to `Stopped` within 5 s; Caddy has no `connection refused` burst
- [ ] m20: the footer shows `v1.1.<n>` of the live release (fixed by #104)
- [ ] m21: install @mofidbot in Chrome (Install and Create shortcut) and on a phone; the app shows the bot's icon (it must be square, 192 or more; a dashboard icon always is)
- [ ] m22: in @mofidbot type `/`, then `/tic`; Enter sends, Tab completes
- [ ] m23: open a chat, turn the network off and on; the ring turns round the icon and goes, the tooltip changes, nothing moves (ring made a true circle in #105)
- [ ] m24: Persian and English, desktop and 360px phone: labels read clearly; the diagrams fit
- [ ] m25 and m26: every settings section, Persian and English, light and dark, desktop and 360px phone; the keys show in Access keys
- [ ] m28 and m33: on a phone, light and dark: the microphone icon and the offline bar
- [ ] m29: the dashboard as admin and as a common user, phone and desktop
- [ ] m32: a real sprint review report on an iPhone in Safari; turn its pages

## Sites (m15)
- [ ] `dalan service install` on real systems: Linux (systemd user unit), macOS (launchd), Windows (Run key, `reg add` quoting)
- [ ] Put `dalans.ir` on the Public Suffix List; decide on a wildcard certificate (Let's Encrypt gives about 50 new names a week)
- [ ] Mysite side: build with `PUBLIC_POCKETBASE_URL=https://mysite.dalans.ir`; trust `X-Forwarded-For` in PocketBase; the site address in OAuth redirects, Turnstile hostnames and `--origins`; about 6 files at a time in `src/sw.js`
- Known limits: a streamed answer has no `Content-Length`; Windows does not restart the agent after a crash
- Ideas approved, not specced: static upload and custom domain, signed user header, inspector, Basic Auth and IP allowlist
