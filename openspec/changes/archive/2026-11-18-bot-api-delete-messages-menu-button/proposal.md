# Proposal

## Why

ArcaneBot is the first real bot for Dalan (the shutdown drill in m11 uses it). It calls two methods Dalan does not serve: `deleteMessages` (to clear old menus) and `setChatMenuButton` (to show the commands list). Both get the TG-09 400 today, so the bot cannot run unchanged.

## What Changes

- `deleteMessages` (`chat_id`, `message_ids`): deletes each message it finds, skips the rest, returns `true`, as Telegram does.
- `setChatMenuButton`: `commands` and `default` answer `true` and do nothing (Dalan's chat already offers the bot's commands). `web_app` gets a 400, because Dalan has no Mini Apps yet.

## Capabilities

### Modified Capabilities

- `telegram`: TG-09, TG-17.

## Impact

- `src/Plugins/Dalan.Telegram/TelegramAdapter.cs`, tests in `tests/Dalan.Telegram.Tests`.
- No database change, no new setting. The 1 to 100 ids of `deleteMessages` is Telegram's own limit, part of the method's shape.
