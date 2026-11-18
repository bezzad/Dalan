# Dalan — Iran server runbook

How to set up the production server, deploy, back up and run the shutdown drill. Rules: `OPS-09` to `OPS-12`, `DEL-04` to `DEL-06`. Facts behind it: `docs/DESIGN.md` section 14.

## 1. Before the server

1. Server in an Iranian data centre, newest Ubuntu LTS, 2 vCPU, 4 GB RAM, 40 GB or more SSD. In the provider's firewall open only TCP 22, 80, 443.
2. Log in with your own SSH key instead of a password (on your own machine; skip `ssh-keygen` if `~/.ssh/id_ed25519.pub` exists):

   ```bash
   ssh-keygen -t ed25519
   ssh-copy-id -i ~/.ssh/id_ed25519.pub root@<server>
   ssh root@<server>     # must not ask for a password
   ```

3. Check the server (on the server). Ports 80 and 443 must be free; stop any old web server first:

   ```bash
   lsb_release -ds; free -m; df -h /; ss -ltnp | grep -E ':(80|443|5432|5000) '
   ```

4. DNS hosted inside Iran (for example the provider's DNS). Add `A` records: `dalanet.ir` and `api.dalanet.ir` to the server address. Do this first: Caddy asks Let's Encrypt for certificates as soon as it starts. Before the domain is ready, test with `sslip.io` names, which point to the address inside them, for example `203-0-113-10.sslip.io` and `api.203-0-113-10.sslip.io` for the address 203.0.113.10 (they need foreign DNS, so not for the drill). Later run `install.sh` again with the real domains; secrets and data stay.
5. A key pair for CI, on your own machine. It must exist before the next step:

   ```bash
   ssh-keygen -t ed25519 -N "" -C dalan-ci -f ~/.ssh/dalan-deploy
   ```

## 2. Set up the server (once)

From the repository on your own machine (Linux, macOS, WSL or Git Bash; needs `ssh`, `scp`, `curl`):

```bash
git pull
bash deploy/install.sh root@<server> dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
```

The user is root or has sudo. It downloads Caddy here, copies it with `deploy/` to the server and runs `deploy/setup.sh` with sudo. A server with less than 2 GB of memory and no swap gets a 2 GB swap file (`SWAP_MB`). The server downloads only Ubuntu packages from its mirror. It is safe to run again.

Result: users `dalan` (service) and `deploy` (CI; may only `sudo systemctl restart dalan`), PostgreSQL with database `dalan`, Caddy, the units `dalan`, `caddy`, `dalan-backup.timer`, and `/etc/dalan/dalan.env` with a new database password, signing key and push keys. That file is made once and never overwritten.

Then on the server, fill in the rest of the secrets:

```bash
sudo nano /etc/dalan/dalan.env
#   Notification__SmsProvider=kavenegar
#   Notification__Kavenegar__ApiKey=<from the Kavenegar panel; template dalan-login>
#   Identity__AdminPhones=<your mobile number; several are comma separated>
# Only when the domains are not dalanet.ir and api.dalanet.ir (for example sslip.io names):
#   Admin__SiteUrl=https://<site domain>
#   Admin__BotApiUrl=https://<api domain>
#   Notification__Push__Subject=https://<site domain>
```

Without the SMS provider nobody can log in: the first login is always by SMS. Before Kavenegar is ready, test with the log sender: `Notification__SmsProvider=log` and `Notification__LogSmsPhones=<your number>` (only listed numbers get a code), then read the code with `journalctl -u dalan | grep "login code"`. Remove both lines when Kavenegar works.

## 3. Deploy

GitHub, repository Settings, Environments, `production` (create it if missing), secrets:

| Secret | Value |
|---|---|
| `DEPLOY_HOST` | `deploy@<server>` |
| `DEPLOY_SSH_KEY` | the private key: the whole output of `cat ~/.ssh/dalan-deploy` |
| `DEPLOY_KNOWN_HOSTS` | the output of `ssh-keyscan <server>` |

Actions, workflow `ci`, "Run workflow", tick "Deploy". After all gates pass it uploads the release and rolls back by itself if `/ready` fails. A later change of `dalan.env` needs `sudo systemctl restart dalan`.

Check: `https://dalanet.ir` opens, SMS login works, your number sees the admin area. Logs: `journalctl -u dalan -f`.

If the deploy says "not ready", read the error on the server:

```bash
journalctl -u dalan -o cat -n 300 | grep -m1 '"Exception"' | python3 -c 'import json,sys; print(json.loads(sys.stdin.read())["Exception"][:1500])'
```

"ASN1 corrupted data" means a signing key from a `setup.sh` before 2026-11-18 (SEC1 instead of PKCS#8). Make a new one:

```bash
NEW=$(openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 | openssl pkcs8 -topk8 -nocrypt -outform DER | base64 -w0)
sudo sed -i "s|^Identity__SigningKey=.*|Identity__SigningKey=$NEW|" /etc/dalan/dalan.env && sudo systemctl restart dalan
```

## 4. Backups

`dalan-backup.timer` dumps the database every day to `/var/backups/dalan/dalan-<date>.dump` and keeps 14 days (`BACKUP_KEEP_DAYS` in `/etc/dalan/backup.env`). The dumps are on the same disk: copy them off the server from time to time (`scp root@<server>:/var/backups/dalan/*.dump .`). Files sent in chats are in `/var/lib/dalan/files`.

Restore: `sudo systemctl stop dalan`, then `sudo -u postgres pg_restore --clean --if-exists -d dalan <file>`, then `sudo systemctl start dalan`.

## 5. Shutdown drill

The drill blocks every new outbound connection from the server, as if international links were cut. Users, bots and SSH still come in.

1. Optional allow list `/etc/dalan/drill-allow.txt`, one range per line, for the domestic network (for example the Iranian ranges from ipdeny.com, `ir-aggregated.zone`). Without it even Kavenegar and DNS are blocked, which shows what SMS login needs.
2. `sudo /opt/dalan/tools/drill.sh start`. It stops by itself after 60 minutes (`DRILL_MINUTES` in `/etc/dalan/drill.env`).
3. By hand, on a phone on Iranian mobile data with no VPN: open `https://dalanet.ir`, log in by SMS, open ArcaneBot, send a message, press buttons, get replies. ArcaneBot runs on its home machine with only two changes: server address `https://api.dalanet.ir` and the token from "My bots".
4. `sudo /opt/dalan/tools/drill.sh stop`. It prints each blocked address and port. Expected: push services (Google, Apple, Mozilla, Microsoft) and Let's Encrypt. Anything else is a bug to fix.

What the drill does not show: in a real shutdown certificates cannot be renewed (renew early), push stops (sync still works), and in a total blackout nothing works.
