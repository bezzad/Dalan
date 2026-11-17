# Proposal

## Why

A bot on Dalan can now only send text and reply buttons. Real bots (ArcaneBot among them) also use inline buttons, edit and delete their messages, pin one, set a command list and send files. Milestone 6 (docs/DESIGN.md section 19) adds these in the core first, then in the Telegram adapter and in the client.

## What Changes

- **Inline buttons** under a message: a button sends `callback_data` back to the bot, or opens a link. The bot gets a `callback` update and can answer it with a short text the user sees.
- **Edit** a bot message (text and inline buttons), **delete** any message of the chat, **pin** one message of the chat.
- **Sync of changes**: every message gets a `version`. A change gives the message a new version, so the client's sync after a reconnect also brings edits, deletes and pins.
- **Commands**: a bot sets its command list; the client shows it as a menu.
- **Files**: a bot sends a photo, document, audio, voice or video (upload or the id of a file it already has); a user sends a file from the client. Both sides download files they may see. Files live on the server's disk and expire.
- **Telegram adapter**: `InlineKeyboardMarkup`, `callback_query` updates, `answerCallbackQuery`, `editMessageText`, `editMessageReplyMarkup`, `deleteMessage`, `pinChatMessage`, `unpinChatMessage`, `unpinAllChatMessages`, `setMyCommands`, `getMyCommands`, `deleteMyCommands`, `sendPhoto`, `sendDocument`, `sendAudio`, `sendVoice`, `sendVideo`, `getFile` and the file download address; files in received messages.
- **Client**: inline buttons and their answers, the pinned message bar, the command menu, sending and showing files, edited and deleted messages.

Out of scope: formatted text (`parse_mode`, `entities`): it needs the core to store entities and the client to draw them, and is its own change. Also webhooks, albums, editing a caption or a file, voice recording in the client, files from a URL, more than one pinned message per chat.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `relay`: inline buttons, button presses, edit, delete, pin, versions, commands, files.
- `telegram`: the methods above.
- `live-updates`: changed messages and button answers in the stream.
- `client`: the new chat features.
- `security`: the file upload and the file download headers.

## Impact

- Database: new columns on `relay.messages` (`version`, `edited_at`, `deleted`, `pinned`) and `relay.memberships` (`last_version`), `relay.bots.commands` (jsonb), new table `relay.files`. One migration.
- Disk: files in the folder `Relay:FileFolder`. The systemd unit gets `StateDirectory=dalan` and sets the folder to `/var/lib/dalan/files`.
- New settings (all in `Relay`): `FileFolder`, `MaxFileSizeMb` 20, `FileRetentionDays` 30, `FileSweepMinutes` 60, `UserUploadMbPerDay` 200, `MaxCaptionLength` 1024, `MaxCallbackDataBytes` 64, `MaxButtonUrlLength` 2048, `CallbackAnswerSeconds` 120, `MaxCallbackAnswerLength` 200, `MaxCommands` 100, `MaxCommandDescriptionLength` 256.
- The live event `MessageCreated` becomes `MessageChanged`. Nothing is deployed yet, so no old outbox rows exist.
- No new package.
