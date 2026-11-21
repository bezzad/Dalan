# Dalan — Iran server runbook

How to set up the production server, deploy, back up and run the shutdown drill. Rules: `OPS-09` to `OPS-16`, `DEL-04` to `DEL-08`. Facts behind it: `docs/DESIGN.md` section 14.

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
5. A key pair for the fallback deploy over SSH (your computer or a CI run by hand), on your own machine. It must exist before the next step:

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

Result: users `dalan` (service) and `deploy` (builds and deploys; may only `sudo systemctl restart dalan`), PostgreSQL with database `dalan`, Caddy, `rsync` (the deploy needs it), the units `dalan`, `caddy`, `dalan-backup.timer`, Postfix and OpenDKIM for the login emails, `/etc/dalan/domains.env` (the domains; written each run) and `/etc/dalan/dalan.env` with a new database password, signing key and push keys. The secrets are made once and never overwritten; setup only adds the email lines at the end when it has no `Notification__Smtp__Host` line.

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

### Sites (Layer 2, optional)

Users' sites live on their own registered domain (ours: `dalans.ir`), never under the site domain (SITE-13). The site agents connect to the tunnel host, `tunnel.<site domain>` unless `TUNNEL_DOMAIN` says another.

1. DNS: an `A` record `tunnel` in the site domain, and in the sites domain an `A` record `*` (and `@` if you like), all to the server address with the CDN proxy **off**.
2. Run `install.sh` again with the sites domain. Give `SITES_DOMAIN` (and `TUNNEL_DOMAIN`, if not the default) on **every** later run too: without it, sites are off again.

   ```bash
   SITES_DOMAIN=dalans.ir bash deploy/install.sh root@<server> dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
   ```

3. Check: `curl -fsSL https://dalanet.ir/install.sh | head -3`, then on any computer install the agent, make a key in "My apps" and run a site. Caddy gets each site's certificate on its first visit (a few seconds), only for names Dalan knows (SITE-17).

Limit: Let's Encrypt gives about 50 new certificates per registered domain per week, so about 50 new site names a week. After buying the sites domain, ask for it on the Public Suffix List (https://github.com/publicsuffix/list), so a site cannot set cookies for the other sites. Before the domain is ready, test with sslip.io: `SITES_DOMAIN=203-0-113-10.sslip.io` gives `name.203-0-113-10.sslip.io` (it needs foreign DNS, so not for the drill).

The agent files come with each deploy (`/opt/dalan/current/agent`, DEL-08); `/downloads/<file>` serves them.

### A new computer for the owner

Only `install.sh`, `scripts/ci.sh` (the tests, docs/TESTING.md) and SSH need your own computer; the server builds main itself. On the new computer (Linux):

1. `git clone https://github.com/bezzad/Dalan && cd Dalan`.
2. Your own SSH key: `ssh-keygen -t ed25519`, then put `~/.ssh/id_ed25519.pub` on the server. From the old computer: `ssh-copy-id -i <new key>.pub root@<server>`; with no old computer, paste it into `/root/.ssh/authorized_keys` from the provider's web console. Check: `ssh root@<server>` asks no password.
3. The CI public key, which `install.sh` needs: `ssh root@<server> cat /home/deploy/.ssh/authorized_keys > ~/.ssh/dalan-deploy.pub`. The CI private key stays only in the GitHub secret; if it is lost or leaked, make a new pair (section 1, step 5), run `install.sh` with the new `.pub`, and put the new private key in `DEPLOY_SSH_KEY`.
4. Run `install.sh` again with the same settings when needed. `dalan.env` on the server keeps every secret, so nothing else moves with you.

A bot you run at home (for example ArcaneBot) needs only its token and `https://api.<domain>`; the site page `/guide` has the steps.

## 3. Deploy

The server builds `main` itself (OPS-16, next section): merge a pull request after `scripts/ci.sh` passed on your computer, and the new release runs a few minutes later. GitHub CI does not run by itself any more (its minutes ran out, 2026-11-21).

Fallbacks, both over SSH with `deploy/deploy.sh`: it sends the release with `rsync` to a new folder (only the parts that differ from the current release go over the network), checks `/ready` after the switch and rolls back by itself. It opens one SSH connection with up to 5 tries of 30 seconds (`SSH_TRIES`, `SSH_TIMEOUT`), because the link to Iran stalls now and then.

- From your computer, when the server cannot reach GitHub: `bash scripts/build-release.sh out`, then `DEPLOY_HOST=root@<server> SYSTEMCTL=systemctl bash deploy/deploy.sh out manual-<name>` and `ssh root@<server> chown -R deploy:deploy /opt/dalan/releases`.
- A CI run by hand: Actions, workflow `ci`, "Run workflow" on `main`, tick "Deploy". It is the only way to build the Windows agent (`dalan-windows-x64.exe`, DEL-08): run it after a change in `agent/`. The server's own builds keep that file. It needs these secrets in GitHub, repository Settings, Environments, `production`:

| Secret | Value |
|---|---|
| `DEPLOY_HOST` | `deploy@<server>` |
| `DEPLOY_SSH_KEY` | the private key: the whole output of `cat ~/.ssh/dalan-deploy` |
| `DEPLOY_KNOWN_HOSTS` | the output of `ssh-keyscan <server>` |

"Connection closed by <server> port 22" or "No SSH connection" in a deploy log is a stall: run it again. On the server, `journalctl -u ssh | grep 'Timeout before authentication'` shows it. A server set up before 2026-11-19 may not have `rsync`: run `install.sh` again, or `sudo apt-get install rsync`. A change of `dalan.env` by hand needs `sudo systemctl restart dalan`.

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

### The server builds main itself (OPS-16)

The main way a release reaches the server (DEL-06). The server needs GitHub, but nothing else from abroad after the first build (NuGet and npm packages stay in `/home/deploy`).

1. On your computer: `PULL_DEPLOY=1 SITES_DOMAIN=dalans.ir bash deploy/install.sh root@<server> dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub`. It downloads the .NET SDK (the version in `global.json`, newest patch) and Node (the major version in `ci.yml`) here, about 300 MB, and copies them; the server downloads neither. They stay in `~/.cache/dalan-install` (`DALAN_CACHE`), so a later run downloads only a new version. Give `PULL_DEPLOY=1` again only to update them.
2. Setup makes the GitHub key and prints it. Add it to GitHub read-only (below).
3. `dalan-pull.timer` runs every minute. Each run asks GitHub only for `refs/heads/main` (`git ls-remote`, small); no other branch is ever fetched or built. A new commit is fetched, built in a new folder (about 5 minutes, low priority), smoke-tested on `/health` (no database), and deployed with `deploy.sh`: switch, restart, `/ready`, rollback when not ready. Only the last 5 releases are kept. A lock keeps two runs from overlapping. Watch: `journalctl -u dalan-pull -f`. Start a run now: `sudo systemctl start dalan-pull`.

A failed build or a deploy that rolled back is not tried again: `/opt/dalan/pull-failed` holds its commit and the journal has the reason. Push a fix to `main`. A failed `git ls-remote` or fetch (GitHub slow or cut) is tried again on the next run. The full .NET tests do not run here (they need a test database server; `scripts/ci.sh` runs them): only the client checks, the release smoke test and the Linux agent build.

#### The GitHub deploy key

Setup does the server part. The same by hand, as the user `deploy` on the server:

```bash
sudo -iu deploy
ssh-keygen -t ed25519 -N '' -C dalan-pull@dalanet.ir -f ~/.ssh/github
cat > ~/.ssh/config <<'EOF'
Host github.com
  HostName ssh.github.com
  Port 443
  User git
  IdentityFile ~/.ssh/github
  IdentitiesOnly yes
  StrictHostKeyChecking yes
EOF
# GitHub's host keys for port 443 (deploy/setup.sh has the full lines). Compare the fingerprints with
# docs.github.com "GitHub's SSH key fingerprints", for example ED25519 SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU
ssh-keyscan -p 443 ssh.github.com > ~/.ssh/known_hosts && ssh-keygen -lf ~/.ssh/known_hosts
cat ~/.ssh/github.pub          # the key for GitHub
```

Port 443, because port 22 to GitHub is often closed from Iran. If GitHub changes a host key, update the lines in `deploy/setup.sh` and run `install.sh` again.

On GitHub, read-only: repository Settings, Deploy keys, **Add deploy key**, title `dalan-server`, paste the key, leave **Allow write access** off. Or from your computer, with the key copied there (`scp root@<server>:/home/deploy/.ssh/github.pub dalan-server.pub`):

```bash
gh repo deploy-key add dalan-server.pub --repo bezzad/Dalan --title dalan-server   # read-only unless --allow-write
```

Check on the server: `sudo -iu deploy git ls-remote git@github.com:bezzad/Dalan.git refs/heads/main` prints one commit.

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
