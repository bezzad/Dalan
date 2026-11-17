# Design

## Context

Milestones 1 to 5 are merged. A bot sends text with an optional reply keyboard; a user sends text. Every bot door calls `IBotGateway` (REL-19). The client syncs a chat with "messages after the newest id I have". The owner delegated the spec review; section 18 values are used as defaults.

## Goals / Non-Goals

**Goals:**
- What docs/DESIGN.md section 19 lists for milestone 6: edit, delete, pin, inline buttons and callbacks, commands, files. In the core, the Telegram adapter and the client.
- A change made while the client is away is not lost: the sync brings it.
- Files cannot fill the disk while a home machine is off, and a file cannot run script in the site.

**Non-Goals:**
- Formatted text, webhooks, albums, caption or media edit, files from a URL, voice recording, more than one pin, a user editing or deleting messages, the history cap (last 200, 30 days).

## Decisions

1. **Versions for sync.** Every message has `version`: a per-chat counter (`memberships.last_version`), taken under the same row lock as the sequence number. A new message and every change (edit, delete, pin, unpin) take the next version. `GET /api/bots/{id}/messages?after=V` returns the messages whose version is above V, in version order. The client keeps the highest version it saw as its sync position. Sequence numbers (`id`) stay without gaps (REL-06), so `message_id` looks like Telegram's.
2. **Delete keeps a stub.** A deleted message keeps its row with `deleted` true, an empty text, no buttons and no file, and a new version, so a syncing client learns to remove it. The file itself stays until its expiry (decision 10), because another message may use the same file.
3. **One pin per chat.** Pinning a message unpins the one before. Both get a new version. Every messages answer has `pinned` (the pinned message, or null), so a client that has not loaded that old message still shows the bar. Telegram allows several pins; a bot that pins a second message sees the first one go. Simpler, and enough for the bots we know.
4. **Inline keyboard** is stored in the message content beside the reply keyboard: rows of buttons, each with `text` and exactly one of `callback_data` (1 to `MaxCallbackDataBytes` bytes) or `url` (`http` or `https`, at most `MaxButtonUrlLength`). Other Telegram button kinds get 400. Button count and text use the reply keyboard limits.
5. **Button press (callback)** is not a chat message: it is not stored and not in history. The client sends `{message_id, data}`; the server checks that this bot message of the user's chat has a button with exactly that data, so a client cannot invent data. The update id comes from the same database sequence as message ids, so offsets stay valid. It goes into the bot queue like a message (latest command wins, expiry). Not stored means a press waiting during a restart is lost; the user presses again. The same is true in Telegram when a bot is down.
6. **Answer to a press.** The server remembers a press in memory (callback id → bot, user) for `CallbackAnswerSeconds`. `answerCallbackQuery` within that time sends an `answer` event to the user's live streams, straight to the notifier, not through the outbox: it is worth nothing later. A later or unknown answer gets an error. `url` in the answer (Telegram games) gets 400.
7. **Edit.** Only bot messages, not deleted. The edit sets the text (when given) and always sets the inline keyboard (none = removed), as Telegram's `editMessageText` does. Editing the text of a file message is `no_text` (no caption edit yet). Same text and same buttons is `not_modified`. `edited_at` is set.
8. **Commands** are a jsonb list on the bot row. `setMyCommands` with a `language_code` or a `scope` other than default or all private chats answers `true` and stores nothing: Dalan shows one list to everyone, and a bot that sets a list per language must not overwrite it or crash at start. Command: 1 to 32 of `a-z`, `0-9`, `_`; description 1 to `MaxCommandDescriptionLength`; at most `MaxCommands`.
9. **Files on disk.** One flat folder, `Relay:FileFolder`, file name = file id (16 random bytes, base64url). Table `relay.files`: id, bot, user (the chat it was first sent in), name, content type, size, kind, time, `expires_at`. The message content keeps a copy of the file data. A bot may send again any file of its own (any chat); a user may download a file only through a message of their chat that is not deleted.
10. **Files expire.** `expires_at` = upload time + `FileRetentionDays`. When a user message ends "not delivered" (expiry or "latest command wins"), its file expires at once: the bot never saw it, so nobody needs it, and a user cannot fill the disk while the home machine is off. A sweep every `FileSweepMinutes` deletes expired files from disk and table. A message whose file expired still shows its name; the download answers 404 `file_not_found`.
11. **Kinds.** `photo`, `document`, `audio`, `voice`, `video`. A bot names the kind (the Telegram method names it). For a user upload the server picks it from the content type: `image/jpeg|png|gif|webp` photo, `audio/*` audio, `video/*` video, else document.
12. **Uploads.** Bot (core API): `POST /v1/messages` as `multipart/form-data` with the part `file`, or JSON or form with `file_id`. User: `POST /api/bots/{id}/files` with the raw file as the body, its type as `Content-Type`, and the headers `X-File-Name` and optional `X-Caption` (both percent-encoded). A custom header cannot come from another site's form, so SEC-05 still holds without a multipart body. Size: `MaxFileSizeMb`; a user may upload at most `UserUploadMbPerDay` in 24 hours (sum over `relay.files`). A file part with no type or `application/octet-stream` gets the type from its name.
13. **Downloads are safe in the browser.** A file is sent with its stored content type, `X-Content-Type-Options: nosniff`, `Content-Security-Policy: sandbox` and `Content-Disposition: attachment` unless the type is a safe image (`jpeg`, `png`, `gif`, `webp`), `audio/*` or `video/*`. So an HTML or SVG file never runs as a page of the site.
14. **Telegram shapes.** A photo is one `PhotoSize` (Dalan does not make smaller sizes); width, height and duration are 0 (Dalan does not read media). `file_unique_id` = `file_id`. `getFile` returns `file_path` = the file id; the download is `/file/bot<token>/<file_path>`. A file field may be an uploaded part or a `file_id`; a URL gets 400. `callback_query.id` is the update id as text; `chat_instance` is the user id as text. A sent or edited message carries `reply_markup` only for inline keyboards, and `edit_date` after an edit.
15. **Contract.** `IBotGateway` grows: `SendAsync` (text, keyboards, file), `EditAsync`, `DeleteAsync`, `PinAsync`, `UnpinAsync`, `AnswerCallbackAsync`, `SetCommandsAsync`, `GetCommandsAsync`, `GetFileAsync`, `OpenFileAsync`. Results carry one error enum `BotError`. Updates carry either a message or a callback.
16. **Live events.** `MessageCreated` becomes `MessageChanged` and carries the whole message as the client sees it. The SSE event name stays `message`; the client replaces a message with the same id. A new event `answer` carries a button answer.

## Starting values used

| Setting | Value | Source |
|---|---|---|
| `Relay:MaxFileSizeMb` | 20 | Section 18 |
| `Relay:FileRetentionDays` | 30 | Section 18 (history age) |
| `Relay:FileSweepMinutes` | 60 | Assumed |
| `Relay:UserUploadMbPerDay` | 200 | Assumed |
| `Relay:MaxCaptionLength` | 1024 | Assumed (Telegram) |
| `Relay:MaxCallbackDataBytes` | 64 | Assumed (Telegram) |
| `Relay:MaxButtonUrlLength` | 2048 | Assumed |
| `Relay:CallbackAnswerSeconds` | 120 | Assumed |
| `Relay:MaxCallbackAnswerLength` | 200 | Assumed (Telegram) |
| `Relay:MaxCommands` | 100 | Assumed (Telegram) |
| `Relay:MaxCommandDescriptionLength` | 256 | Assumed (Telegram) |
| `Relay:FileFolder` | `files` (next to the program); production `/var/lib/dalan/files` | Assumed |

## Risks / Trade-offs

- [Files on one disk] Disk full stops uploads → per-file size, per-user daily cap, expiry, and undelivered files expire at once. Object storage can replace the folder later behind the same few calls.
- [A press during a restart is lost] Rare and harmless: the user presses again.
- [One pin per chat] A bot that relies on several pins shows only the newest.
- [Width, height and duration are 0] A bot that reads them gets 0. Reading media headers is a later change if a bot needs it.
- [Message delete keeps the file until expiry] The stub shows nothing, and the client cannot reach the file through it.

## Migration Plan

One Relay migration: new columns with defaults (`version` = `seq`, `last_version` = `last_seq`), new table. Rollback: the old release ignores the new columns; files on disk stay until removed by hand.
