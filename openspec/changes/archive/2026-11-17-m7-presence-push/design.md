# Design

## Context

Milestones 1 to 6 are merged. Messages reach an open app over SSE and by sync. Delivery states (`pending`, `delivered`, `not delivered`) are stored but never shown, and changing them makes no version and no event. Notification has only the login SMS and no data. The owner delegated the spec review; section 18 values are used as defaults.

## Goals / Non-Goals

**Goals:**
- What docs/DESIGN.md section 19 lists for milestone 7: Web Push, unread counts, bot status, "not delivered".
- A push never slows a sender, and a failed push loses nothing: sync brings every message.
- The server never sends a request to an address a user typed (no server-side request forgery through push).

**Non-Goals:**
- App icon badge, email, per-chat mute, "delivered" or "read by bot" marks, live status events, push for edits or for the user's own messages.

## Decisions

1. **Push for every new bot message; the service worker decides.** DESIGN section 8 says "push only when the browser has no open connection". A phone that locks its screen often keeps the TCP connection open on the server for minutes, so the server would think the app is open and send nothing. So Relay pushes every new bot message, and the service worker shows nothing when a focused window already shows that chat. Cost: more push requests while the app is open. Easy to change later (one condition). DESIGN is updated.
2. **Door in Relay, data and sender in Notification.** The client calls `/api/push/...` in Relay's Client API group, so the session check, the access log (ADM-01) and the per-user limit (SEC-03) hold with no new code. Relay calls the contract `IWebPush` (in `Dalan.Notification.Contracts`). Notification owns `notification.push_subscriptions` and the sender.
3. **Async path.** The `MessageChanged` event in Relay's outbox gets a `new` flag. The outbox worker passes new bot messages to `IWebPush.Send`, which only puts the push in an in-memory queue (`Notification:Push:QueueSize`). A few workers (`Notification:Push:Workers`) send. A full queue drops the push with a log line. A push lost in a restart is not sent again; the message itself is safe in the database and arrives by sync.
4. **Only known push services.** A subscription's endpoint must be `https` on a host in `Notification:Push:AllowedHosts` (the host itself or a subdomain): Google, Apple, Mozilla and Microsoft at the start. Any other endpoint is refused. Keys must have the sizes of the standard (65 and 16 bytes).
5. **One owner per endpoint.** An endpoint is unique. Saving it again (for example another person logs in on the same browser) moves it to the new user. On logout the client removes this device's subscription first. At most `Notification:Push:MaxSubscriptionsPerUser` per user; the oldest goes.
6. **Dead subscriptions.** 404 or 410 from the push service deletes the subscription. Other errors are logged and the subscription stays. No retry: the next message tries again.
7. **Payload.** JSON `{"title","body","url","tag"}`: bot name, the text (or the file name) cut to `Notification:Push:MaxBodyLength`, `/@username`, `bot-<id>`. Encrypted with `aes128gcm` (RFC 8291) and signed with VAPID (RFC 8292). Headers: `TTL` (`Notification:Push:TtlSeconds`), `Urgency: high`, `Topic: bot-<id>` (the push service keeps only the newest push of a chat while the phone is off). The push service sees no text.
8. **Keys.** `Notification:Push:PublicKey`, `PrivateKey` (secret, server environment only) and `Subject`. Both keys empty = push is off: `GET /api/push` gives `key: null` and the client hides the switch. A wrong key stops the start. `scripts/vapid-keys.mjs` makes a pair.
9. **Library.** `Lib.Net.Http.WebPush` 3.3.1 does the encryption and the VAPID token with the crypto built into .NET. Writing RFC 8291 ourselves is about 100 lines of crypto; a used and tested library is the smaller risk. Tests decrypt one push with the subscriber's key to prove the payload.
10. **Unread count.** `relay.memberships.last_read_seq`. Unread = bot messages of the chat, not deleted, with `id` above it. `POST /api/bots/{id}/read` moves it forward only, never past the newest message. A message the user sends moves it to that message. Counting is cheap: the history of a chat is small and `(bot_id, user_id, seq)` has an index.
11. **`read` live event.** After a read, Relay publishes `read` (`bot_id`, `unread`) straight to the notifier, like a button answer: not stored. A lost one only leaves a stale number until the next list load.
12. **Bot status.** In memory per bot: polls running now and the time of the last poll. Online = a poll is running, or the last one ended less than `Relay:BotOnlineSeconds` ago. `relay.bots.last_seen_at` is written at most once per `BotOnlineSeconds` per bot, so "last seen" survives a restart. The client asks `GET /api/bots/{id}/status` every `status_refresh_seconds` while the chat is open; a live event would go to all users of a bot (up to 100,000), which is too much. With more than one server this map becomes shared, like the queue (docs/ARCHITECTURE.md section 8).
13. **"Not delivered" makes a change.** When user messages become "not delivered" (Q-02, Q-04, Q-07), each one gets the next version of its chat and a `MessageChanged` event in one transaction, with the chats locked in id order (no deadlock). "Delivered" stays a silent update: showing it would add a version and an event to every message.

## Starting values used

| Setting | Value | Source |
|---|---|---|
| `Relay:BotOnlineSeconds` | 60 | Section 18 |
| `Relay:ClientStatusRefreshSeconds` | 30 | Assumed |
| `Notification:Push:PublicKey`, `PrivateKey` | empty (push off) | Owner sets them |
| `Notification:Push:Subject` | `https://dalanet.ir` | Assumed |
| `Notification:Push:AllowedHosts` | `fcm.googleapis.com,push.apple.com,push.services.mozilla.com,notify.windows.com` | Assumed |
| `Notification:Push:TtlSeconds` | 86400 (1 day) | Assumed |
| `Notification:Push:MaxSubscriptionsPerUser` | 10 | Assumed |
| `Notification:Push:MaxEndpointLength` | 1000 | Assumed |
| `Notification:Push:MaxBodyLength` | 200 | Assumed |
| `Notification:Push:QueueSize` | 10000 | Assumed |
| `Notification:Push:Workers` | 4 | Assumed |
| `Notification:Push:TimeoutSeconds` | 10 | Assumed |

## Risks / Trade-offs

- [Push depends on foreign services] During an international shutdown no push arrives; sync still works (DESIGN section 16).
- [More push traffic] Every bot message is pushed, also while the app is open (decision 1).
- [iPhone] Push works only after the user adds the app to the home screen; the client says so.
- [Status is up to `status_refresh_seconds` old] Good enough for "is the home machine on?".
- [Pushes in memory] A restart loses pushes not yet sent; the messages are not lost.
