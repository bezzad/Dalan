#!/usr/bin/env bash
# Prepare a fresh Ubuntu server for the first deploy (OPS-09, OPS-10). Run as root, on the server.
# Safe to run again: it never changes an existing /etc/dalan/dalan.env.
# deploy/install.sh copies this folder and the Caddy binary to the server and runs it.
#
# Usage: setup.sh <site domain> <api domain> <deploy public key file>
# Optional settings.env next to this script (install.sh writes it): ADMIN_PHONES, SMS (log or kavenegar), KAVENEGAR_KEY.
#   They are written into dalan.env, each run. SMS=log sends login codes only to ADMIN_PHONES, into the log.
# Settings (environment):
#   SWAP_MB      default 2048. A server with less memory than this and no swap gets a swap file of this size.
#   DALAN_ROOT   for tests only (default empty, the real /); MEMINFO for tests only (default /proc/meminfo)
set -euo pipefail

site="${1:?site domain is required, for example dalanet.ir}"
api="${2:?api domain is required, for example api.dalanet.ir}"
key_file="${3:?public key file of the CI deploy user is required}"
here="$(cd "$(dirname "$0")" && pwd)"
root="${DALAN_ROOT:-}"

echo "Packages (from the Ubuntu mirror)"
apt-get update -q
# .NET needs ICU; its package name has the version in it (libicu74 on 24.04), so ask apt for the one this Ubuntu has.
icu=$(apt-cache search --names-only '^libicu[0-9]+$' | awk '{ print $1 }' | sort -V | tail -n 1)
DEBIAN_FRONTEND=noninteractive apt-get install -y -q postgresql curl nftables openssl $icu

swap_mb="${SWAP_MB:-2048}"
memory_mb=$(awk '/^MemTotal:/ { print int($2 / 1024) }' "${MEMINFO:-/proc/meminfo}")
if [ "$memory_mb" -lt "$swap_mb" ] && [ -z "$(swapon --show --noheadings)" ] && [ ! -f "$root/swapfile" ]; then
  echo "Swap file of $swap_mb MB (the server has $memory_mb MB of memory)"
  fallocate -l "${swap_mb}M" "$root/swapfile"
  chmod 600 "$root/swapfile"
  mkswap "$root/swapfile" >/dev/null
  swapon "$root/swapfile"
  echo '/swapfile none swap sw 0 0' >> "$root/etc/fstab"
fi

echo "Users"
id dalan >/dev/null 2>&1 || useradd --system --home-dir /var/lib/dalan --shell /usr/sbin/nologin dalan
id deploy >/dev/null 2>&1 || useradd --create-home --shell /bin/bash deploy
install -d -m 700 -o deploy -g deploy "$root/home/deploy/.ssh"
install -m 600 -o deploy -g deploy "$key_file" "$root/home/deploy/.ssh/authorized_keys"
# deploy.sh needs only this one command with root rights. Not "install /dev/stdin": install on Ubuntu 26.04 cannot read it.
sudoers="$root/etc/sudoers.d/dalan-deploy"
rm -f "$sudoers"
echo 'deploy ALL=(root) NOPASSWD: /usr/bin/systemctl restart dalan' > "$sudoers"
chmod 440 "$sudoers"

echo "Folders"
install -d -m 755 -o deploy -g deploy "$root/opt/dalan" "$root/opt/dalan/releases"
install -d -m 750 -o root -g dalan "$root/etc/dalan"
install -d -m 700 -o postgres -g postgres "$root/var/backups/dalan"

base64url() { base64 -w0 | tr '+/' '-_' | tr -d '='; }
hex_bytes() { local hex; hex=$(cat); printf '%b' "$(printf '%s' "$hex" | sed 's/../\\x&/g')"; }
sql() { sudo -u postgres psql -Atq -c "$1"; }

env_file="$root/etc/dalan/dalan.env"
if [ ! -f "$env_file" ]; then
  echo "Secrets (new $env_file)"
  password=$(openssl rand -hex 24)
  # .NET reads the signing key as PKCS#8; genpkey alone writes SEC1 in DER.
  signing_key=$(openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 | openssl pkcs8 -topk8 -nocrypt -outform DER | base64 -w0)
  # A P-256 private key in DER (SEC1): the 32 private bytes start at byte 7, the last 65 bytes are the public key.
  der=$(openssl ecparam -name prime256v1 -genkey -noout -outform DER | od -An -tx1 -v | tr -d ' \n')
  push_private=$(printf '%s' "${der:14:64}" | hex_bytes | base64url)
  push_public=$(printf '%s' "${der: -130}" | hex_bytes | base64url)
  if [ -z "$(sql "SELECT 1 FROM pg_roles WHERE rolname = 'dalan'")" ]; then
    sql "CREATE ROLE dalan LOGIN PASSWORD '$password'"
  else
    sql "ALTER ROLE dalan LOGIN PASSWORD '$password'"
  fi
  (umask 027 && cat > "$env_file" <<ENV
# Dalan server secrets. Made by deploy/setup.sh; never commit this file.
ConnectionStrings__Dalan=Host=localhost;Database=dalan;Username=dalan;Password=$password
Identity__SigningKey=$signing_key
Notification__Push__PublicKey=$push_public
Notification__Push__PrivateKey=$push_private
# After a change by hand: sudo systemctl restart dalan
# SMS and admin phones: install.sh sets them (ADMIN_PHONES, SMS, KAVENEGAR_KEY), or by hand:
# Notification__SmsProvider=kavenegar
# Notification__Kavenegar__ApiKey=
# Identity__AdminPhones=
# Email login, by hand:
# Notification__EmailProvider=smtp
# Notification__Smtp__Host=
# Notification__Smtp__UserName=
# Notification__Smtp__Password=
# Notification__Smtp__From=Dalan <no-reply@dalanet.ir>
ENV
  )
fi
chown root:dalan "$env_file"
chmod 640 "$env_file"

# Replace the line "$1=..." in dalan.env (or add it); with no $2, only remove it. Keeps the file's owner and mode.
set_env() {
  local lines
  lines=$(grep -v "^$1=" "$env_file" || true)
  { printf '%s\n' "$lines"; [ $# -lt 2 ] || printf '%s=%s\n' "$1" "$2"; } > "$env_file"
}
if [ -f "$here/settings.env" ]; then
  echo "Settings from install.sh"
  . "$here/settings.env"
  [ -z "${ADMIN_PHONES:-}" ] || set_env Identity__AdminPhones "$ADMIN_PHONES"
  [ -z "${KAVENEGAR_KEY:-}" ] || set_env Notification__Kavenegar__ApiKey "$KAVENEGAR_KEY"
  case "${SMS:-}" in
    "") ;;
    log) set_env Notification__SmsProvider log
         set_env Notification__LogSmsPhones "${ADMIN_PHONES:?SMS=log needs ADMIN_PHONES}" ;;
    kavenegar) set_env Notification__SmsProvider kavenegar
               set_env Notification__LogSmsPhones ;;
    *) echo "SMS must be log or kavenegar, not '$SMS'" >&2; exit 2 ;;
  esac
fi

echo "Database"
if [ -z "$(sql "SELECT 1 FROM pg_database WHERE datname = 'dalan'")" ]; then
  sql "CREATE DATABASE dalan OWNER dalan"
fi

echo "Caddy and units"
install -m 755 "$here/caddy" "$root/usr/local/bin/caddy"
install -d -m 755 "$root/etc/caddy"
install -m 644 "$here/Caddyfile" "$root/etc/caddy/Caddyfile"
# Domains for Caddy and for the links the server makes; written each run, so a new domain needs only a new run.
printf 'DALAN_SITE_DOMAIN=%s\nDALAN_API_DOMAIN=%s\nAdmin__SiteUrl=https://%s\nAdmin__BotApiUrl=https://%s\nNotification__Push__Subject=https://%s\n' \
  "$site" "$api" "$site" "$api" "$site" > "$root/etc/dalan/domains.env"
chmod 644 "$root/etc/dalan/domains.env"
rm -f "$root/etc/dalan/caddy.env"
install -d -m 755 "$root/opt/dalan/tools"
install -m 755 "$here/backup.sh" "$here/drill.sh" "$root/opt/dalan/tools/"
for unit in dalan.service caddy.service dalan-backup.service dalan-backup.timer; do
  install -m 644 "$here/$unit" "$root/etc/systemd/system/$unit"
done
systemctl daemon-reload
systemctl enable dalan.service
systemctl enable --now dalan-backup.timer
systemctl enable caddy.service
systemctl restart caddy.service
# Reads new settings and domains; does nothing before the first deploy.
systemctl try-restart dalan.service

echo "Done. Next: run the deploy from CI (the first time), and see $env_file for the SMS provider."
