# Proposal

## Why

The account screen (`/account`) is one page of loose cards: username, password, phone, email, GitHub, notifications and builder, side by side in a grid, each with its own green "Save". Nothing is grouped, the order has no meaning, and the user menu is a plain list of words (owner, 2026-11-25, screenshot: "UI/UX so bad"). The owner points to ngrok's dashboard as the model: a settings area with a side menu in groups, one section per page with a title and a one-line description, rows of "what it is" on one side and "the control" on the other, login methods as cards with a status badge, a danger zone, and a user menu with icons in groups.

## What Changes

1. **Settings area (CLI-77).** `/account` becomes "Settings" with a side menu and one page per section, each at its own address (CLI-51):
   - Account: **Profile** (`/account/profile`: username, phone, email), **Login and security** (`/account/security`: login methods, merge), **Notifications** (`/account/notifications`), **Preferences** (`/account/preferences`: theme and language).
   - Building (only for an approved builder or an admin): **Builder** (`/account/builder`: builder state, link to My bots and My apps), **Access keys** (`/account/keys`: CLI-30 after m25).
   - `/account` opens Profile on a desktop and the menu on a phone.
2. **Section page.** A breadcrumb (`Settings › Profile`), the title, a one-line description, then rows split by thin lines: start side the row title and a short help text, end side the control and its button. A row's button is plain; it turns primary only when its value changed.
3. **Login methods** as one card per way (Password, Phone (SMS), Email, GitHub): icon, name, a badge "Connected" (green) or "Not connected" (grey), what it holds (masked phone, email, GitHub name), and one action ("Set up", "Change", "Link"). The add and link flows are the ones of CLI-34 and CLI-70; the merge card (CLI-70) shows on this page.
4. **Preferences.** Theme as two radio cards (Light, Dark) with a small picture of each; language as two radio cards (فارسی, English). Both apply at once, as the header buttons do (CLI-35, CLI-04).
5. **Phone (below 760px).** `/account` is the menu as a list of 48px rows (icon, name, ›); a section page has "‹ Settings" at the start of its title row. Rows stack: title and help above, control below.
6. **User menu (CLI-78).** The panel opens with the user's name and, in grey, the phone or email. Then groups with an icon per item and a line between groups: (My bots, Chats, My apps), (Settings, Admin for an admin), (Log out).
7. **Design system.** `docs/UI.md` gets a "Settings" part and the new pieces (side menu, breadcrumb, setting row, status badge, radio card, danger card, empty state, grouped menu); see design.md.

Out of scope: new account features (sessions, deleting an account, a second factor): change m27-account-ideas.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: new CLI-77, CLI-78.

## Impact

- Client: `account.ts` split into one file per section under `client/src/settings/`, `main.ts` (routes, user menu), `icon.ts` (new icons), `i18n.ts` (titles and descriptions), `app.css`, `docs/UI.md`.
- m25 (`/account#keys`) becomes `/account/keys`; the links in My bots and My apps follow.
- Host: `/account/*` serves the page.
- No server API change, no new settings.
