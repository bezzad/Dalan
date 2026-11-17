# Tasks

## 1. Admin core

- [ ] 1.1 Migrations: `admin.settings`, `admin.audit_log`, `identity.users.builder` and `suspended_at`, `relay.bots.suspended_at`
- [ ] 1.2 Write tests for ADM-05 to ADM-09; build the admin door, the settings provider and the audit log; verify they pass
- [ ] 1.3 Write tests for ADM-10 to ADM-12; build access log search, users and bots; verify they pass

## 2. Identity and Relay

- [ ] 2.1 Write tests for AUTH-18 to AUTH-20; build admin phones, builder request, suspend; verify they pass
- [ ] 2.2 Write tests for REL-36 to REL-39 and TG-22; build builder bots, rotation, suspended bot; verify they pass

## 3. Client

- [ ] 3.1 Write tests for CLI-23 to CLI-25; build the screens; check them in a real browser

## 4. Finish

- [ ] 4.1 Update docs and `scripts/demo-bot.mjs`
- [ ] 4.2 Verify CI is green, review the whole diff, archive this change
