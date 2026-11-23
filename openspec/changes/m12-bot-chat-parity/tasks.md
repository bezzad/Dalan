# Tasks

Each task: write the failing tests tagged with the rule IDs, then the code, then run them.

## 1. Keyboards (items 1 and 2)

- [ ] 1.1 Relay: keyboard buttons with `style`, the three flags, `reply_keyboard_options` and inline `style` in content, client JSON and `/v1` (REL-17, REL-20)
- [ ] 1.2 Telegram: read `style`, `is_persistent`, `resize_keyboard`, `one_time_keyboard`; return `style` in `reply_markup` (TG-08, TG-14)
- [ ] 1.3 Client: keyboard under the input, rows as sent, colours, ⌨ button, one-time and resize; ↗ on URL buttons; `--primary` token in both themes (CLI-07, CLI-13)
- [ ] 1.4 Find why @mofidbot's 5/4/3 rows show as 6/6 today and prove it fixed with ArcaneBot's real markup as a test sample

## 2. Voice (item 3)

- [ ] 2.1 Relay: `relay.files.duration`, `X-Voice-Duration`, bot `duration` (REL-28, REL-50); migration
- [ ] 2.2 Telegram: `duration` on send methods and in `voice`/`audio`/`video`; saved example `userVoice.json` (TG-20, TG-21)
- [ ] 2.3 Client: `ogg.ts` WebM-to-Ogg rewrite with a recorded sample; recorder in the input bar; the Dalan player (CLI-46, CLI-16)

## 3. Paste and drop (item 5)

- [ ] 3.1 Client: one send card for 📎, paste and drop, with preview and caption (CLI-47, CLI-16)

## 4. Bot icon (item 6)

- [ ] 4.1 Relay: icon columns, migration that moves showcase icons, `PUT/DELETE /api/builder/bots/{id}/icon`, public `GET /api/bots/{id}/icon`, `icon_version` in bot JSON, `max_bot_icon_kb` in `/api/settings` (REL-51, REL-41)
- [ ] 4.2 Telegram: `setMyProfilePhoto`, `removeMyProfilePhoto` (TG-23)
- [ ] 4.3 MCP `set_bot_icon`, CLI `dalan.mjs icon` (REL-46)
- [ ] 4.4 Client: square cut and upload; icons in header, chat list, My bots, bot page, showcase (CLI-50)

## 5. Text size (item 4)

- [ ] 5.1 Client: `--message-*` tokens and bubble sizes (CLI-48)
- [ ] 5.2 Screenshot Dalan and Telegram Desktop at the same width (Persian and Latin, light and dark, phone and desktop) and set the final values

## 6. Addresses, My bots and the bot page (items 7 and 8)

- [ ] 6.1 Client router by address only: `/chats`, `/bots`, `/bots/new`, `/bots/<username>`, `/account`, `/admin`, `/apps`; menu items; login returns to the asked address (CLI-51)
- [ ] 6.2 My bots list and empty state; builder step card for non-builders (CLI-24, CLI-23)
- [ ] 6.3 Create page with icon; bot page with link, Open, Copy, icon, rename, token, delete, status, showcase, guide tabs filled for this bot (CLI-49, CLI-38)

## 7. Docs and end checks

- [ ] 7.1 `llms.txt`: `setMyProfilePhoto`, `removeMyProfilePhoto`, `style`, keyboard options, `duration`, the icon REST call, MCP tool and CLI command; `docs/UI.md`: keyboard, voice, paste, addresses
- [ ] 7.2 `scripts/ci.sh` green on the NUC; final review of the whole diff
- [ ] 7.3 Owner: approve the deploy (merge to main is live in about 2 minutes); check on @mofidbot with ArcaneBot beside Telegram Desktop: keyboard place and rows, ➕ green, voice todo through whisper, pasted screenshot, icon, `/bots` pages, Parsa's flow
- [ ] 7.4 Archive this change
