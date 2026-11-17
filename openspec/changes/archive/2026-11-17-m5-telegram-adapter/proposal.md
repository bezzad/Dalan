# Proposal

## Why

Milestones 1 to 4 gave Dalan its own Bot API (`/v1`), the web client and the real login. A bot written for Telegram still cannot use Dalan. Milestone 5 (docs/DESIGN.md section 19) adds the Telegram adapter: an existing Telegram bot moves to Dalan by changing only the server address and the token.

## What Changes

- **Telegram-shaped address**: `/bot<token>/<method>` at the root of the API address, GET or POST, with parameters in the query, a form or JSON, like Telegram. The token is the same one the core API uses.
- **Methods that work now**: `getMe`, `getUpdates` (update type `message`), `sendMessage` (text, `ReplyKeyboardMarkup`, `ReplyKeyboardRemove`), `deleteWebhook` and `sendChatAction` (both answer `true` and do nothing).
- **Every other method** answers a Telegram-shaped error that says "not supported by Dalan", so the bot does not crash on a strange answer.
- **Errors and limits** have Telegram's shape (`ok`, `error_code`, `description`, `retry_after`).
- **One contract for bots**: `IBotGateway` in `Dalan.Relay.Contracts`. The core Bot API and the adapter both call it, so there is one set of rules and one queue.
- **Pinned version**: Telegram Bot API 10.4 (2026-11-09), the newest on the day of building.

The adapter covers only what the core can do today. Out of scope, each waits for its core feature: formatted text (`parse_mode`, `entities`), inline buttons and `callback_query`, edit, delete, pin, commands list, files (milestone 6); webhooks (`setWebhook`, `getWebhookInfo`); "typing" shown to the user.

## Capabilities

### New Capabilities

- `telegram`: the Telegram-shaped door for bots.

### Modified Capabilities

- `relay`: bot calls go through one contract, used by both doors.

## Impact

- `Dalan.Relay.Contracts` gets its first types (`IBotGateway` and its data records). The core Bot API endpoints become thin callers of it; their behavior does not change.
- New test project `tests/Dalan.Telegram.Tests`. New test-only package: `Telegram.Bot` 22.10.4 (a real client library, pointed at the test server).
- No new setting, no database change, no new runtime package.
- The bot token is now also part of a URL path. Nothing in Dalan logs request paths; the front proxy must not log them either.
