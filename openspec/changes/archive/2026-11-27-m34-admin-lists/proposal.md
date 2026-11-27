# m34: Dashboard in the menu; admin lists and detail pages

## Why
The owner (2026-11-27): the user menu has Settings and Admin, but no way to the dashboard itself. In the admin
pages, Users and Bots are empty until a search; the overview says "5 users" and a click shows nothing; a user or a
bot has no detail page. They should be lists and detail pages like ngrok's dashboard.

## What changes
- CLI-78: the user menu has one "Dashboard" item for everyone instead of Settings and Admin (owner's choice).
- ADM-11, ADM-12: with no search, the lists show everyone, newest first, a page at a time; a user has a bot count
  and the last active time, a bot its count of chat users.
- ADM-20: one user with their bots; one bot with its owner.
- CLI-93: Users and Bots are tables; a row opens its detail page at its own address, with the actions there.

## Impact
Identity (`IUserAdmin`), Relay (`IBotAdmin`), Admin endpoints, client `admin.ts` and the dashboard routes. No new setting:
the page size is `Admin:PageSize`.
