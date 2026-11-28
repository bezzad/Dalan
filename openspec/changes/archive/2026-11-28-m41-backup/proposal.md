# m41: backup and restore for the admin

## Why
The owner (2026-11-28): the admin needs a "Backup" item in the dashboard that backs up the database and everything
important, and restores it, so the server can move to another machine without trouble. Today the daily backup is only
the database: `dalan.env` (secrets), the "Login with Dalan" key, the chat files, the DKIM key and the wildcard
certificate are not in it, and a restore is done by hand.

## What changes
- OPS-11: the daily backup is one archive with the database and the server's files (`dalan.env`, `arvan.env`,
  `backup.env`, `/var/lib/dalan`, the DKIM key, the wildcard certificate). Old `.dump` files are deleted like old archives.
- OPS-19: `deploy/restore.sh <archive>` puts a backup back on a server made by `setup.sh` (same or new machine).
- OPS-20: the dashboard asks for a backup or a restore with a request file; systemd path units run the scripts as root.
- ADM-21: the admin lists, makes and downloads backups.
- ADM-22: the admin uploads a backup and restores one.
- CLI-99: the "Backup" page in the admin part of the dashboard.

## Not in scope
- Copying backups to another machine by itself (the admin downloads them).
- Encrypting the archive. It holds every secret: only the admin can download it, and the docs say to keep it safe.
