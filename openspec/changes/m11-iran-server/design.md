# Design

## Context

Milestones 1 to 10 are merged. `deploy/deploy.sh` copies a release over SSH, switches a link, restarts `dalan` and rolls back if `/ready` fails. CI runs it from a manual workflow with the secrets `DEPLOY_HOST`, `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS`. The owner delegated the spec review.

## Goals / Non-Goals

**Goals:**
- A fresh server becomes ready for the first deploy with one command and a few lines the owner fills in.
- Nothing on the server comes from a foreign host (DEL-06, rule 7).
- The database is backed up every day.
- A repeatable drill that shows Dalan works with international traffic blocked, and names what still calls abroad.

**Non-Goals:**
- Firewall rules, more servers, off-site backups, alerts, Layer 2.

## Decisions

1. **Two scripts: `install.sh` on the owner's machine, `setup.sh` on the server.** `install.sh <user@host>` (root, or a user with sudo) downloads Caddy (pinned version, checksum checked, same version as CI), copies `deploy/` and the binary with `scp`, and runs `setup.sh` with `sudo` over `ssh`. A server with less memory than `SWAP_MB` and no swap gets a swap file (the first test runs on a 1 GB US server). It works from Linux, macOS, WSL or Git Bash. The server never needs GitHub.
2. **`setup.sh` is idempotent.** Every step checks first: users, folders, database role, files. Users: `dalan` (runs the service, no login) and `deploy` (CI logs in; owns `/opt/dalan`; may only run `sudo systemctl restart dalan`, one sudoers line). PostgreSQL comes from the Ubuntu packages (domestic mirrors work in a shutdown; the PostgreSQL apt site is abroad).
3. **Secrets are made on the server, once.** If `/etc/dalan/dalan.env` does not exist, `setup.sh` writes it with a random database password, `Identity__SigningKey` (P-256, like the unit file says) and a VAPID pair, all made with `openssl`. Kavenegar and admin phones are commented lines the owner fills in. If the file exists it is never touched. Mode `640`, owner `root:dalan`. Domains go to `/etc/dalan/caddy.env`, written each run.
4. **Caddy runs under systemd** with `DynamicUser`, state in `/var/lib/caddy`, the bind capability only, and `/etc/dalan/caddy.env`. The repository `Caddyfile` is copied as is.
5. **Backup** is `deploy/backup.sh` started daily by `dalan-backup.timer`, as user `postgres`: `pg_dump -Fc` to `/var/backups/dalan/dalan-<date>.dump`, then delete dumps older than `BACKUP_KEEP_DAYS`. The timer is `Persistent`, so a missed day runs after boot.
6. **The drill blocks outbound, not inbound.** `drill.sh start` adds one nftables table with an output hook: allow loopback, replies (established), private ranges, and the CIDRs in `/etc/dalan/drill-allow.txt` (if present; for example an Iranian ranges list); log and drop the rest. Users, the bot machine and SSH still come in. A systemd timer runs `drill.sh stop` after `DRILL_MINUTES`, so a forgotten drill ends. `stop` removes the table and prints each blocked address once, from the kernel log. Without an allow list, even domestic services (Kavenegar, DNS) are blocked: the report shows them.
7. **The real check is by hand**, written in `docs/SERVER.md`: with the drill on, a phone on Iranian mobile data with no VPN logs in by SMS, opens ArcaneBot, presses buttons and gets replies; ArcaneBot changed only its address and token. Push is expected to stop (DESIGN section 8).

8. **Log in before the SMS provider is ready.** A Kavenegar account takes time. The `log` sender (code in the server log, no SMS) may now run outside Development, but only for the numbers in `Notification:LogSmsPhones`; any other number gets a failed send. The log is readable only by root on the server. The setting is hidden from the admin area, like the provider.

9. **Own mail, send only.** The owner wants login emails from `no-reply@dalanet.ir` with no outside mail service. `setup.sh` installs Postfix (Ubuntu package, "Internet Site", `inet_interfaces = loopback-only`, IPv4 only, `myhostname` = site domain, no relay for others) and OpenDKIM (`/etc/opendkim.conf` written each run, key `/etc/dkimkeys/dalan.private` made once, milter on `127.0.0.1:8891`). Dalan sends by plain SMTP to `127.0.0.1:25`; Postfix sends on with opportunistic TLS. If `dalan.env` has no `Notification__Smtp__Host` line, setup adds `EmailProvider=smtp`, host, port 25, no STARTTLS and `From` at the end (a later line wins in a systemd environment file, so an old `EmailProvider=log` line needs no edit) and restarts `dalan` if it runs. Setup prints the three TXT records: SPF `v=spf1 a -all`, DKIM `dalan._domainkey`, DMARC `v=DMARC1; p=none`. Reverse DNS of the server address is set in the provider panel by hand.

## Starting values used

| Setting | Value | Source |
|---|---|---|
| `BACKUP_KEEP_DAYS` (`/etc/dalan/backup.env`) | 14 | Assumed |
| `DRILL_MINUTES` (`/etc/dalan/drill.env`) | 60 | Assumed |
| `CADDY_VERSION` (`install.sh`) | same as CI | Rule 10 |
| `SWAP_MB` (`setup.sh`) | 2048 | Assumed (the US test server has 1 GB) |
| `Notification:LogSmsPhones` | empty | Owner (2026-11-18): log in on the server before Kavenegar is ready |

## Risks / Trade-offs

- [Backups on the same disk] A lost disk loses them too. The runbook says to copy them off the server; automatic off-site copy is later.
- [No firewall from the script] Only Caddy (80, 443) and SSH listen publicly; PostgreSQL and Kestrel listen on localhost. A script that changes the firewall can lock the owner out.
- [Own mail can be blocked] Port 25 outbound may be closed by the provider, and a new address has no reputation, so mail may land in spam. The login screen already says to look in spam. When the international internet is cut, foreign mailboxes cannot be reached; SMS login keeps working.
- [The drill is a simulation] A real shutdown may also break domestic DNS or the certificate renewal; the runbook lists those.
