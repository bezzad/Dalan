# Proposal

## Why

The owner ran ArcaneBot (a real Telegram bot) on Dalan as @mofidbot, side by side with Telegram Desktop. Users see a weaker chat on Dalan: the reply keyboard sits in the wrong place and loses its rows and colours, a user cannot send a voice or paste a picture, the text is larger than Telegram's, and a bot has no picture. Builders find their bots hard to manage: one long page mixes all bots, and every screen (account, My bots, chats) shares the `/bots` address. A new user (Parsa) pressed "My bots" after login and landed on the account page with no clear reason.

ArcaneBot needs no change: every fix is in Dalan.

## What Changes

1. **Reply keyboard under the input**, like Telegram, on phone and desktop. `is_persistent`, `resize_keyboard` and `one_time_keyboard` work as in Telegram, with a keyboard button in the input bar to show or hide it.
2. **Rows as sent, and button colours.** Each row keeps its own buttons and shares its width between them. The Bot API `style` field (`success`, `danger`, `primary`) is kept on reply and inline buttons and shown as a colour in both themes. Inline URL buttons get Telegram's small ↗ mark.
3. **Voice from the user.** A 🎤 button records a voice (tap to start and stop, cancel, length shown), as OGG/Opus when the browser can, else in the browser's own format with its real type. The bot gets `message.voice` with `duration`, readable with `getFile`. Voices both ways get a player with a play button, length and seek.
4. **Message text like Telegram Desktop**: font, size, line height, bubble padding and time size, for Persian and Latin, phone and desktop, light and dark.
5. **Paste or drop a file.** Ctrl+V of an image or a file, or drag and drop, attaches it like 📎: a preview, an optional caption, then send. The bot gets `message.photo` or `message.document`.
6. **Bot icon.** One icon per bot: set on the create page and on the bot's page (cropped to a square on the device), by the Bot API (`setMyProfilePhoto`, `removeMyProfilePhoto`), by the builder REST call, MCP and the CLI. Shown in the chat header, the chat list, the bot list and the showcase. The showcase uses the same icon.
7. **My bots: a list, then one page per bot.** `/bots` lists my bots (icon, name, `@username`, online or last seen), or an empty state with "Create a bot". `/bots/new` creates one. `/bots/<username>` is that bot's own page: link with Open and Copy, icon, rename, new token, delete, status, showcase, and the guide to run it (Telegram library, CLI, MCP) with this bot's values. The token shows only right after create or rotate.
8. **One address per screen.** `/chats` (the bots I talk to), `/bots`, `/bots/new`, `/bots/<username>`, `/account`, `/admin`, `/apps`. Back, reload and a shared link open the same screen. A user who is not a builder yet sees on `/bots` what to do next (verify the phone, ask for access, or wait), not the account page.

Out of scope: animated profile photos, custom emoji on buttons (`icon_custom_emoji_id` is accepted and ignored), `input_field_placeholder` and `selective` are accepted and ignored as before, server-side image or audio conversion, groups.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `relay`: button style and keyboard options (REL-17, REL-20), voice from the user with its length (REL-28), file length (REL-50), bot icon (REL-41, REL-51), MCP tool for the icon (REL-46).
- `telegram`: keyboard style and options (TG-08, TG-14), `duration` (TG-20, TG-21), `setMyProfilePhoto` and `removeMyProfilePhoto` (TG-23).
- `client`: keyboard place and rows (CLI-07, CLI-13), files and voice (CLI-16, CLI-46, CLI-47), message text size (CLI-48), My bots and the bot page (CLI-23, CLI-24, CLI-38, CLI-49), icons (CLI-50), addresses (CLI-51).

## Impact

- Relay: new jsonb fields in message content (no column), new columns `relay.bots.icon`, `icon_type`, `icon_version`; `relay.files.duration`. One EF migration; showcase icons move to the bot.
- Telegram adapter: `ReplyMarkup.cs`, `TelegramJson.cs`, `TelegramAdapter.cs`.
- Client: `chat.ts`, `messages.ts`, `bots.ts` (chats), `builder.ts` split into the list, create and bot pages, `guide.ts`, `main.ts` routes, `app.css`, a small WebM-to-Ogg remuxer.
- `llms.txt`, `client/public/dalan.mjs` (`icon` command), MCP tool `set_bot_icon`.
- `docs/UI.md`: keyboard, voice, paste, addresses.
- New settings: none. Existing `Relay:MaxBotIconKb` (100) now limits the bot icon.
