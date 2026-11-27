# operations Specification

## Purpose
Defines how the running Dalan server reports its health, starts, stops, logs and reads settings, so it can run under systemd or any container platform.

## Requirements

### Requirement: OPS-01 Liveness endpoint
The server SHALL answer `GET /health` with HTTP 200 and a JSON body with `status` set to `ok` and `version` set to the release version. It MUST NOT call the database or any other dependency.

#### Scenario: Server is running
- **WHEN** a client calls `GET /health`
- **THEN** the response is 200 with JSON `{"status":"ok","version":"<release version>"}`

### Requirement: OPS-02 Readiness endpoint
The server SHALL answer `GET /ready` with HTTP 200 only while it accepts traffic: after its start-up work is done (database schema applied, bot queues reloaded) and before it starts to stop. Otherwise it SHALL answer 503.

#### Scenario: Server is ready
- **WHEN** the server has started and a client calls `GET /ready`
- **THEN** the response is 200

#### Scenario: Server is stopping
- **WHEN** shutdown has started and a client calls `GET /ready`
- **THEN** the response is 503

#### Scenario: Database is not reachable at start
- **GIVEN** the server started and cannot reach the database
- **WHEN** a client calls `GET /ready` and `GET /health`
- **THEN** `/ready` answers 503 and `/health` answers 200

### Requirement: OPS-03 Graceful shutdown
On a stop signal the server SHALL stop taking new connections, let running requests finish, and exit within the configured shutdown timeout (`Dalan:ShutdownTimeoutSeconds`). Long-lived connections (the live stream, a bot long poll, a site tunnel, an agent long poll) and background workers SHALL end at once on the stop signal, so that a server with no other running request exits within `Dalan:StopGraceSeconds` (start 5). A site tunnel SHALL close with WebSocket status 1001.

#### Scenario: Request in flight during stop
- **GIVEN** a request is running
- **WHEN** the server gets a stop signal
- **THEN** the request completes and the server exits within the shutdown timeout

#### Scenario: Long-lived connections do not hold the stop
- **GIVEN** a user has an open live stream, a bot waits in a long poll, and an agent has an open site tunnel
- **WHEN** the server gets a stop signal
- **THEN** the stream ends, the long poll answers, the tunnel closes with status 1001, and the server exits within `Dalan:StopGraceSeconds`

#### Scenario: Deploy gap
- **WHEN** a deploy restarts the server while bots and users are connected
- **THEN** the server refuses connections for at most `Dalan:StopGraceSeconds` plus its start time, not for `Dalan:ShutdownTimeoutSeconds`

### Requirement: OPS-04 Structured logs on stdout
The server SHALL write logs to stdout as one JSON object per line, with time, level, category and message.

#### Scenario: A log line is written
- **WHEN** the server logs a message
- **THEN** stdout gets one line that parses as JSON with `Timestamp`, `LogLevel`, `Category` and `Message`

### Requirement: OPS-05 Settings from files and environment
The server SHALL read settings from its settings file and from environment variables, and an environment variable SHALL win over the file. Secrets MUST NOT be stored in the repository.

#### Scenario: Environment overrides file
- **GIVEN** the settings file sets `Dalan:Version` to `dev`
- **WHEN** the environment variable `Dalan__Version` is `env`
- **THEN** `GET /health` reports version `env`

### Requirement: OPS-06 Probes are private
`/health` and `/ready` SHALL NOT be reachable through the public front proxy; only the server itself and the deploy script on it use them.

#### Scenario: Public call to a probe
- **WHEN** a client on the internet calls `https://<site domain>/health`
- **THEN** the front proxy answers 404 and does not forward the call

### Requirement: OPS-07 Schema is applied at start
At start each service SHALL apply its own database migrations, in its own schema. While the database is not reachable the server SHALL keep running and try again every `Dalan:StartupRetrySeconds` (start 5).

#### Scenario: Empty database
- **WHEN** the server starts on an empty database
- **THEN** the schemas `identity`, `relay` and `admin` exist, each with its own migration history table, and `/ready` answers 200

#### Scenario: Start again
- **WHEN** the server starts on a database that already has the newest schema
- **THEN** it becomes ready and the data is unchanged

### Requirement: OPS-08 No traffic before ready
Until the start-up work is done, every request other than `/health` and `/ready` SHALL get HTTP 503 with error code `not_ready`.

#### Scenario: Call during start-up
- **GIVEN** the server started and cannot reach the database
- **WHEN** a bot calls `GET /v1/updates`
- **THEN** the response is 503 with error code `not_ready`

### Requirement: OPS-14 GitHub login is set up by the install script
With `GITHUB_CLIENT_ID` and `GITHUB_CLIENT_SECRET` in its environment, `deploy/install.sh` SHALL pass them to `setup.sh` in its settings file (mode 600), never on a command line. `setup.sh` SHALL then set `Identity__GitHub__ClientId`, `Identity__GitHub__ClientSecret` and `Identity__GitHub__CallbackUrl` (`https://<site>/api/auth/github/callback`) in `dalan.env`, keep every other line, and restart `dalan` if it runs. Without them `dalan.env` SHALL NOT change.

#### Scenario: New values
- **GIVEN** a server set up before, with a `dalan.env` that has other secrets
- **WHEN** `setup.sh` runs with settings holding id `abc` and secret `s3`
- **THEN** `dalan.env` has `Identity__GitHub__ClientId=abc`, `Identity__GitHub__ClientSecret=s3` and the callback URL of the site, once each, and the other lines are the same

#### Scenario: No values
- **WHEN** `setup.sh` runs with no GitHub settings
- **THEN** `dalan.env` does not change

#### Scenario: Not on the command line
- **WHEN** `install.sh` runs with `GITHUB_CLIENT_SECRET` set
- **THEN** no `ssh` or `scp` command line holds the secret, and the file sent holds it

### Requirement: OPS-16 The server builds main itself
With `PULL_DEPLOY=1`, setup SHALL install the .NET SDK, Node, a read-only GitHub key (port 443, pinned host keys) and `dalan-pull.timer`, every minute. Each run SHALL ask GitHub only for `refs/heads/main`, and build and deploy each new commit once, by the commit's own `deploy/pull-deploy.sh`: `/health` before the switch, `/ready` after it with rollback. A failed build or deploy SHALL keep the running release and its Windows agent file, and SHALL NOT be tried again.

#### Scenario: A changed pull script
- **GIVEN** a commit that changes `deploy/pull-deploy.sh`
- **WHEN** the installed older copy runs
- **THEN** the commit's own copy builds and deploys it

#### Scenario: New main
- **GIVEN** a new commit on main
- **WHEN** the timer runs twice
- **THEN** the commit is built and deployed once, and the Windows agent file stays

#### Scenario: Nothing new
- **GIVEN** main is the running release
- **WHEN** the timer runs
- **THEN** it asks GitHub only for `refs/heads/main` and fetches nothing

#### Scenario: Another branch
- **WHEN** a commit is pushed to a branch other than main
- **THEN** the server never builds it

#### Scenario: Broken build
- **WHEN** the build of the new commit fails
- **THEN** the running release stays, and the next run does not build it again

#### Scenario: GitHub does not answer
- **WHEN** `git ls-remote` fails three times
- **THEN** the run fails, the running release stays, and the next run tries again

#### Scenario: Not ready after the switch
- **WHEN** the deploy of the new commit rolls back
- **THEN** the reason is in the journal, and the next run does not deploy it again

#### Scenario: Two runs at once
- **GIVEN** a run is still building
- **WHEN** the timer starts another run
- **THEN** the second run stops at once and changes nothing

### Requirement: OPS-15 Front proxy for sites and the tunnel
The front proxy SHALL serve the tunnel host (`DALAN_TUNNEL_DOMAIN`, default `tunnel.<site domain>`) like the site domain. Every other HTTPS host SHALL get its certificate on its first visit, only after Dalan says yes (SITE-17), and SHALL go to Dalan with the client's real address and no blocked path. Each `setup.sh` run SHALL write the tunnel host, `Sites__TunnelUrl`, `Sites__Domain` and `Sites__DemoHost` (both `SITES_DOMAIN`; empty: off) into `domains.env`.

#### Scenario: Unknown host
- **WHEN** a client opens `https://shop.<sites domain>` and Dalan has no site `shop`
- **THEN** the proxy asked Dalan and gets no certificate, so the connection fails

#### Scenario: Sites domain from install.sh
- **WHEN** the owner runs `SITES_DOMAIN=dalancloud.ir deploy/install.sh ...`
- **THEN** `domains.env` has `Sites__Domain=dalancloud.ir` and `Sites__DemoHost=dalancloud.ir`, and setup prints the DNS records `dalancloud.ir` and `*.dalancloud.ir`

### Requirement: OPS-09 One-command server setup
`deploy/setup.sh` SHALL prepare a fresh Ubuntu server for the first deploy: the `dalan` and `deploy` users, PostgreSQL with a `dalan` role and database, Caddy and the systemd units. The `deploy` user MAY only restart the `dalan` service with root rights. Running it again SHALL succeed. The server MUST NOT download anything from a foreign host for it.

#### Scenario: Fresh server
- **WHEN** `setup.sh` runs on a fresh server
- **THEN** the `dalan`, `caddy` and backup units are installed and enabled, and Caddy gets the domains

#### Scenario: Small server
- **GIVEN** a server with less memory than `SWAP_MB` (start 2048) and no swap
- **WHEN** `setup.sh` runs, and runs again
- **THEN** a swap file of `SWAP_MB` is made once and kept after a reboot

#### Scenario: New domains
- **GIVEN** the server was set up for other domains
- **WHEN** `setup.sh` runs with new domains
- **THEN** Caddy and the server's links use the new domains, and `dalan.env` is unchanged

#### Scenario: Deploy user rights
- **WHEN** `setup.sh` is done
- **THEN** the sudoers file lets `deploy` run only `systemctl restart dalan`

### Requirement: OPS-10 Server secrets are made once
`setup.sh` SHALL write `/etc/dalan/dalan.env` (mode 640) with a random database password, a signing key and a push key pair when the file does not exist. It MUST NOT change these secrets later, unless `install.sh` gets `ROTATE_SECRETS=1`. The settings given to `install.sh` SHALL replace their lines on each run. It MAY add the own-mail lines of OPS-13 at the end.

#### Scenario: First run
- **WHEN** `setup.sh` runs on a server without `/etc/dalan/dalan.env`
- **THEN** the file exists with mode 640, a database connection, a signing key and a push key pair

#### Scenario: Second run
- **GIVEN** `/etc/dalan/dalan.env` exists and the owner added the SMS key to it
- **WHEN** `setup.sh` runs again
- **THEN** the file is unchanged and the script succeeds

#### Scenario: Settings from install.sh
- **GIVEN** `/etc/dalan/dalan.env` exists
- **WHEN** `install.sh` runs with `ADMIN_PHONES` and `SMS=log`, and later with `SMS=smsir` and `SMSIR_KEY`
- **THEN** each setting is in the file once, the log sender sends only to `ADMIN_PHONES`, the log sender numbers are removed for SMS.ir, the secrets are unchanged, and an unknown `SMS` value stops the script

#### Scenario: Secrets made again
- **GIVEN** the secrets were seen by someone else
- **WHEN** `install.sh` runs with `ROTATE_SECRETS=1`
- **THEN** the database password, signing key and push keys in `dalan.env` are new, the database role has the new password, and the other settings stay

### Requirement: OPS-11 Daily database backup
The server SHALL dump the database once a day to `/var/backups/dalan` and delete dumps older than `BACKUP_KEEP_DAYS` (start 14).

#### Scenario: Old dumps
- **GIVEN** dumps from 20 days ago and from yesterday
- **WHEN** the backup runs with `BACKUP_KEEP_DAYS=14`
- **THEN** today's dump exists, yesterday's is kept and the 20-day-old one is deleted

#### Scenario: Dump fails
- **WHEN** `pg_dump` fails
- **THEN** the backup exits with an error, leaves no partial dump and deletes nothing

### Requirement: OPS-12 Shutdown drill
`deploy/drill.sh start` SHALL block every new outbound connection from the server except to loopback, private ranges and the ranges in `/etc/dalan/drill-allow.txt`, and SHALL NOT block inbound connections or replies. It SHALL end by itself after `DRILL_MINUTES` (start 60). `drill.sh stop` SHALL remove the block and list each blocked address once.

#### Scenario: Drill on
- **WHEN** the owner runs `drill.sh start` with an allow list
- **THEN** outbound to the listed ranges and private ranges is allowed, other outbound is logged and dropped, replies pass, and a stop is scheduled after `DRILL_MINUTES`

#### Scenario: Drill off
- **GIVEN** the drill blocked calls to two foreign addresses
- **WHEN** the owner runs `drill.sh stop`
- **THEN** the block is removed and the two addresses are printed once each

### Requirement: OPS-13 Own mail for login codes
`setup.sh` SHALL install Postfix that sends only, listens only on loopback and signs mail with DKIM (OpenDKIM, selector `dalan`, key made once). When `dalan.env` has no `Notification__Smtp__Host` line, it SHALL add lines so Dalan sends from `no-reply@<site domain>` through it. It SHALL print the SPF, DKIM and DMARC records for the domain.

#### Scenario: Mail on a new server
- **WHEN** `setup.sh` runs
- **THEN** Postfix listens only on loopback with the site domain as its name, OpenDKIM signs for the site domain, and `dalan.env` sends email by SMTP to `127.0.0.1:25` from `no-reply@<site domain>`

#### Scenario: Server made before own mail
- **GIVEN** a `dalan.env` without an SMTP host
- **WHEN** `setup.sh` runs, and runs again
- **THEN** the mail lines are added once and the old lines stay the same

#### Scenario: Key made once
- **GIVEN** the DKIM key exists
- **WHEN** `setup.sh` runs again
- **THEN** the key is not made again and the same DKIM record is printed

### Requirement: OPS-17 A version that counts up
`VERSION` at the root of the repository SHALL hold `<major>.<minor>` (start `1.1`; the owner raises it). A release of main (OPS-16) SHALL have `Dalan__Version=v<major>.<minor>.<n>` in `version.env`, `<n>` being the commits on main since `VERSION` changed, and `Dalan__Commit=<12 characters of the sha>`. `/health` (private, OPS-06) SHALL give both. The release folder SHALL keep the sha as its name.

#### Scenario: A new VERSION
- **GIVEN** the commit of main that set `VERSION` to `1.1`
- **WHEN** the server builds and deploys it
- **THEN** the version is `v1.1.0`, and the release folder is named by the sha

#### Scenario: Every update counts up
- **GIVEN** the server runs `v1.1.0`
- **WHEN** the next commit of main is deployed
- **THEN** the version is `v1.1.1`

#### Scenario: Local run
- **WHEN** Dalan runs with no `version.env`
- **THEN** `/health` gives `version` `dev` and `commit` ""
