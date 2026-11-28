## RENAMED Requirements
- FROM: `### Requirement: OPS-11 Daily database backup`
- TO: `### Requirement: OPS-11 Daily backup of the server`

## MODIFIED Requirements

### Requirement: OPS-11 Daily backup of the server
The server SHALL make one archive `dalan-<UTC date and time>.tar.gz` in `/var/backups/dalan` once a day, mode 640, owner root, group dalan. It SHALL hold the database dump, `dalan.env`, `arvan.env`, `backup.env` (each one that exists), `/var/lib/dalan`, the DKIM key and the wildcard certificate state, and NOT `domains.env`. It SHALL delete archives, uploads and old `.dump` files older than `BACKUP_KEEP_DAYS` (start 14), and write the result to `backup-status`.

#### Scenario: Old dumps
- **GIVEN** an archive and a dump from 20 days ago and an archive from yesterday
- **WHEN** the backup runs with `BACKUP_KEEP_DAYS=14`
- **THEN** a new archive exists with mode 640, yesterday's is kept and the 20-day-old files are deleted

#### Scenario: What is inside
- **WHEN** the backup runs on a server with `dalan.env`, `domains.env`, the "Login with Dalan" key and a chat file
- **THEN** the archive has the dump, `dalan.env`, the key and the chat file, and no `domains.env`

#### Scenario: Dump fails
- **WHEN** `pg_dump` fails
- **THEN** the backup exits with an error, leaves no partial archive, deletes nothing and `backup-status` says it failed

## ADDED Requirements

### Requirement: OPS-19 Restore from a backup
`deploy/restore.sh <archive>` SHALL, as root, stop `dalan`, restore the database into a new empty database as the role `dalan` (not the superuser) with the restored `dalan.env` password, put back `dalan.env`, `/var/lib/dalan`, the DKIM key and the certificate state, keep `domains.env`, start `dalan` and write `restore-status`. It SHALL refuse an archive with a link, a special file or no dump, changing nothing, and take from `arvan.env` and `backup.env` only their own setting lines.

#### Scenario: Move to a new server
- **GIVEN** a new server made by `setup.sh`, with its own secrets, and an archive of the old server
- **WHEN** `restore.sh` runs with the archive
- **THEN** `dalan.env`, the "Login with Dalan" key and the chat files are the old server's, the database was restored as role `dalan` with the old password, `domains.env` is the new server's and `dalan` runs

#### Scenario: A bad archive
- **WHEN** the archive holds a link, or `arvan.env` has a line `LD_PRELOAD=/x`
- **THEN** a link refuses the restore and changes nothing; the `LD_PRELOAD` line is not written

### Requirement: OPS-20 Backup and restore on request
`setup.sh` SHALL make `/var/backups/dalan` (root:dalan 750) with `uploads` and `requests` (dalan:dalan 700), install `restore.sh` and the units `dalan-backup.path` and `dalan-restore.path`. A file `requests/backup` SHALL start the backup; a file `requests/restore` holding an archive name SHALL start `restore.sh` with that archive from `/var/backups/dalan` or `uploads`. Each script SHALL remove its request file first. `dalan.service` SHALL be able to write only `uploads` and `requests` there.

#### Scenario: The dashboard asks for a restore
- **GIVEN** `requests/restore` holds `dalan-upload-2026-11-28-101500.tar.gz`
- **WHEN** `restore.sh --request` runs
- **THEN** the request file is gone and that upload is restored; a name with `/` or `..` is refused
