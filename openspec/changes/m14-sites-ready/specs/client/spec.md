# Spec Delta

## MODIFIED Requirements

### Requirement: CLI-39 My apps
The user menu SHALL open "My apps" at `/apps`. For a builder it SHALL list their sites (name, address as a link, online or offline) with a delete that asks first, and refresh the list every 10 seconds while the screen is open. It SHALL show a step-by-step guide: 1) install the agent, 2) log in with a key made here by one button (the full `dalan login` command, shown once, with Copy), 3) choose a port and a name, with a "Reserve" button that claims the name at once and says when it is taken, 4) try it with `dalan http <port> --name <name>` (it runs while the terminal is open; Ctrl+C stops it), with an example for two sites, 5) keep it online with `dalan service install`, and `dalan status` to check. Every action SHALL end in a toast. A user who is not a builder SHALL see how to become one instead.

#### Scenario: Guide commands
- **WHEN** the guide is drawn for the name `levelwise` and port `3000`
- **THEN** it shows `dalan http 3000 --name levelwise` and `dalan service install`

#### Scenario: Reserve
- **WHEN** a builder reserves the name in the guide
- **THEN** the name appears in the list, and a taken name shows its error

#### Scenario: Not a builder
- **WHEN** a user who is not a builder opens "My apps"
- **THEN** they see the way to become a builder, and no site list

### Requirement: CLI-38 Guide to run a bot
A visitor and a logged-in user SHALL see at `/guide` a choice of plan: a bot or a site. In both languages, `/guide/bot` SHALL show nine steps to run a bot without an AI agent and `/guide/site` five steps (CLI-39), with addresses and numbers from the public settings. "My bots" SHALL show steps 5 to 9 of the bot guide, numbered from 1. The token test SHALL show where the whole token goes; on "My bots" it SHALL hold the new token while that token is on screen.

#### Scenario: Steps use the real addresses
- **GIVEN** the public settings say `bot_api_url` is `https://api.example.ir`, `site_url` is `https://example.ir` and the expiry is 60 seconds
- **WHEN** the guide is made in Persian and in English
- **THEN** both have nine steps; the token test is `curl https://api.example.ir/bot12:AbCdEfGhIjKlMnOp/getMe` and its text names `bot`; the run step shows `https://example.ir/@`; the last step says 60 seconds

#### Scenario: Every library points at Dalan
- **WHEN** the library code is made for `https://api.example.ir`
- **THEN** there are seven samples (python-telegram-bot, aiogram, pyTelegramBotAPI, Telegraf, grammY, node-telegram-bot-api, Telegram.Bot), each has `https://api.example.ir` and none has `api.telegram.org`

#### Scenario: Site guide
- **GIVEN** the public settings say `site_url` is `https://example.ir`
- **WHEN** the site guide is made in Persian and in English
- **THEN** both have five steps; the install step has `curl -fsSL https://example.ir/install.sh | sh` and `irm https://example.ir/install.ps1 | iex`; the try step has `dalan http 3000 --name mysite` and says Ctrl+C stops it; the last step has `dalan service install`

#### Scenario: Which screen
- **WHEN** the address is `/guide`, `/guide/bot` or `/guide/site`, for a visitor or a logged-in user
- **THEN** both get the plan choice, the bot guide and the site guide

#### Scenario: My bots fills in the new token
- **GIVEN** "My bots" shows the new token `7:XyZ`
- **WHEN** its run steps are made
- **THEN** they are numbered from 1, the first is the token test `curl https://api.example.ir/bot7:XyZ/getMe`, and with no token shown it is the sample again
