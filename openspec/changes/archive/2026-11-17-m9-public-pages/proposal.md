# Proposal

## Why

Today a visitor who opens `dalanet.ir` sees only a login form. Nobody can learn what Dalan is, what it keeps, or which bots exist. DESIGN section 13 asks for a landing page, a privacy page and an FAQ that say six things plainly, and an opt-in showcase that the admin reviews. Milestone 9 (DESIGN section 19) builds them.

The privacy page must say how many messages per chat are kept and for how long. That rule (DESIGN section 10, values in section 18) is not built yet, so this milestone builds it too, so the page tells the truth.

## What Changes

- **Public pages, no login**: `/` shows the landing page to a visitor (a logged-in user still gets the bot list), `/privacy`, `/faq` and `/showcase`. Persian first, English second, like the rest of the client.
- **Numbers from settings**: `GET /api/public/settings` (no login) gives the numbers the pages print: history kept, access log kept, file and update expiry.
- **Showcase**:
  - A builder asks to list a bot with a description, a category and an icon. Every change goes back to review.
  - The admin approves or refuses each request. Only approved, not suspended bots are listed.
  - `GET /api/showcase` and the icon are public. "Open" goes to the bot's chat (after login).
  - The builder can take the bot out of the showcase at any time.
- **History kept per chat**: a sweeper deletes messages older than `Relay:HistoryDays` or beyond the newest `Relay:HistoryMessages` of each chat.
- **Client**: landing, privacy, FAQ and showcase pages; showcase form in "My bots"; showcase requests tab in the admin screen.

Out of scope: search in the showcase, ratings, more than one icon size, a showcase page per bot, server-side rendered pages for search engines, a contact form, the lawyer's review of the texts.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `relay`: showcase entries, public showcase, history kept per chat.
- `admin`: showcase review, public numbers.
- `client`: public pages, showcase form and review tab; CLI-01 serves the new paths.

## Impact

- Database: `relay.showcase` (one row per bot that asked). One migration.
- `IBotAdmin` (Relay.Contracts) gets two methods for the review.
- New settings: listed in design.md.
