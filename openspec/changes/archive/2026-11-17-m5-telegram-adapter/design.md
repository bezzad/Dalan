# Design

## Context

Milestones 1 to 4 are merged. The core Bot API has two calls: `GET /v1/updates` and `POST /v1/messages` (text and a reply keyboard). `Dalan.Telegram` is an empty plugin that may reference only `Dalan.Relay.Contracts` (ARC-04), and that project is empty too. Decisions already made: `docs/DESIGN.md` section 7 and `docs/ARCHITECTURE.md` section 2. The owner delegated the spec review.

## Goals / Non-Goals

**Goals:**
- A stock Telegram library (C#, Python, JavaScript) talks to Dalan with only the base address and the token changed.
- One implementation of the bot rules (token, limit, queue, send), used by both doors.
- Every call the adapter cannot serve gets a clear Telegram-shaped error.

**Non-Goals:**
- No feature the core does not have. Formatted text, inline buttons, edit, delete, pin, commands and files come with milestone 6, in the core first and then in the adapter.
- No webhooks. Long polling is the main way for a home machine; webhooks come as their own change.
- No list of Telegram's method names. Anything unknown gets the same "not supported" answer.

## Decisions

1. **Versions (checked 2026-11-17):** Telegram Bot API 10.4 (released 2026-11-09) is the snapshot. Test package `Telegram.Bot` 22.10.4 (it targets Bot API 10.4).
2. **Contract `IBotGateway`** (in `Dalan.Relay.Contracts`): `EnterAsync(token)` (token check, then the limit per bot; returns the bot's id, username and name), `GetUpdatesAsync`, `SendMessageAsync`. Relay implements it once. The core endpoints and the adapter are both thin callers. This keeps ARC-03 and ARC-04: Relay knows nothing of Telegram, the plugin knows nothing of Relay's inside.
3. **Address:** `/bot<token>/<method>` at the root, as Telegram has it; GET and POST; method name without regard to letter case. `docs/DESIGN.md` section 18 assumed a prefix (`/tg/bot<token>/...`). It is dropped: the most used .NET library, `Telegram.Bot` 22.10.4, keeps only the scheme and host of its base address (checked: base `https://api.dalanet.ir/tg` calls `https://api.dalanet.ir/bot<token>/getMe`). At the root every library works. No other route of Dalan starts with `/bot`, so nothing clashes.
4. **Parameters** are read the way Telegram reads them: query string, `application/x-www-form-urlencoded`, `multipart/form-data` and JSON. A number may come as a string. `reply_markup` may be a JSON object or a string with JSON in it.
5. **Ids:** `update_id` is the id of the stored message (decision 11 of the architecture). `message_id` is the sequence number in the chat. `chat.id` is the user's id, as in a Telegram private chat. `date` is Unix seconds.
6. **Commands:** many libraries find commands only through `entities`. The adapter adds a `bot_command` entity for each `/name` or `/name@bot` that starts the text or follows white space. No other entity is made for user text.
7. **`sendMessage`** needs `chat_id` (a number) and `text`. The answer `Message` has `chat` with only `id` and `type`; the user's name would cost one more lookup and no library needs it. Unknown parameters are ignored, as Telegram does.
8. **`parse_mode` and `entities` are ignored for now**: the text is shown as the bot sent it, so a bot that sends HTML shows its tags. Stripping the markup would lose link addresses, and refusing the call would stop most bots. Real formatting needs the core to store entities and the client to draw them: milestone 6.
9. **Reply markup:** `keyboard` becomes the core reply keyboard (a button is a string or an object with `text`); `remove_keyboard` becomes an empty keyboard. Look-only fields (`resize_keyboard`, `one_time_keyboard`, `is_persistent`, `input_field_placeholder`, `selective`) are ignored. A button that asks for something Dalan cannot give (`request_contact`, `request_location`, other `request_...`, `web_app`) and any other kind of markup (`inline_keyboard`, `force_reply`) get a 400, so a bot never waits for an answer that cannot come.
10. **`getUpdates`:** `offset`, `limit` and `timeout` follow the queue rules (Q-03, Q-06, Q-08). A negative `offset` counts as 0: Dalan keeps an update for about a minute, so there is no backlog to skip. A `limit` under 1 counts as 1. `allowed_updates` is ignored.
11. **Calls with nothing to do:** `deleteWebhook` answers `true` (there is no webhook; libraries call it before they start polling). `sendChatAction` answers `true` and shows nothing yet.
12. **Errors:** HTTP status = `error_code`. Texts follow Telegram's where bots match on them: `Unauthorized`, `Conflict: terminated by other getUpdates request; make sure that only one bot instance is running`, `Bad Request: message text is empty`, `Bad Request: message is too long`, `Bad Request: chat not found`, `Too Many Requests: retry after N` with `parameters.retry_after`. A user who has not started the bot: 403 `Forbidden: bot can't initiate conversation with a user`. A method the adapter does not have: 400 `Bad Request: method <name> is not supported by Dalan` (not 404: some libraries read 404 as "wrong token").
13. **JSON:** snake_case, empty fields left out, Persian text not escaped.
14. **Access log (ADM-01):** `IBotGateway.EnterAsync` takes the address and port of the connection and records the call after the token check, before the limit. So every bot door is in the log by construction, and no door reads `X-Forwarded-For` (CLAUDE.md rule 13).
15. **Tests:** a new project `tests/Dalan.Telegram.Tests`. Answers are compared with saved JSON files (`Examples/`). One test drives the real `Telegram.Bot` client against the test server, so a wrong shape fails in a real parser.

## Starting values used

No new setting. The adapter uses the Relay settings already there: `Relay:PollTimeoutDefaultSeconds`, `Relay:PollTimeoutMaxSeconds`, `Relay:MaxUpdatesPerPoll`, `Relay:MaxTextLength`, `Relay:MaxKeyboardButtons`, `Relay:MaxButtonTextLength`, `Relay:BotRequestLimit`, `Relay:RateLimitWindowSeconds`.

## Risks / Trade-offs

- [`update_id` is 64-bit and shared by all bots; some libraries keep it in 32 bits] It breaks after about 2 billion stored user messages → far away at the planned load; a per-bot counter can replace it later without a change for bots.
- [Telegram routes sit at the root of the API host] A later core route that starts with `/bot` would clash → core routes stay under `/v1` and `/api`.
- [The token is part of the URL] A proxy or log that keeps paths would keep tokens → Dalan logs no request path (TG-10 tests it); the Caddyfile has no access log. Keep it so.
- [Formatting is ignored] A bot that sends HTML or Markdown shows its markup until milestone 6.
- [ArcaneBot was not run against Dalan here] Its code is not in this repository → the real `Telegram.Bot` client test stands in; the owner points ArcaneBot at `https://api.dalanet.ir` to confirm.
- [The limit per client address answers in Dalan's shape, not Telegram's] It is a safety net in front of everything (SEC-02) → a bot under the per-bot limit never sees it.

## Migration Plan

No database change and no new setting. Rollback: the old release has no `/bot<token>/...` address.
