# Design

## Context

Milestones 1 to 8 are merged. The client is one plain-TypeScript PWA with a screen per state; the host serves its page only at `/` and `/@name`. Builders create bots; the admin door is in the Admin service and calls Relay through `IBotAdmin`. Messages are kept for ever today. The owner delegated the spec review; section 18 values are used as defaults.

## Goals / Non-Goals

**Goals:**
- DESIGN section 19, milestone 9: landing, privacy, FAQ, opt-in showcase.
- The pages say the six things of DESIGN section 13, with numbers that are true now (rule 4: no number in the text).
- Nothing in the showcase that the builder did not ask for and the admin did not approve.

**Non-Goals:**
- Showcase search, ratings, per-bot pages, server-rendered pages, a legal text checked by a lawyer.

## Decisions

1. **Pages live in the PWA.** The texts are in `client/src` in both languages, next to the other texts, and are drawn like the other screens. The host serves the same page at `/privacy`, `/faq` and `/showcase` (CLI-01). A visitor at `/` gets the landing page; "Enter" opens the login screen; after login the address decides the screen as before. One page, one style sheet, no second build. The cost: a search engine sees little without JavaScript. Fine for now.
2. **Public numbers in Admin.** `GET /api/public/settings` lives in the Admin service, because Admin is the service that knows every setting. It reads a fixed list of keys from the configuration (so a panel change shows at once) and needs no login. It answers `history_messages`, `history_days`, `access_log_days`, `file_days`, `update_expiry_seconds`.
3. **Public endpoints know no caller.** `/api/public/settings`, `/api/showcase` and the icon need no session and write no access log row: they know no user or bot (rule 13 covers doors that know their caller). The per-address limit (SEC-02) holds, as for every endpoint.
4. **Showcase data in Relay.** `relay.showcase`: `bot_id` (key, and a foreign key to `relay.bots`), `description`, `category`, `icon` (bytes, may be empty), `icon_type`, `state` (`pending`, `listed`, `refused`), `updated_at`, `reviewed_at`. Relay owns bots, so it owns their entries. The bot's name and username come from `relay.bots`, so a listed bot always shows its current name.
5. **Every builder change goes back to review.** Changing the text, the category or the icon sets `pending`, and a pending entry is not listed. Simple and safe: an approved bot cannot quietly change its words. Leaving the showcase deletes the row at once, with no review.
6. **Icon.** Sent alone as the raw body (`PUT .../showcase/icon`), at most `Relay:MaxBotIconKb`. Only PNG, JPEG or WebP, checked by the first bytes, never by the name or the header; never SVG, which can hold a script. Kept in the row (it is small) and served with its own type, `nosniff` and the site's content security policy. No icon: the client draws the first letter of the name.
7. **Categories are a setting.** `Relay:ShowcaseCategories` is a comma list of short keys (start `games,tools,education,news,shopping,other`). The client has Persian and English names for the start keys and shows any other key as it is.
8. **Review.** `GET /api/admin/showcase` lists pending entries, oldest first; `POST /api/admin/showcase/{botId}` with `{"approve": true|false}` lists or refuses. Both go through `IBotAdmin` and are in the audit log (`showcase.approve`, `showcase.refuse`). A builder's request is in the audit log too (`showcase.request`, `showcase.leave`). The admin sees a pending icon through the builder icon address, which allows admins (like token rotation).
9. **Public list.** `GET /api/showcase?category=<key>&after=<bot id>` gives listed, not suspended bots in rising bot id order, `Relay:ShowcasePageSize` per page, with the categories. Each entry: `id`, `username`, `name`, `description`, `category`, `icon` (true or false). The icon: `GET /api/showcase/{botId}/icon`, 404 unless the bot is listed and not suspended.
10. **History kept per chat.** A background sweeper runs every `Relay:HistorySweepMinutes`. It deletes, in small batches, a chat's messages older than `Relay:HistoryDays`, and those with `seq` at or below the chat's `last_seq` minus `Relay:HistoryMessages`. It never deletes a user message that still waits for the bot (`delivery = pending`): those expire by Q-02 first. Sequence numbers and versions keep counting, so sync (REL-16) is not changed: a deleted message just never comes again. A file a deleted message points to still expires by REL-30.

## Starting values used

| Setting | Value | Source |
|---|---|---|
| `Relay:HistoryMessages` | 200 | DESIGN section 18 (assumed) |
| `Relay:HistoryDays` | 30 | DESIGN section 18 (assumed) |
| `Relay:HistorySweepMinutes` | 60 | Assumed |
| `Relay:MaxBotDescriptionLength` | 512 | Assumed (Telegram's bot description limit) |
| `Relay:MaxBotIconKb` | 100 | Assumed |
| `Relay:ShowcaseCategories` | `games,tools,education,news,shopping,other` | Assumed |
| `Relay:ShowcasePageSize` | 50 | Assumed |

## Risks / Trade-offs

- [The texts are not checked by a lawyer] DESIGN section 13 asks for it before a public launch. They are plain and honest; the owner can change them in `client/src/pages.ts`.
- [History is deleted] It is the designed rule. The admin can raise both numbers; 2147483647 means "keep".
- [Icons in the database] Small (at most 100 KB) and few; a file store would add a second place to clean.
