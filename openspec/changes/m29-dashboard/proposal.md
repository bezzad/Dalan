# Proposal

## Why

The admin panel (`/admin`) is a row of tab buttons and, under Settings, every setting as a raw key (`AccessLogIntervalMinutes`, `BotApiUrl`, …) with its own green "Save", one under another (owner, 2026-11-25, screenshot). Nothing says what a value means, its unit or its safe range. The owner wants one dashboard like ngrok's: a side menu in groups that holds every signed-in page; common users see only their own pages; admins also see the admin group.

## What Changes

1. **One dashboard (CLI-81).** Every signed-in page lives in one frame with a side menu (the frame and pieces of m26), at `dalanet.ir/dashboard/...` (see design.md, decision 1, for why not `dashboard.dalanet.ir`):
   - Workspace: Chats, My bots, My apps (builder and site pages only for a builder or an admin).
   - Account: the settings sections of m26 (Profile, Login and security, Notifications, Preferences, Access keys).
   - Admin, only for an admin: Overview, Requests, Users, Bots, Showcase, Messages, Settings, Access log, Audit log.
   - The user menu's "Settings" and "Admin" open the dashboard at that place. The bot chat page (`/@username`) stays full screen, outside the frame.
   - The old addresses (`/account/*`, `/bots`, `/apps`, `/admin`) move to the new ones with a redirect.
2. **Admin overview (CLI-82).** The first admin page: cards with counts that link to their page: users (new this week), bots (online now), builder and showcase requests waiting, unread support messages, sites online.
3. **Settings page (CLI-83, ADM-19).** Settings grouped by service (Identity, Relay, Notification, Sites, Admin), each as a setting row (m26): a readable name and a help line in the page language, the control by kind (a number with its unit and allowed range, a switch, a text), a "Changed" badge with the start value and "Reset", a "Needs restart" badge, one Save that turns primary only for a changed row. A search box filters by name, key or help. A setting outside its range is refused by the server (400 `out_of_range`).
4. **Server checks stay the gate (SEC-14).** Hiding the Admin group is only for the eyes. Every admin call keeps its server check (`AdminFilter`), and every admin page asks the server first: a common user who types an admin address gets "Not found", the same as a missing page, and the try is in the access log.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: new CLI-81, CLI-82, CLI-83.
- `admin`: new ADM-19.
- `security`: new SEC-14.

## Impact

- Client: a `dashboard/` folder (frame, menu, routes), the admin screens moved into it and split per page, `admin.ts` settings rebuilt, `i18n.ts`, `app.css`, `docs/UI.md`.
- Admin: a settings description file (`settings.json` embedded: name and help in both languages, unit, min, max, group, risky) read by `SettingsCatalog`; `/api/admin/settings` returns them; range check on save; `/api/admin/overview` (counts).
- Host: `/dashboard/*` serves the page; redirects from the old addresses.
- Depends on m26 (the frame and pieces) and m25 (keys in the account).
- No new admin settings.
