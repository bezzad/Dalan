#!/usr/bin/env bash
# Tests for deploy/backup.sh and deploy/restore.sh with fake PostgreSQL tools, systemctl and chown, and a fake root folder.
set -euo pipefail
deploy="$(cd "$(dirname "$0")/../.." && pwd)/deploy"
fail() { echo "FAIL: $*"; exit 1; }
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
bin="$tmp/bin"; state="$tmp/state"
mkdir -p "$bin" "$state"
log() { printf '#!/bin/bash\necho "%s $*" >> %s/commands.log\n%s\n' "$1" "$state" "${2:-}" > "$bin/$1"; }
# Fake pg_dump: writes the dump to standard output, or fails when the file "broken" exists.
log pg_dump 'echo DUMP; [ ! -f '"$state"'/broken ]'
# Fake psql keeps what it read from standard input; fake pg_restore keeps the password it got.
log psql '[ -t 0 ] || cat >> '"$state"'/psql-input'
log pg_restore 'echo "password $PGPASSWORD" >> '"$state"'/commands.log; [ ! -f '"$state"'/restore-broken ]'
log systemctl
log chown
chmod +x "$bin"/*
export PATH="$bin:$PATH"

# A server folder: the secrets, the domains, the "Login with Dalan" key, a chat file, the DKIM key.
server() {
  local root="$1" password="$2"
  mkdir -p "$root"/{etc/dalan,etc/dkimkeys,var/lib/dalan/files,var/lib/dalan-cert/certificates,opt/dalan/current} \
    "$root"/var/backups/dalan/{uploads,requests}
  printf 'ConnectionStrings__Dalan=Host=localhost;Database=dalan;Username=dalan;Password=%s\nIdentity__SigningKey=%s-key\n' \
    "$password" "$password" > "$root/etc/dalan/dalan.env"
  echo "Sites__Domain=$password.example" > "$root/etc/dalan/domains.env"
  echo "$password-oauth" > "$root/var/lib/dalan/oauth-signing-key"
  echo "$password-dkim" > "$root/etc/dkimkeys/dalan.private"
  echo "Dalan__Version=v1.1.7" > "$root/opt/dalan/current/version.env"
}
old="$tmp/old"; server "$old" oldpw
echo "hello" > "$old/var/lib/dalan/files/chat-1"
dir="$old/var/backups/dalan"

# Rule: OPS-11 old dumps
touch -d '-20 days' "$dir/dalan-old.dump" "$dir/dalan-2026-01-01-000000.tar.gz" "$dir/uploads/dalan-upload-2026-01-01-000000.tar.gz"
touch -d '-1 day' "$dir/dalan-yesterday.tar.gz"
touch "$dir/requests/backup"
DALAN_ROOT="$old" BACKUP_KEEP_DAYS=14 bash "$deploy/backup.sh" >/dev/null || fail "backup failed"
archive=$(ls "$dir"/dalan-20[0-9][0-9]-*.tar.gz | grep -v 2026-01-01 | head -n 1)
[ -f "$archive" ] || fail "no archive for today: $(ls "$dir")"
[ "$(stat -c %a "$archive")" = 640 ] || fail "archive mode is $(stat -c %a "$archive")"
grep -q "chown root:dalan" "$state/commands.log" || fail "archive not given to the group dalan"
[ -f "$dir/dalan-yesterday.tar.gz" ] || fail "yesterday's archive deleted"
[ ! -e "$dir/dalan-old.dump" ] && [ ! -e "$dir/dalan-2026-01-01-000000.tar.gz" ] || fail "20-day-old backups kept"
[ ! -e "$dir/uploads/dalan-upload-2026-01-01-000000.tar.gz" ] || fail "20-day-old upload kept"
[ ! -e "$dir/requests/backup" ] || fail "the request was not removed"
grep -q "^ok .* $(basename "$archive")$" "$dir/backup-status" || fail "backup-status: $(cat "$dir/backup-status")"
[ -z "$(ls -A "$dir" | grep '^\.work')" ] || fail "work folder left"
grep -q 'pg_dump -Fc --no-owner --no-privileges dalan' "$state/commands.log" || fail "dump without --no-owner"
grep -q 'OnCalendar=daily' "$deploy/dalan-backup.timer" || fail "timer is not daily"
grep -q 'BACKUP_KEEP_DAYS=14' "$deploy/dalan-backup.service" || fail "unit does not start with 14 days"
echo "PASS OPS-11 keeps the newest days"

# Rule: OPS-11 what is inside
list=$(tar -tzf "$archive")
for path in dalan.dump backup.txt etc/dalan/dalan.env var/lib/dalan/oauth-signing-key var/lib/dalan/files/chat-1 etc/dkimkeys/dalan.private; do
  grep -qx "$path" <<< "$list" || fail "$path is not in the archive: $list"
done
grep -q domains.env <<< "$list" && fail "domains.env is in the archive"
tar -xzOf "$archive" backup.txt | grep -qx 'release v1.1.7' || fail "backup.txt has no release"
echo "PASS OPS-11 the archive has the database and the server's files"

# Rule: OPS-11 dump fails
before=$(ls "$dir")
touch -d '-20 days' "$dir/dalan-2026-01-02-000000.tar.gz"; touch "$state/broken"
if DALAN_ROOT="$old" bash "$deploy/backup.sh" >/dev/null 2>&1; then fail "backup exited with success"; fi
[ "$(ls "$dir" | grep -v dalan-2026-01-02)" = "$before" ] || fail "a failed backup left or deleted files: $(ls "$dir")"
[ -f "$dir/dalan-2026-01-02-000000.tar.gz" ] || fail "a failed backup deleted old archives"
grep -q '^failed .* pg_dump failed$' "$dir/backup-status" || fail "backup-status: $(cat "$dir/backup-status")"
rm "$state/broken"
echo "PASS OPS-11 failed dump leaves nothing and deletes nothing"

# Rule: OPS-19 move to a new server
new="$tmp/new"; server "$new" newpw
echo "new-only" > "$new/var/lib/dalan/files/other"
cp "$archive" "$new/var/backups/dalan/uploads/dalan-upload-2026-11-28-101500.tar.gz"
: > "$state/commands.log"
DALAN_ROOT="$new" bash "$deploy/restore.sh" dalan-upload-2026-11-28-101500.tar.gz >/dev/null 2>"$tmp/err" || { cat "$tmp/err"; fail "restore failed"; }
cmp -s "$old/etc/dalan/dalan.env" "$new/etc/dalan/dalan.env" || fail "dalan.env is not the old one"
[ "$(stat -c %a "$new/etc/dalan/dalan.env")" = 640 ] || fail "dalan.env mode"
[ "$(cat "$new/var/lib/dalan/oauth-signing-key")" = oldpw-oauth ] || fail "the Login with Dalan key is not the old one"
[ "$(stat -c %a "$new/var/lib/dalan/oauth-signing-key")" = 600 ] || fail "key mode"
[ "$(cat "$new/var/lib/dalan/files/chat-1")" = hello ] && [ ! -e "$new/var/lib/dalan/files/other" ] || fail "files are not the old ones"
[ "$(cat "$new/etc/dkimkeys/dalan.private")" = oldpw-dkim ] || fail "DKIM key is not the old one"
grep -qx 'Sites__Domain=newpw.example' "$new/etc/dalan/domains.env" || fail "domains.env changed"
log=$(cat "$state/commands.log")
grep -q 'pg_restore --no-owner --no-privileges --exit-on-error -h localhost -U dalan -d dalan_restore' <<< "$log" || fail "not restored as dalan: $log"
grep -qx 'password newpw' <<< "$log" || fail "pg_restore did not use this server's password"
grep -q 'DROP DATABASE IF EXISTS dalan WITH (FORCE) -c ALTER DATABASE dalan_restore RENAME TO dalan' <<< "$log" || fail "no database switch"
grep -q 'psql .* -v password=oldpw' <<< "$log" && grep -q "ALTER ROLE dalan PASSWORD :'password'" "$state/psql-input" || fail "role password not set"
[ "$(grep -n 'systemctl stop dalan.service' <<< "$log" | cut -d: -f1)" -lt "$(grep -n 'pg_restore' <<< "$log" | cut -d: -f1)" ] || fail "dalan not stopped first"
tail -n 1 <<< "$log" | grep -qx 'systemctl start dalan.service' || fail "dalan not started at the end"
grep -q '^ok .* dalan-upload-2026-11-28-101500.tar.gz$' "$new/var/backups/dalan/restore-status" || fail "restore-status"
echo "PASS OPS-19 a backup moves to a new server"

# Rule: OPS-19 a bad archive
bad="$tmp/bad"; mkdir -p "$bad/etc/dalan"
echo DUMP > "$bad/dalan.dump"
cp "$old/etc/dalan/dalan.env" "$bad/etc/dalan/dalan.env"
printf 'ARVANCLOUD_API_KEY="k"\nLD_PRELOAD=/x\n' > "$bad/etc/dalan/arvan.env"
printf 'BACKUP_KEEP_DAYS=7\nLD_PRELOAD=/x\n' > "$bad/etc/dalan/backup.env"
tar -czf "$tmp/env.tar.gz" -C "$bad" .
DALAN_ROOT="$new" bash "$deploy/restore.sh" "$tmp/env.tar.gz" >/dev/null 2>&1 || fail "restore with filtered lines failed"
[ "$(cat "$new/etc/dalan/arvan.env")" = 'ARVANCLOUD_API_KEY="k"' ] || fail "arvan.env: $(cat "$new/etc/dalan/arvan.env")"
[ "$(cat "$new/etc/dalan/backup.env")" = 'BACKUP_KEEP_DAYS=7' ] || fail "backup.env: $(cat "$new/etc/dalan/backup.env")"
ln -s /etc/passwd "$bad/etc/dalan/link"
tar -czf "$tmp/link.tar.gz" -C "$bad" .
cp "$new/etc/dalan/dalan.env" "$tmp/env-before"
echo changed > "$new/etc/dalan/dalan.env.marker"
: > "$state/commands.log"
if DALAN_ROOT="$new" bash "$deploy/restore.sh" "$tmp/link.tar.gz" >/dev/null 2>&1; then fail "an archive with a link was restored"; fi
grep -q 'systemctl stop\|pg_restore' "$state/commands.log" && fail "a refused archive stopped the server or touched the database"
cmp -s "$tmp/env-before" "$new/etc/dalan/dalan.env" || fail "a refused archive changed dalan.env"
grep -q '^failed .*link' "$new/var/backups/dalan/restore-status" || fail "restore-status: $(cat "$new/var/backups/dalan/restore-status")"
# A dump that does not restore: the database is not switched and the server starts again.
touch "$state/restore-broken"; : > "$state/commands.log"
if DALAN_ROOT="$new" bash "$deploy/restore.sh" "$tmp/env.tar.gz" >/dev/null 2>&1; then fail "a broken dump was restored"; fi
grep -q 'RENAME TO' "$state/commands.log" && fail "the database was switched after a failed restore"
tail -n 1 "$state/commands.log" | grep -qx 'systemctl start dalan.service' || fail "dalan not started again: $(cat "$state/commands.log")"
rm "$state/restore-broken"
echo "PASS OPS-19 a bad archive changes nothing"

# Rule: OPS-20 the dashboard asks for a restore
requests="$new/var/backups/dalan/requests"
echo dalan-upload-2026-11-28-101500.tar.gz > "$requests/restore"
: > "$state/commands.log"
DALAN_ROOT="$new" bash "$deploy/restore.sh" --request >/dev/null 2>&1 || fail "restore by request failed"
[ ! -e "$requests/restore" ] || fail "the request was not removed"
grep -q 'pg_restore' "$state/commands.log" || fail "the upload was not restored"
for name in '../../etc/shadow' 'dalan-x/../../a.tar.gz'; do
  echo "$name" > "$requests/restore"; : > "$state/commands.log"
  if DALAN_ROOT="$new" bash "$deploy/restore.sh" --request >/dev/null 2>&1; then fail "the name $name was taken"; fi
  [ ! -e "$requests/restore" ] && [ ! -s "$state/commands.log" ] || fail "a bad name was not refused at once"
done
ln -s "$tmp/secret" "$requests/restore"
if DALAN_ROOT="$new" bash "$deploy/restore.sh" --request >/dev/null 2>&1; then fail "a link as the request was read"; fi
[ ! -L "$requests/restore" ] || fail "the link request was not removed"
echo "PASS OPS-20 a restore request is done once and only by name"
