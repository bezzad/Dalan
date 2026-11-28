# m38: an inbox under the bell, with push and an SMS when push cannot reach

## Why
Owner (2026-11-27): anyone may make a permanent site for now, but the admin must hear of each one, to check that no one
breaks the law and the sites domain is not closed. Owner's design: one inbox for every user, admin or common, under the
bell; each notice also comes as a push, and when push cannot reach (no internet abroad), as a short SMS.

## What changes
- NTF-12: the inbox (stored notices, list, unread count, mark read).
- NTF-13: each notice is pushed (NTF-06); with no push device, or when every push fails, a short SMS (limited per day).
- CLI-96: the bell in the header with the unread count, the list, a notice opens its link.
- SITE-25: a new site name (first claim, SITE-03, or a reserve, SITE-24) sends every admin a notice.

## Impact
Notification gets a schema table and the `IInbox` contract; Sites calls it. New settings in DESIGN section 18:
`Notification:InboxDays`, `Notification:InboxSmsPerDay`, `Notification:InboxSmsLength`.
