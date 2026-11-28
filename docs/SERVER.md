# Dalan — Iran server runbook

How to set up the production server, deploy, back up and run the shutdown drill. Rules: `OPS-09` to `OPS-18`, `DEL-04` to `DEL-08`. Facts behind it: `docs/DESIGN.md` section 14.

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
# Before SMS.ir is ready: login codes go to the server log, only for ADMIN_PHONES.
ADMIN_PHONES=<your mobile; several are comma separated> SMS=log bash deploy/install.sh root@<server> dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
# Read a code on the server:
journalctl -u dalan | grep "login code"
# SMS.ir (the API key from the panel, "توسعه‌دهندگان"). read -s keeps the key out of the shell history:
read -rs SMSIR_KEY && export SMSIR_KEY
SMS=smsir bash deploy/install.sh root@<server> dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
```

The number of the approved OTP template (SMS.ir panel, "ارسال سریع") goes in the admin panel as `Notification:SmsIr:TemplateId`; the code variable of the template text is `Notification:SmsIr:CodeParameter` (start `CODE`, for `#CODE#`). Until SMS.ir approves the template every send fails with SMS.ir status 113 in the log.

The SMS must end with the line `@dalanet.ir #123456` (the site domain, alone on the line), so Android and iPhones read the code by themselves (CLI-59). SMS.ir takes `#CODE#` as the variable, so that line needs one more `#`: `##CODE#`. The approved template (2026-11-24, checked on Android and iPhone):

```
کد ورود به دالان: #CODE#

@dalanet.ir ##CODE#
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

Reverse DNS (PTR) is not needed: ours stays the provider default (`hosted-by.webdade.cloud`) and login emails still reach the Gmail inbox (2026-11-22). Only if a mail provider rejects us, set the PTR of the server address to `dalanet.ir` in the server provider's panel.

Check:

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

4. Demo links (`dalan demo <port>`, `https://dalans.ir/<id>/` for 24 hours): on by themselves with the sites domain. Setup writes `Sites__DemoHost` (the same as `SITES_DOMAIN`) into `domains.env`; it needs an `A` record for the bare `dalans.ir`. Caddy gets the certificate of the bare host on its first visit, after Dalan says yes (SITE-17).

Limit: Let's Encrypt gives about 50 new certificates per registered domain per week, so about 50 new site names a week. After buying the sites domain, ask for it on the Public Suffix List (https://github.com/publicsuffix/list), so a site cannot set cookies for the other sites. Before the domain is ready, test with sslip.io: `SITES_DOMAIN=203-0-113-10.sslip.io` gives `name.203-0-113-10.sslip.io` (it needs foreign DNS, so not for the drill).

The agent files come with each deploy (`/opt/dalan/current/agent`, DEL-08); `/downloads/<file>` serves them.

### One certificate for every site (OPS-18, optional)

Without it each site gets its own certificate on its first visit, so in an internet shutdown a new site has no HTTPS. With it the server keeps one Let's Encrypt certificate for `*.<sites domain>`, made with a DNS challenge at ArvanCloud by lego, and Caddy uses it for every site host, also new ones, with no question to anyone.

1. ArvanCloud panel, the account that holds the sites domain (done for `dalans.ir`, 2026-11-28):
   1. IAM (user management): make a **machine user**, for example `dalan`, and make an API key for it. Copy the key; Arvan shows it once.
   2. IAM, **resource management** (access rules): give the machine user DNS administrator access **on the resource `dalans.ir`** (only DNS). A role on the user alone is not enough: then Arvan answers 403 "access restricted" and lego fails with `dns01: error presenting token`.
2. On your computer, update the repository first: `git checkout main && git pull`. A detached HEAD or an old branch has an old `install.sh` with no wildcard part (then the server has no `dalan-cert` unit). Check: `grep -c ARVAN_API_KEY deploy/install.sh` prints more than 0. Then run `install.sh` once with the key, written exactly as ArvanCloud shows it: `Apikey <key>`. `install.sh` downloads lego (`LEGO_VERSION`, default 5.8.0) on your computer, checks its SHA-256 and copies it; the key goes in a file, never on a command line. Both settings go before `bash` on the same line. The run prints `lego <version>` and `Wildcard certificate of the sites (OPS-18)`.

   ```bash
   SITES_DOMAIN=dalans.ir ARVAN_API_KEY='Apikey xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx' \
     bash deploy/install.sh root@<server> dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
   ```

   Optional `CERT_EMAIL=<you@example.com>`: the Let's Encrypt account email. Later runs need no key: the server keeps it. The last line of setup ("Next: the first deploy") is the same on every run; a server that already runs needs no deploy.
3. Check on the server (setup starts the first run at once):

   ```bash
   journalctl -u dalan-cert -n 40 --no-pager      # the full lines; the error is often at the end of a long line
   ls -l /etc/caddy/certs /etc/caddy/wildcard.caddy
   ```

   After a fix in Arvan, run it again: `sudo systemctl start dalan-cert`. A new key: run `install.sh` again with it.

How it works: the key is in `/etc/dalan/arvan.env` (root, 600), read only by `dalan-cert.service`. `dalan-cert.timer` runs `/opt/dalan/tools/wildcard-cert.sh` every day at about 04:17 Tehran time (a missed run runs at boot): `lego run --dns arvancloud -d '*.<sites domain>' --renew-days 30 ...` in `/var/lib/dalan-cert`. Only when lego made or renewed the certificate, its deploy hook copies it to `/etc/caddy/certs` (key `root:dalan-cert 640`; Caddy is in that group), writes the site block `/etc/caddy/wildcard.caddy` (the Caddyfile imports `wildcard*.caddy`) and restarts Caddy. Run it now: `sudo systemctl start dalan-cert`.

In a shutdown: lego cannot reach Let's Encrypt or ArvanCloud's API, so renewal fails (the journal says so) and the certificate stays in use. It renews at 30 days left, so a shutdown can last 30 to 90 days before HTTPS of the sites ends. The bare sites domain (demo links) is not in `*.<sites domain>`: it still gets its own certificate.

### A new computer for the owner

Only `install.sh`, `scripts/ci.sh` (the tests, docs/TESTING.md) and SSH need your own computer; the server builds main itself. On the new computer (Linux):

1. `git clone https://github.com/bezzad/Dalan && cd Dalan`.
2. Your own SSH key: `ssh-keygen -t ed25519`, then put `~/.ssh/id_ed25519.pub` on the server. From the old computer: `ssh-copy-id -i <new key>.pub root@<server>`; with no old computer, paste it into `/root/.ssh/authorized_keys` from the provider's web console. Check: `ssh root@<server>` asks no password.
3. The CI public key, which `install.sh` needs: `ssh root@<server> cat /home/deploy/.ssh/authorized_keys > ~/.ssh/dalan-deploy.pub`. The CI private key stays only in the GitHub secret; if it is lost or leaked, make a new pair (section 1, step 5), run `install.sh` with the new `.pub`, and put the new private key in `DEPLOY_SSH_KEY`.
4. Run `install.sh` again with the same settings when needed. `dalan.env` on the server keeps every secret, so nothing else moves with you.

A bot you run at home (for example ArcaneBot) needs only its token and `https://api.<domain>`; the site page `/guide` has the steps.

## 3. Deploy

The server builds `main` itself (OPS-16, next section): merge a pull request after `scripts/ci.sh` passed on your computer, and the new release runs a few minutes later. GitHub CI does not run by itself any more (its minutes ran out, 2026-11-21).

A deploy restarts Dalan. On a stop, live streams, long polls and site tunnels end at once (tunnels close with status 1001 and the agent reconnects), so the server exits within `Dalan:StopGraceSeconds` (5; OPS-03) and the gap is that plus the start, a few seconds. `Dalan:ShutdownTimeoutSeconds` (30) is only the limit for a normal request still running.

Fallbacks, both over SSH with `deploy/deploy.sh`: it sends the release with `rsync` to a new folder (only the parts that differ from the current release go over the network), checks `/ready` after the switch and rolls back by itself. It opens one SSH connection with up to 5 tries of 30 seconds (`SSH_TRIES`, `SSH_TIMEOUT`), because the link to Iran stalls now and then.

- From your computer, when the server cannot reach GitHub: `bash scripts/build-release.sh out`, then `DEPLOY_HOST=root@<server> SYSTEMCTL=systemctl bash deploy/deploy.sh out manual-<name>` and `ssh root@<server> chown -R deploy:deploy /opt/dalan/releases`.
- A CI run by hand: Actions, workflow `ci`, "Run workflow" on `main`, tick "Deploy". It is the only way to build the Windows and macOS agents (`dalan-windows-x64.exe`, `dalan-macos-arm64`, `dalan-macos-x64`, DEL-08): run it after a change in `agent/`, or `gh workflow run ci --repo bezzad/Dalan --ref main -f deploy=true`. The server's own builds keep those files. It needs these secrets in GitHub, repository Settings, Environments, `production`:

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

The key of "Login with Dalan" (AUTH-42) is `Identity__OAuthSigningKey` in `dalan.env` (setup.sh makes it once). A server set up before milestone 39 makes it itself at the first start in `/var/lib/dalan/oauth-signing-key` (mode 600). Keep a copy of that file or line with `dalan.env`, and never change it: each site's user ids come from it.

### The server builds main itself (OPS-16)

The main way a release reaches the server (DEL-06). The server needs GitHub, but nothing else from abroad after the first build (NuGet and npm packages stay in `/home/deploy`).

1. On your computer: `PULL_DEPLOY=1 SITES_DOMAIN=dalans.ir bash deploy/install.sh root@<server> dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub`. It downloads the .NET SDK (the version in `global.json`, newest patch) and Node (the major version in `ci.yml`) here, about 300 MB, and copies them; the server downloads neither. They stay in `~/.cache/dalan-install` (`DALAN_CACHE`), so a later run downloads only a new version. Give `PULL_DEPLOY=1` again only to update them.
2. Setup makes the GitHub key and prints it. Add it to GitHub read-only (below).
3. `dalan-pull.timer` runs every minute. Each run asks GitHub only for `refs/heads/main` (`git ls-remote`, small); no other branch is ever fetched or built. A new commit is fetched, built in a new folder (about 5 minutes, low priority), smoke-tested on `/health` (no database), and deployed with `deploy.sh`: switch, restart, `/ready`, rollback when not ready. Only the last 5 releases are kept. A lock keeps two runs from overlapping. Watch: `journalctl -u dalan-pull -f`. Start a run now: `sudo systemctl start dalan-pull`. The script setup installed (`/opt/dalan/tools/pull-deploy.sh`) only fetches; then the commit's own `deploy/pull-deploy.sh` builds and deploys it, so a change to that script needs no new setup. An installed copy from before this passes no version; `deploy.sh` (from the commit) then works it out itself (`deploy/version.sh`).

The live version (OPS-17) is `v<major>.<minor>.<n>`: `VERSION` in the repository holds `<major>.<minor>` (raise it for a bigger step) and `<n>` counts the commits of main since it changed, so every update counts up. The 12-character commit is next to it: `curl -s http://127.0.0.1:5000/health`, or the small line at the bottom of every public page. `releases/<sha>/version.env` holds both.

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

1. Optional allow list `/etc/dalan/drill-allow.txt`, one range per line, for the domestic network (for example the Iranian ranges from ipdeny.com, `ir-aggregated.zone`). Without it even SMS.ir and DNS are blocked, which shows what SMS login needs.
2. `sudo /opt/dalan/tools/drill.sh start`. It stops by itself after 60 minutes (`DRILL_MINUTES` in `/etc/dalan/drill.env`).
3. By hand, on a phone on Iranian mobile data with no VPN: open `https://dalanet.ir`, log in by SMS, open ArcaneBot, send a message, press buttons, get replies. ArcaneBot runs on its home machine with only two changes: server address `https://api.dalanet.ir` and the token from "My bots".
4. `sudo /opt/dalan/tools/drill.sh stop`. It prints each blocked address and port. Expected: push services (Google, Apple, Mozilla, Microsoft) and Let's Encrypt (also from `dalan-cert`, OPS-18, if it ran during the drill). Anything else is a bug to fix.

What the drill does not show: in a real shutdown certificates cannot be renewed (renew early; with OPS-18 new sites keep HTTPS until the wildcard certificate ends), push stops (sync still works), and in a total blackout nothing works.
