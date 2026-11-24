# Tasks

Each task: write the failing tests tagged with the rule IDs, then the code, then run them.

## 1. Replies (item 1)

- [x] 1.1 Relay: `ReplyTo` in content, `reply_to` on user send (JSON and `X-Reply-To`) and on `/v1` send, in client JSON, `/v1` JSON and `BotMessage` (REL-52)
- [x] 1.2 Telegram: `reply_parameters`, `reply_to_message_id`, `allow_sending_without_reply`; `reply_to_message` in updates and answers (TG-24)
- [x] 1.3 Client: Reply from the menu, double-click and swipe; reply bar; quote in the bubble with jump (CLI-52)

## 2. Press in progress (item 2)

- [x] 2.1 Relay: `press_wait_seconds` in `/api/settings` (REL-18)
- [x] 2.2 Client: pending skeleton and lock until answer, change or timeout (CLI-53)

## 3. Edits (item 3)

- [x] 3.1 Relay: `switch_query` buttons; `MessageEdit` with text, caption and entities (REL-20, REL-23)
- [x] 3.2 Telegram: switch buttons, `editMessageCaption` (TG-14, TG-16)
- [x] 3.3 Client: a switch button fills the input (CLI-13)

## 4. Install one bot (item 4)

- [x] 4.1 Relay: `/@name/manifest.webmanifest` (REL-54); host links it in `index.html` for `/@name` (CLI-09)
- [x] 4.2 Client: the chat screen links the bot's manifest (CLI-55)

## 5. Formatting (item 5)

- [x] 5.1 Relay: entities checked, stored and given out (REL-53)
- [x] 5.2 Telegram: HTML parser, `entities` and `caption_entities`, entities in answers (TG-07, TG-25)
- [x] 5.3 Client: `format.ts` draws entities (CLI-54)

## 6. Close

- [x] 6.1 `llms.txt`, `docs/UI.md`, appsettings and DESIGN.md section 18 for the two new settings
- [x] 6.2 `scripts/ci.sh` green on the NUC; final review of the whole diff; merge
- [ ] 6.3 Owner: check on @mofidbot with ArcaneBot: reply to an AI answer, press skeleton, add-todo edit, install the bot, bold text
- [ ] 6.4 Archive this change (after m12)
