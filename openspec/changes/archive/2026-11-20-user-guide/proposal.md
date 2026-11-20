# Proposal

## Why

The owner (2026-11-20): can a normal user get a bot running from the site alone, without an AI agent? Not yet. The landing page says "change one line", but not which line in each library; after a bot is created, the screen does not say what to do next; and nothing says that a builder request waits for the admin.

## What Changes

- A public page `/guide`: the steps from login to a bot that keeps running, with the address from the public settings, ready code for the common bot libraries (python-telegram-bot, aiogram, pyTelegramBotAPI, Telegraf, grammY, node-telegram-bot-api, Telegram.Bot), a token test with `getMe`, a Linux user service, and what to do when it does not work.
- Links to it: the site header, the footer, the landing steps, and the token card in "My bots".

## Capabilities

### Modified Capabilities

- `client`: CLI-01 (one more page address), CLI-26 (the screen for `/guide`), new CLI-38.

## Impact

Client, and one address in the host. No new setting.
