## MODIFIED Requirements

### Requirement: CLI-01 The server serves the client
The server SHALL serve the PWA page at `/`, `/bots`, `/privacy`, `/faq`, `/showcase`, `/guide` and `/@<bot username>`, and its files (scripts, styles, font, icons) from the same origin. The page SHALL be sent with `Cache-Control: no-cache`, so a new release is seen at the next open. Any other unknown path SHALL get 404.

#### Scenario: Open the site
- **WHEN** a browser asks for `/`
- **THEN** the response is 200 HTML with `Cache-Control: no-cache`

#### Scenario: Open a bot link
- **WHEN** a browser asks for `/@ArcaneBot`
- **THEN** the response is the same page

#### Scenario: Open a public page
- **WHEN** a browser asks for `/privacy`, `/faq`, `/showcase` or `/guide`
- **THEN** the response is the same page

#### Scenario: Unknown path
- **WHEN** a browser asks for `/nothing-here`
- **THEN** the response is 404

## ADDED Requirements

### Requirement: CLI-38 Guide to run a bot
A visitor and a logged-in user SHALL see at `/guide`, in both languages, the steps to run a bot without an AI agent: log in, verify the mobile number, ask to become a builder, create the bot and copy the token, test it with `getMe`, change the server address in the code, run it and share the link, keep it running, and what to do when it fails. Addresses and numbers SHALL come from the public settings. The header, footer, landing steps and token card SHALL link to it.

#### Scenario: Steps use the real addresses
- **GIVEN** the public settings say `bot_api_url` is `https://api.example.ir`, `site_url` is `https://example.ir` and the expiry is 60 seconds
- **WHEN** the guide is made in Persian and in English
- **THEN** both have nine steps; the token test is `curl https://api.example.ir/bot<token>/getMe`; the run step shows `https://example.ir/@`; the last step says 60 seconds

#### Scenario: Every library points at Dalan
- **WHEN** the library code is made for `https://api.example.ir`
- **THEN** there are seven samples (python-telegram-bot, aiogram, pyTelegramBotAPI, Telegraf, grammY, node-telegram-bot-api, Telegram.Bot), each has `https://api.example.ir` and none has `api.telegram.org`

#### Scenario: Which screen
- **WHEN** the address is `/guide`, for a visitor or a logged-in user
- **THEN** both get the guide
