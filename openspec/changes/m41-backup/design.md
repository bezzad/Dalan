# m41 design

## Archive
`/var/backups/dalan/dalan-<UTC yyyy-mm-dd-HHMMSS>.tar.gz`, mode 640, owner root, group dalan (the server reads it for
the download). Inside:

| Path in the archive | From |
|---|---|
| `dalan.dump` | `pg_dump -Fc --no-owner --no-privileges dalan` |
| `etc/dalan/dalan.env`, `arvan.env`, `backup.env` | secrets and settings (`domains.env` is not taken: setup writes it) |
| `var/lib/dalan/` | chat files and `oauth-signing-key` |
| `etc/dkimkeys/` | the DKIM key (the DNS record stays right) |
| `var/lib/dalan-cert/` | lego's account and the wildcard certificate |
| `backup.txt` | when it was made and the release |

## Folders
| Folder | Owner, mode | What |
|---|---|---|
| `/var/backups/dalan` | root:dalan 750 | archives (root writes), `backup-status`, `restore-status` |
| `/var/backups/dalan/uploads` | dalan:dalan 700 | archives the admin uploaded |
| `/var/backups/dalan/requests` | dalan:dalan 700 | `backup` and `restore` request files |

`dalan.service` may write only `uploads` and `requests` (`ReadWritePaths`). Root never writes into a folder the dalan
user owns, so a link put there cannot make root write elsewhere.

## Requests
`dalan-backup.path` (`PathExists=.../requests/backup`) starts `dalan-backup.service`; `dalan-restore.path` starts
`dalan-restore.service` (`restore.sh --request`). Each script removes its request file first.

## Restore and a broken server process
A restore runs as root on an archive the dalan user could write. So `restore.sh`:
- copies the archive into a root-only temporary folder and refuses links or anything but plain files and folders;
- restores the database as the role `dalan` (not the superuser), into a new empty database;
- keeps from `arvan.env` and `backup.env` only their own setting lines (root units read them);
- never takes `domains.env`, units or programs from the archive.

## Settings
| Setting | Start | Admin may change |
|---|---|---|
| `Admin:BackupFolder` | `backups` (dalan.service: `/var/backups/dalan`) | no (hidden, like `Relay:FileFolder`) |
| `Admin:BackupMaxUploadMb` | 2048 | yes |
| `BACKUP_KEEP_DAYS` (`/etc/dalan/backup.env`) | 14 | on the server |
