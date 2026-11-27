# Proposal

## Why

The owner ran ArcaneBot on Dalan (@mofidbot) beside Telegram and found five gaps (2026-11-24):

1. **No reply.** A user cannot reply to a message, and a bot cannot reply either. ArcaneBot's AI chat needs it: a reply to an answer picks the context or session it continues (`reply_to_message`).
2. **A press shows nothing.** After a tap on an inline button nothing moves until the bot answers, so users tap again and again.
3. **Messages stay in their first state.** ArcaneBot's log: `editMessageText: 400 Bad Request: inline keyboard button field switch_inline_query_current_chat is not supported by Dalan`. One button kind fails the whole edit, so the message never changes. `editMessageCaption` is missing too.
4. **Install opens the home page.** The one manifest has `start_url` "/". A user who installs a bot's chat gets Dalan's home, not the bot.
5. **Raw HTML.** `parse_mode` is ignored, so ArcaneBot's `<b>…</b>` shows as text.

## What Changes

1. **Reply (REL-52, TG-24, CLI-52).** A user replies by right-click or long-press → Reply, double-click, or a swipe. A bar above the input shows the quote with ✕. The bot gets `reply_to_message`. A bot replies with `reply_parameters` or `reply_to_message_id` (Telegram) or `reply_to` (`/v1`). The bubble shows the quote; a click on it goes to that message.
2. **Press in progress (CLI-53, REL-18).** The tapped button shows a moving skeleton and the buttons of that message are locked until the bot answers the press, changes the message, or `Relay:ClientPressWaitSeconds` (15) pass.
3. **Edits (REL-20, REL-23, TG-14, TG-16, CLI-13).** `switch_inline_query`, `switch_inline_query_current_chat` and `switch_inline_query_chosen_chat` buttons are accepted: a tap puts `@bot query` in the input, as Telegram does in the current chat. `editMessageCaption` is added; `/v1` edits a caption with `text`.
4. **Install one bot (REL-54, CLI-09, CLI-55).** `/@name/manifest.webmanifest`: the bot's name, icon, start address and scope `/@name`, its own `id`. The bot's chat page links it, so Install there installs that bot and opens its chat.
5. **Formatting (REL-53, TG-07, TG-25, CLI-54).** Bot messages carry Telegram `entities` (bold, italic, underline, strikethrough, spoiler, code, pre, text link, blockquote). `parse_mode` "HTML" is turned into entities; `entities` and `caption_entities` are taken as given. The client draws them, never as HTML.

Out of scope: `parse_mode` "MarkdownV2" and "Markdown" (the text stays as sent, as today), custom emoji, links found in plain text, quotes of part of a message (`reply_parameters.quote`), replies to messages of other chats, inline mode itself.

## Capabilities

### Modified Capabilities

- `relay`: REL-18, REL-20, REL-23; new REL-52, REL-53, REL-54.
- `telegram`: TG-07, TG-14, TG-16; new TG-24, TG-25.
- `client`: CLI-09, CLI-13; new CLI-52, CLI-53, CLI-54, CLI-55.

## Impact

- Relay: message content jsonb gets `ReplyTo` and `Entities` (no column, no migration). Contracts: `TextEntity`, `MessageReply`; `OutgoingMessage`, `BotMessage` get `ReplyTo` and `Entities`; `InlineButton` gets `SwitchQuery`; `IBotGateway.EditAsync` takes a `MessageEdit`.
- Telegram adapter: HTML parser, reply parameters, `editMessageCaption`, switch buttons.
- Host: `index.html` for `/@name` links the bot's manifest.
- Client: `chat.ts`, `messages.ts`, new `format.ts`, `app.css`, `i18n.ts`.
- `llms.txt`, `docs/UI.md`.
- New settings: `Relay:ClientPressWaitSeconds` 15, `Relay:MaxTextEntities` 100.
