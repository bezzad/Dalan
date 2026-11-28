#!/usr/bin/env bash
# OPS-11: one archive of the server: the database and the files a new server needs (deploy/restore.sh puts it back).
# Runs as root from dalan-backup.service: daily (dalan-backup.timer), or when the dashboard writes requests/backup (OPS-20).
# The archive holds every secret of the server: only root and the group dalan (the download in the dashboard) read it.
# Settings (environment): BACKUP_DIR (default /var/backups/dalan), BACKUP_KEEP_DAYS (default 14), BACKUP_DATABASE (default dalan)
#   DALAN_ROOT   for tests only (default empty, the real /)
set -euo pipefail
root="${DALAN_ROOT:-}"
dir="${BACKUP_DIR:-$root/var/backups/dalan}"
keep="${BACKUP_KEEP_DAYS:-14}"
database="${BACKUP_DATABASE:-dalan}"

# The request of the dashboard is done by this run; a new one while it runs starts the next run.
rm -f "$dir/requests/backup"

# backup-status: "ok|failed <time> <text>", for the dashboard (ADM-21).
status() {
  printf '%s %s %s\n' "$1" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$2" > "$dir/backup-status.tmp"
  chmod 644 "$dir/backup-status.tmp"
  mv -f "$dir/backup-status.tmp" "$dir/backup-status"
}
fail() { status failed "$1"; echo "$1" >&2; exit 1; }
# The work folder is root's own (mktemp: mode 700) in the root folder, so nothing the dalan user writes is in its way.
work=$(mktemp -d "$dir/.work.XXXXXX")
trap 'rm -rf "$work"' EXIT
as_postgres() { if [ "$(id -u)" = 0 ] && [ -z "$root" ]; then runuser -u postgres -- "$@"; else "$@"; fi; }

# --no-owner and --no-privileges: restore.sh puts it back as the role dalan, not the superuser.
as_postgres pg_dump -Fc --no-owner --no-privileges "$database" > "$work/dalan.dump" || fail "pg_dump failed"
version=$(grep -h '^Dalan__Version=' "$root/opt/dalan/current/version.env" 2>/dev/null | cut -d= -f2- || true)
printf 'Dalan backup\nmade %s\nrelease %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "${version:-unknown}" > "$work/backup.txt"

# domains.env is not taken: setup.sh writes it for the server it runs on.
paths=()
for path in etc/dalan/dalan.env etc/dalan/arvan.env etc/dalan/backup.env var/lib/dalan etc/dkimkeys var/lib/dalan-cert; do
  [ ! -e "$root/$path" ] || paths+=("$path")
done
# Chat files may change while tar reads them (exit 1); that is still a good archive.
tar -czf "$work/archive" --warning=no-file-changed -C "$work" dalan.dump backup.txt -C "${root:-/}" "${paths[@]}" \
  || [ $? -eq 1 ] || fail "tar failed"
chown root:dalan "$work/archive"
chmod 640 "$work/archive"
name="dalan-$(date -u +%Y-%m-%d-%H%M%S).tar.gz"
mv -f "$work/archive" "$dir/$name"

find "$dir" -maxdepth 1 \( -name 'dalan-*.tar.gz' -o -name 'dalan-*.dump' \) -mtime "+$keep" -delete
[ ! -d "$dir/uploads" ] || find "$dir/uploads" -maxdepth 1 -name 'dalan-*.tar.gz' -mtime "+$keep" -delete
status ok "$name"
echo "Backup $dir/$name is done."
