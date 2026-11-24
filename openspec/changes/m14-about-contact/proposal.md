# Proposal

## Why

Enamad (the trust badge for Iranian sites) asks for an "About us" and a "Contact us" page before it gives the license (owner, 2026-11-24). Dalan has neither, and a user has no way to send us a question.

## What Changes

1. **About us (CLI-56).** `/about` says what Dalan does, in Persian when Persian is on and in English when English is on. No owner name.
2. **Contact us (CLI-57).** `/contact` has an email field and a text box. A visitor must type an email, so we can answer. A logged-in user with an email sees it filled in and locked; one with no email types it.
3. **Messages for the admin (ADM-17).** `POST /api/contact` keeps the message with the email, the user id (when logged in), the address, port and time. ADM-18: the admin screen gets a "Messages" tab: newest first, with "read".
4. Both pages are linked in the footer.

Out of scope: answering from the panel (the admin writes to the email), files in a message, an email to the admin for each message, address and phone on the page.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `admin`: new ADM-17, ADM-18.
- `client`: new CLI-56, CLI-57.

## Impact

- Admin: table `admin.contact_messages` (migration), `/api/contact`, `/api/admin/contact`, `contact_max_length` in `/api/public/settings`.
- Identity contracts: `IUserAdmin.FindAsync`, to read the user's own email.
- Host: `/about` and `/contact` serve the page.
- Client: `pages.ts`, `public.ts`, `admin.ts`, `api.ts`, `i18n.ts`, `main.ts`, `app.css`.
- New settings: `Admin:ContactMaxLength` 2000, `Admin:ContactPerAddressPerDay` 5.
