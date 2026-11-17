# Proposal

## Why

A user of Dalan does not know when a bot wrote while the app was closed, cannot see which chats have new messages, cannot tell whether the bot's home computer is on, and does not see that a message never reached the bot. Milestone 7 (docs/DESIGN.md section 19) adds Web Push, unread counts, bot status and "not delivered" marks. Version 1 is done only when a user "receives a notification".

## What Changes

- **Web Push**: a user turns on notifications on a device; every new bot message is pushed to that user's devices with the Web Push standard (VAPID keys, encrypted payload). Dead subscriptions are removed. Push is off while the server has no VAPID keys.
- **Unread counts**: each chat keeps the user's read position. The bot list shows the number of unread bot messages; reading a chat moves the position; other devices of the user hear about it live.
- **Bot status**: the server remembers when each bot last called get-updates. The chat header shows "online" or "last seen ...".
- **"Not delivered" marks**: when a user message becomes "not delivered" (expiry, replaced by a newer command, or found expired at start), the message gets a new version and an event, so the client shows the mark live and after a sync.
- **Client**: unread badges, bot status line, the "not delivered" mark, a notifications switch in the account screen, and a service worker that shows pushes and opens the chat on a tap.

Out of scope: an app badge on the home-screen icon, email, SMS notifications, per-chat mute, a "delivered" or "read by bot" mark, live status events (the client asks again every few seconds), push for edits.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `relay`: list with unread and status, `not_delivered` in a message, read position, bot status, push for new bot messages, the push subscription door.
- `live-updates`: the `read` event.
- `notification`: push subscriptions and the Web Push sender.
- `client`: unread badges, bot status, "not delivered" mark, notifications.

## Impact

- Database: `relay.memberships.last_read_seq`, `relay.bots.last_seen_at`; new schema `notification` with table `push_subscriptions`. Two migrations (Relay, Notification).
- New package: `Lib.Net.Http.WebPush` 3.3.1 (latest stable; on .NET it uses only the built-in crypto).
- `Relay` references `Dalan.Notification.Contracts`.
- New settings: listed in design.md.
- Owner to-do: make VAPID keys once (`node scripts/vapid-keys.mjs`) and put them in the server environment.
