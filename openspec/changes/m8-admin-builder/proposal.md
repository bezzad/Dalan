# Proposal

## Why

Today every setting lives in `appsettings.json` and the server environment, so the owner must edit files and restart to change a limit. Bots can be made only with SQL. Nobody can be suspended. For a legal complaint the owner must read `admin.access_log` with SQL. Milestone 8 (docs/DESIGN.md section 19) adds the admin and builder areas: limits, builder approval, suspend, bot creation and token rotation.

## What Changes

- **Admin**: the owner's phone numbers are in `Identity:AdminPhones`. Admin screens behind `/api/admin/...`, all in the Admin service:
  - **Settings**: every limit, expiry, size and domain is listed and can be changed or reset from the panel, without a restart (a few are marked "after restart"). Secrets and a few start-only safety settings are never listed. Changes are stored in `admin.settings` and win over the file and environment.
  - **Access log search** by user, bot, phone or address, for legal complaints.
  - **Users**: find by phone, username or id; approve or refuse builder requests; suspend and unsuspend.
  - **Bots**: find by username, id or owner; suspend and unsuspend.
  - **Audit log**: every admin action and every bot creation or token rotation.
- **Builder**: a user asks to become a builder; after approval the user can create bots (token shown once), list them and rotate a token.
- **Suspend**: a suspended user is logged out on every device at once and cannot log in. A suspended bot gets 403 on every bot call and users cannot write to it.
- **Client**: builder request in the account screen, "My bots" screen, admin screen.

Out of scope: email (builder needs only admin approval for now), per-user and per-bot limit overrides, bot profile (description, icon, visibility: milestone 9 showcase), deleting a bot, a second admin role, security events in the audit table (they stay log lines), settings shared by several server instances.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `admin`: admin door, settings, audit log, access log search, user and bot lists.
- `identity`: admin by phone, builder request and approval, suspend.
- `relay`: builder bots, token rotation, suspended bot.
- `telegram`: suspended bot.
- `client`: builder and admin screens.

## Impact

- Database: `admin.settings`, `admin.audit_log`; `identity.users.builder`, `identity.users.suspended_at`; `relay.bots.suspended_at`. Three migrations.
- New contracts: `IUserAdmin` (Identity.Contracts), `IBotAdmin` (Relay.Contracts), `IAuditLog` (Admin.Contracts). Admin references Identity.Contracts and Relay.Contracts.
- The host adds the settings from the database as the last configuration source.
- New settings: listed in design.md.
- Owner to-do: put the owner's phone in `Identity__AdminPhones` on the server.
