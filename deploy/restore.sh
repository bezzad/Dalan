#!/usr/bin/env bash
# OPS-19: put a backup of backup.sh back on a server made by setup.sh: this one, or a new one when the server moves.
# Usage (as root):
#   restore.sh <archive>     a path, or the name of an archive in /var/backups/dalan or its uploads
#   restore.sh --request     dalan-restore.service: the name is in /var/backups/dalan/requests/restore (OPS-20, the dashboard)
# The archive may come from the dashboard, so it is not trusted: it is opened in root's own temporary folder, links and
# special files refuse it, the database goes back as the role dalan (not the superuser), and from the files that root
# units read (arvan.env, backup.env) only their own setting lines are taken. Units, programs and domains.env never come
# from it. Everyone, the admin too, logs in again after it: the sessions' key is the old server's.
# Messages: journalctl -u dalan-restore. The result is also in /var/backups/dalan/restore-status.
# Settings (environment): BACKUP_DIR (default /var/backups/dalan), BACKUP_DATABASE (default dalan)
#   DALAN_ROOT   for tests only (default empty, the real /)
set -euo pipefail
root="${DALAN_ROOT:-}"
dir="${BACKUP_DIR:-$root/var/backups/dalan}"
database="${BACKUP_DATABASE:-dalan}"
env_file="$root/etc/dalan/dalan.env"

status() {
  printf '%s %s %s\n' "$1" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$2" > "$dir/restore-status.tmp"
  chmod 644 "$dir/restore-status.tmp"
  mv -f "$dir/restore-status.tmp" "$dir/restore-status"
}
stopped=
fail() {
  status failed "$name: $1"
  echo "Restore of $name failed: $1" >&2
  # Before the database switch nothing changed: the server runs on as it was.
  [ -z "$stopped" ] || systemctl start dalan.service || true
  exit 1
}
name_ok() { [[ "$1" =~ ^dalan-[A-Za-z0-9-]+\.tar\.gz$ ]]; }
as_postgres() { if [ "$(id -u)" = 0 ] && [ -z "$root" ]; then runuser -u postgres -- "$@"; else "$@"; fi; }
password_of() { grep -m1 '^ConnectionStrings__Dalan=' "$1" | grep -o 'Password=[^;]*' | cut -d= -f2- || true; }

name="${1:?usage: restore.sh <archive> | --request}"
if [ "$name" = --request ]; then
  # The dalan user writes this folder: read the request only when it is a plain file, and only its first line.
  request="$dir/requests/restore"
  name=request
  [ -f "$request" ] && [ ! -L "$request" ] || { rm -f "$request"; fail "no restore request"; }
  name=$(head -c 200 "$request" | head -n 1 | tr -d '\r\n')
  rm -f "$request"
  name_ok "$name" || fail "not an archive name"
fi
if name_ok "$name" && [ -e "$dir/$name" ]; then archive="$dir/$name"
elif name_ok "$name"; then archive="$dir/uploads/$name"
else archive="$name"; name=$(basename "$name")
fi

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
# Any other failed step also says so in restore-status, and starts the server again.
trap 'fail "step at line $LINENO failed"' ERR
# A copy in root's folder: the dalan user cannot change it any more, and a link stays a link (refused).
cp -P -- "$archive" "$work/archive" 2>/dev/null || fail "no such archive"
[ -f "$work/archive" ] && [ ! -L "$work/archive" ] || fail "not a plain file"
kinds=$(tar -tvzf "$work/archive" 2>/dev/null | cut -c1 | sort -u | tr -d '\n') || fail "not a backup archive"
[[ "$kinds" =~ ^[-d]+$ ]] || fail "the archive holds a link or a special file"
x="$work/x"
mkdir "$x"
tar -xzf "$work/archive" -C "$x" --no-same-owner --no-same-permissions || fail "cannot open the archive"
[ -z "$(find "$x" ! -type f ! -type d -print -quit)" ] || fail "the archive holds a link or a special file"
[ -s "$x/dalan.dump" ] || fail "no database dump"
[ -s "$x/etc/dalan/dalan.env" ] || fail "no dalan.env"
old_password=$(password_of "$x/etc/dalan/dalan.env")
password=$(password_of "$env_file")
[[ "$old_password" =~ ^[A-Za-z0-9_-]+$ ]] || fail "dalan.env has no database password"
[ -n "$password" ] || fail "this server's dalan.env has no database password"

echo "Restoring $name"
systemctl stop dalan.service
stopped=1

# Into a new database first, as the role dalan with this server's password; the old database stays until it worked.
new="${database}_restore"
as_postgres psql -v ON_ERROR_STOP=1 -Atq -c "DROP DATABASE IF EXISTS $new" -c "CREATE DATABASE $new OWNER dalan" \
  || fail "cannot make a new database"
PGPASSWORD="$password" pg_restore --no-owner --no-privileges --exit-on-error -h localhost -U dalan -d "$new" "$x/dalan.dump" \
  || { as_postgres psql -Atq -c "DROP DATABASE IF EXISTS $new" || true; fail "the database dump does not restore"; }
as_postgres psql -v ON_ERROR_STOP=1 -Atq -c "DROP DATABASE IF EXISTS $database WITH (FORCE)" -c "ALTER DATABASE $new RENAME TO $database" \
  || fail "cannot switch to the restored database"
# The restored dalan.env holds the old server's database password; the role takes it (a psql variable: no quoting of it).
printf "ALTER ROLE dalan PASSWORD :'password';\n" | as_postgres psql -v ON_ERROR_STOP=1 -Atq -v password="$old_password" \
  || fail "cannot set the database password"

echo "Files"
cp "$x/etc/dalan/dalan.env" "$env_file"
chown root:dalan "$env_file"
chmod 640 "$env_file"
# Root units read these: only their own lines, so the archive cannot set LD_PRELOAD or the like for them.
if [ -f "$x/etc/dalan/arvan.env" ]; then
  (umask 077 && { grep -E '^(ARVANCLOUD_API_KEY|CERT_EMAIL)=' "$x/etc/dalan/arvan.env" || true; } > "$root/etc/dalan/arvan.env")
  chown root:root "$root/etc/dalan/arvan.env"
fi
if [ -f "$x/etc/dalan/backup.env" ]; then
  { grep -E '^BACKUP_KEEP_DAYS=[0-9]+$' "$x/etc/dalan/backup.env" || true; } > "$root/etc/dalan/backup.env"
  chmod 644 "$root/etc/dalan/backup.env"
fi
state="$root/var/lib/dalan"
if [ -d "$x/var/lib/dalan" ]; then
  mkdir -p "$state"
  find "$state" -mindepth 1 -delete
  cp -a "$x/var/lib/dalan/." "$state/"
  chown -R dalan:dalan "$state"
  [ ! -f "$state/oauth-signing-key" ] || chmod 600 "$state/oauth-signing-key"
fi
if [ -f "$x/etc/dkimkeys/dalan.private" ]; then
  mkdir -p "$root/etc/dkimkeys"
  cp "$x/etc/dkimkeys/"dalan.* "$root/etc/dkimkeys/"
  chown -R opendkim:opendkim "$root/etc/dkimkeys"
  chmod 700 "$root/etc/dkimkeys"
  chmod 600 "$root/etc/dkimkeys/dalan.private"
  systemctl restart opendkim.service || echo "opendkim did not restart" >&2
fi
if [ -d "$x/var/lib/dalan-cert" ]; then
  rm -rf "$root/var/lib/dalan-cert"
  cp -a "$x/var/lib/dalan-cert" "$root/var/lib/dalan-cert"
  chown -R root:root "$root/var/lib/dalan-cert"
  chmod 700 "$root/var/lib/dalan-cert"
  # Caddy takes the wildcard certificate when this server has the certificate setup (setup.sh with ARVAN_API_KEY).
  sites=$(grep -h '^Sites__Domain=' "$root/etc/dalan/domains.env" 2>/dev/null | cut -d= -f2- || true)
  if [ -n "$sites" ] && [ -x "$root/opt/dalan/tools/wildcard-cert.sh" ] && [ -f "$root/var/lib/dalan-cert/certificates/_.$sites.crt" ]; then
    "$root/opt/dalan/tools/wildcard-cert.sh" hook "$sites" || echo "the wildcard certificate was not taken; run install.sh again" >&2
  fi
fi

systemctl start dalan.service
stopped=
status ok "$name"
echo "Restored $name. Everyone logs in again. On a new server, run install.sh once more with the same settings."
