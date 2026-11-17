# Spec Delta

## ADDED Requirements

### Requirement: OPS-09 One-command server setup
`deploy/setup.sh` SHALL prepare a fresh Ubuntu server for the first deploy: the `dalan` and `deploy` users, PostgreSQL with a `dalan` role and database, Caddy and the systemd units. The `deploy` user MAY only restart the `dalan` service with root rights. Running it again SHALL succeed. The server MUST NOT download anything from a foreign host for it.

#### Scenario: Fresh server
- **WHEN** `setup.sh` runs on a fresh server
- **THEN** the `dalan`, `caddy` and backup units are installed and enabled, and Caddy gets the domains

#### Scenario: Deploy user rights
- **WHEN** `setup.sh` is done
- **THEN** the sudoers file lets `deploy` run only `systemctl restart dalan`

### Requirement: OPS-10 Server secrets are made once
`setup.sh` SHALL write `/etc/dalan/dalan.env` (mode 640) with a random database password, a signing key and a push key pair only when the file does not exist. It MUST NOT change an existing file.

#### Scenario: First run
- **WHEN** `setup.sh` runs on a server without `/etc/dalan/dalan.env`
- **THEN** the file exists with mode 640, a database connection, a signing key and a push key pair

#### Scenario: Second run
- **GIVEN** `/etc/dalan/dalan.env` exists and the owner added the SMS key to it
- **WHEN** `setup.sh` runs again
- **THEN** the file is unchanged and the script succeeds

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
