# Proposal

## Why

Milestones 1 to 10 run in CI and on a test machine. Version 1 is done only when the server runs in Iran and keeps working when international internet is cut (DESIGN sections 14 and 19). Today the repository can deploy a release over SSH, but nothing prepares a new server, nothing backs up the database, and nothing proves that Dalan works with foreign hosts blocked.

## What Changes

- **Server setup**: one script prepares a fresh Ubuntu server: users, PostgreSQL, Caddy, systemd units, folders and the secret settings file. It makes the secrets once and never overwrites them. It runs again safely. A small script on the owner's machine copies everything (including the Caddy binary) and runs it, so the server downloads nothing from abroad.
- **Daily backup**: a systemd timer dumps the database every day and keeps the newest days.
- **Shutdown drill**: a script on the server blocks every new outbound connection except to private ranges and an allow list (for example Iranian ranges), ends by itself after a set time, and lists the foreign addresses the server tried. Done by hand with ArcaneBot: login, chat, buttons, with the block on.
- **Own mail**: the server sends the login emails itself from `no-reply@<site domain>`: Postfix (send only, loopback only) with a DKIM signature. Setup prints the DNS records (SPF, DKIM, DMARC).
- **Runbook**: `docs/SERVER.md`, short.

Out of scope: a firewall on the server (the provider panel does it; only Caddy listens publicly), a second server, off-site backup copies, a paid fallback certificate, monitoring.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `operations`: server setup, backup, shutdown drill.

## Impact

- New files in `deploy/`: `setup.sh`, `install.sh`, `backup.sh`, `drill.sh`, `caddy.service`, `dalan-backup.service`, `dalan-backup.timer`.
- No code or database change in the server.
- New server settings: listed in design.md.
