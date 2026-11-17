# Tasks

## 1. Core (Relay)

- [ ] 1.1 Migration: `last_read_seq`, `last_seen_at`
- [ ] 1.2 Write tests for REL-31, Q-02, Q-04, Q-07 marks; build "not delivered" with a version and an event; verify they pass
- [ ] 1.3 Write tests for REL-14, REL-32, LIVE-10; build read position and unread; verify they pass
- [ ] 1.4 Write tests for REL-18, REL-33; build bot status; verify they pass

## 2. Push (Notification and Relay)

- [ ] 2.1 Migration: schema `notification`, table `push_subscriptions`; contract `IWebPush`
- [ ] 2.2 Write tests for REL-34, REL-35, NTF-03 to NTF-09; build the door, the sender and the outbox hook; verify they pass

## 3. Client

- [ ] 3.1 Write tests for CLI-18 to CLI-22; build badges, status, mark, notifications and the service worker; check the screens in a real browser

## 4. Finish

- [ ] 4.1 Update docs, `scripts/vapid-keys.mjs`, `scripts/demo-bot.mjs` if needed
- [ ] 4.2 Verify CI is green, review the whole diff, archive this change
