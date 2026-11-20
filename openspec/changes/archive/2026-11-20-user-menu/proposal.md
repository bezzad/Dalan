# Proposal

## Why

The owner (2026-11-20): the site header has no user area, so log in, log out and settings are hard to find. The account page looks like a phone page on a desktop; ngrok's clean pages are the reference.

## What Changes

- The end of the site header, on every page: "Log in" for a visitor; for a logged-in user a small menu under the name with My bots, Account, Admin (admins only) and Log out. The bot list header loses its own Account and Log out links.
- Account on a wide screen: a wider page, cards in two columns, buttons as wide as their words.

## Capabilities

No rule changes: layout only.

## Impact

Client only. No new setting.

## Later in the same PR

- `docs/UI.md`: one design system for every screen, so the same notes are not needed per page.
- Toasts (`client/src/toast.ts`) for every user action: account forms, builder, admin actions, agents. The inline status and error lines of these actions go.
- The builder screen ("My bots") follows it: wide page, "Create bot" in the title row opens the form, the bots as rows in one card.
