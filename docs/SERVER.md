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

4. DNS hosted inside Iran (for example ArvanCloud DNS or the provider's DNS): add the domain there, put its two nameservers in the nic.ir panel, then add `A` records `@` and `api` to the server address with the CDN proxy **off** (with it on, Caddy gets no certificate and the server sees the CDN's address instead of the client's, which breaks the access log duty). Check with `dig +short dalanet.ir`. Do this first: Caddy asks Let's Encrypt for certificates as soon as it starts. Before the domain is ready, test with `sslip.io` names, which point to the address inside them, for example `203-0-113-10.sslip.io` and `api.203-0-113-10.sslip.io` for the address 203.0.113.10 (they need foreign DNS, so not for the drill). Later run `install.sh` again with the real domains; secrets and data stay.
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

Result: users `dalan` (service) and `deploy` (CI; may only `sudo systemctl restart dalan`), PostgreSQL with database `dalan`, Caddy, the units `dalan`, `caddy`, `dalan-backup.timer`, Postfix and OpenDKIM for the login emails, `/etc/dalan/domains.env` (the domains; written each run) and `/etc/dalan/dalan.env` with a new database password, signing key and push keys. The secrets are made once and never overwritten; setup only adds the email lines at the end when it has no `Notification__Smtp__Host` line.

Admin phone and SMS go in as settings of the same command. Run it again to change them, or to move to new domains; it restarts the server by itself:

```bash
# Before Kavenegar is ready: login codes go to the server log, only for ADMIN_PHONES.
ADMIN_PHONES=<your mobile; several are comma separated> SMS=log bash deploy/install.sh root@<server> dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
# Read a code on the server:
journalctl -u dalan | grep "login code"
# When Kavenegar is ready (template dalan-login). read -s keeps the key out of the shell history:
read -rs KAVENEGAR_KEY && export KAVENEGAR_KEY
SMS=kavenegar bash deploy/install.sh root@<server> dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
```

Without an SMS provider nobody can log in: the first login is always by SMS.

If the secrets in `dalan.env` were seen by someone else (for example pasted in a chat), make new ones: `ROTATE_SECRETS=1 bash deploy/install.sh ...` (same arguments). Everyone must log in again and browsers subscribe to push again.

### Login emails (own mail)

The server sends the login emails itself, from `no-reply@<site domain>`: Postfix sends only, listens only on `127.0.0.1`, and OpenDKIM signs each mail (key `/etc/dkimkeys/dalan.private`, made once). Setup writes these lines to `dalan.env`, so there is nothing to fill in:

```
Notification__EmailProvider=smtp
Notification__Smtp__Host=127.0.0.1
Notification__Smtp__Port=25
Notification__Smtp__StartTls=false
Notification__Smtp__From=Dalan <no-reply@dalanet.ir>
```

At the end setup prints three TXT records. Add them in the DNS panel (Arvan: DNS, records, add a TXT record; the cloud icon of `dalanet.ir` must be off, because SPF uses its A record):

| Name | Value |
|---|---|
| `@` | `v=spf1 a -all` |
| `dalan._domainkey` | `v=DKIM1; h=sha256; k=rsa; p=...` (from the setup output, or `sudo cat /etc/dkimkeys/dalan.txt`) |
| `_dmarc` | `v=DMARC1; p=none` |

In the server provider's panel, set the reverse DNS (PTR) of the server address to `dalanet.ir`. Check:

```bash
timeout 8 bash -c '</dev/tcp/gmail-smtp-in.l.google.com/25' && echo "port 25 open"   # the provider may block it
sudo opendkim-testkey -d dalanet.ir -s dalan -vvv                                     # "key OK" after DNS is live
journalctl -u postfix -n 30                                                            # status=sent, or why not
```

Then log in with your Gmail address on the site; in Gmail, "Show original" should say SPF, DKIM and DMARC: PASS. Mail from a new address often lands in spam; the login screen tells people to look there. When the international internet is cut, foreign mailboxes cannot be reached, so email login stops and SMS login keeps working. To test without mail: `Notification__EmailProvider=log` and `Notification__LogEmailAddresses=<your email>` at the end of `dalan.env`, then `journalctl -u dalan | grep "login code"`. An email user (level 1) can use bots; to build bots the user must verify a mobile number in the account screen (level 2).

### Log in with GitHub (optional)

1. On GitHub: Settings, Developer settings, OAuth Apps, **New OAuth App** (direct link: https://github.com/settings/applications/new).
2. Application name `Dalan`; Homepage URL `https://dalanet.ir`; Authorization callback URL `https://dalanet.ir/api/auth/github/callback` (your site domain). Leave "Enable Device Flow" off. **Register application**.
3. Copy the **Client ID**. Press **Generate a new client secret** and copy it (GitHub shows it once).
4. On your own machine, run `install.sh` again with the two values. It writes them into `dalan.env` and restarts Dalan; nothing else changes:

   ```bash
   GITHUB_CLIENT_ID=<client id> GITHUB_CLIENT_SECRET=<client secret> \
     bash deploy/install.sh root@<server> dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
   ```

The login card then shows "Log in with GitHub" first. GitHub users are level 1, like email users. When the international internet is cut, GitHub cannot be reached and the card says to use email or SMS.

## 3. Deploy

GitHub, repository Settings, Environments, `production` (create it if missing), secrets:

| Secret | Value |
|---|---|
| `DEPLOY_HOST` | `deploy@<server>` |
| `DEPLOY_SSH_KEY` | the private key: the whole output of `cat ~/.ssh/dalan-deploy` |
| `DEPLOY_KNOWN_HOSTS` | the output of `ssh-keyscan <server>` |

Every merge to `main` deploys by itself after all gates pass: it uploads the release and rolls back by itself if `/ready` fails. The first deploy, or a deploy again, by hand: Actions, workflow `ci`, "Run workflow" on `main`, tick "Deploy". A change of `dalan.env` by hand needs `sudo systemctl restart dalan`.

Check: `https://dalanet.ir` opens, SMS login works, your number sees the admin area. Logs: `journalctl -u dalan -f`.

If the deploy says "not ready", read the error on the server (the first line is for an error at start, the second for one in the JSON log):

```bash
journalctl -u dalan -n 200 -o cat --no-pager | grep -v '^{"Timestamp' | tail -n 25
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
