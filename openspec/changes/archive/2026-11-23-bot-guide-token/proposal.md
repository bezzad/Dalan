# Proposal

## Why

The owner (2026-11-23), after making a bot on dalanet.ir:
- The token test `curl https://api.dalanet.ir/bot<token>/getMe` is not clear: a reader does not see that the whole `bot<token>` part holds the token, and copied the token with `/getMe` at its end.
- "My bots" starts its steps at 5. A reader looks for steps 1 to 4.
- Persian should say «بات», not «ربات»: Dalan has bots, not robots.

The URL stays `/bot<token>/<method>`: every Telegram library builds this shape by itself, and "change only the base address" is the promise of the Telegram adapter.

## What Changes

- CLI-38: the bot guide's token test shows a real-looking sample token and says to put the whole token right after `bot`, with no space, slash or `<>`. "My bots" shows the guide's run steps numbered from 1. While a new token is on screen, the test line there has that real token in it, ready to copy; once the token is closed, the sample comes back.
- CLI-43: «بات» replaces «ربات» (also «اتصال بات», «بات‌های من»); the test keeps «ربات» out.
- docs/UI.md: the glossary word.

## Capabilities

### Modified Capabilities

- `client`: CLI-38, CLI-43.

## Impact

Client texts and the "My bots" screen only. No server, setting or package change.
