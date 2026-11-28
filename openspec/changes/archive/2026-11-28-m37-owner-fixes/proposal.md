# m37: owner's fixes of 2026-11-27

## Why
The owner (2026-11-27), from use on the site and on Windows:
1. On a phone, a long press on a bot message opens no Copy or Reply (Safari fires no `contextmenu` on touch).
2. The home page titles were colored like ngrok's; #56 made them plain green. Bring the colors back, with a smooth animation.
3. Dashboard in the user menu opens the admin overview; it should open the dashboard.
4. One person is two users (a phone account and an email account), the owner too: a new email login makes a new
   account, and the merge refuses any admin account.
5. Building a bot needs no request or approval (a verified phone stays required, owner's choice).
6. `dalan service install` failed on Windows, and its text did not say enough.

## What changes
CLI-52 (long press), CLI-94 (colored titles), CLI-78 (Dashboard), AUTH-31/SEC-15 (an admin merges with the second
factor, owner's choice), CLI-95 (a new email login says it makes a new account), AUTH-39 and CLI-23 (a verified phone
is a builder, no request), AGT-14 (Windows service).
