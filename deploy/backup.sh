#!/usr/bin/env bash
# Dump the database and delete old dumps (OPS-11). Runs as user postgres from dalan-backup.service.
# Settings (environment): BACKUP_DIR (default /var/backups/dalan), BACKUP_KEEP_DAYS (default 14), BACKUP_DATABASE (default dalan)
set -euo pipefail
dir="${BACKUP_DIR:-/var/backups/dalan}"
keep="${BACKUP_KEEP_DAYS:-14}"
database="${BACKUP_DATABASE:-dalan}"

file="$dir/dalan-$(date -u +%Y-%m-%d).dump"
# Write to a temporary name first, so a failed dump leaves nothing that looks like a backup.
pg_dump -Fc -f "$file.tmp" "$database" || { rm -f "$file.tmp"; echo "pg_dump failed" >&2; exit 1; }
mv -f "$file.tmp" "$file"
find "$dir" -maxdepth 1 -name 'dalan-*.dump' -mtime "+$keep" -delete
echo "Backup $file is done."
