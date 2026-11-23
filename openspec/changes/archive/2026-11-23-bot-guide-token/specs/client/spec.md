## MODIFIED Requirements

### Requirement: CLI-38 Guide to run a bot
A visitor and a logged-in user SHALL see at `/guide` a choice of plan: a bot or a site. `/guide/bot` SHALL show, in both languages, nine steps to run a bot without an AI agent. `/guide/site` SHALL show four steps: become a builder, install the agent in one line (CLI-39), log in from "My apps", run `dalan http`. Addresses and numbers SHALL come from the public settings. "My bots" SHALL show steps 5 to 9 of the bot guide itself, numbered from 1. The token test SHALL show a sample token and say to put the whole token right after `bot`, with no space, slash or `<>`. While "My bots" shows a new token, its token test SHALL have that token in it; once the token is closed, the sample comes back.

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
- **THEN** both have four steps; the install step has `curl -fsSL https://example.ir/install.sh | sh` and `irm https://example.ir/install.ps1 | iex`; the last step has `dalan http 3000 --name mysite`

#### Scenario: Which screen
- **WHEN** the address is `/guide`, `/guide/bot` or `/guide/site`, for a visitor or a logged-in user
- **THEN** both get the plan choice, the bot guide and the site guide

#### Scenario: My bots fills in the new token
- **GIVEN** "My bots" shows the new token `7:XyZ`
- **WHEN** its run steps are made
- **THEN** they are numbered from 1, the first is the token test `curl https://api.example.ir/bot7:XyZ/getMe`, and with no token shown it is the sample again

### Requirement: CLI-43 Natural, formal Persian
Every Persian text the client shows SHALL be a little formal and use the common Persian word, never a literal translation: «بات» (not «ربات»: Dalan has bots, not robots), «اتصال بات» (not «رله»), «برنامه» (not «اپ»), «برنامه‌ی رابط دالان» (not «ایجنت»), «توسعه‌دهنده» for the builder role, «مدیر» (not «ادمین»), «رمز عبور», «جدید» (not «تازه»), «طرح‌ها» (not «پلن»), «گزارش» (not «لاگ»), «پوسته» (not «تم»), «پرسش‌های متداول» and «گفت‌وگو». «می‌» and «ها» SHALL be joined with a zero-width non-joiner.

#### Scenario: No literal words
- **WHEN** every Persian text of the client (texts, landing, privacy, FAQ, both guides, the ready prompt) is read
- **THEN** none has «ربات», «رله», «اپ» as a word, «ایجنت», «ادمین», «پلن», «لاگ», «تم:», «گواه کار», «محدودیت سرعت», «سوال‌های رایج» or «گفتگو»

#### Scenario: Joined prefixes
- **WHEN** every Persian text of the client is read
- **THEN** no «می» stands alone before a space
