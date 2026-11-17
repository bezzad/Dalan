#!/usr/bin/env bash
# Prepare a fresh Ubuntu server for the first deploy (OPS-09, OPS-10). Run as root, on the server.
# Safe to run again: it never changes an existing /etc/dalan/dalan.env.
# deploy/install.sh copies this folder and the Caddy binary to the server and runs it.
#
# Usage: setup.sh <site domain> <api domain> <deploy public key file>
# Settings (environment, for tests): DALAN_ROOT (default empty, the real /)
set -euo pipefail

site="${1:?site domain is required, for example dalanet.ir}"
api="${2:?api domain is required, for example api.dalanet.ir}"
key_file="${3:?public key file of the CI deploy user is required}"
here="$(cd "$(dirname "$0")" && pwd)"
root="${DALAN_ROOT:-}"

echo "Packages (from the Ubuntu mirror)"
DEBIAN_FRONTEND=noninteractive apt-get install -y -q postgresql curl nftables openssl

echo "Users"
id dalan >/dev/null 2>&1 || useradd --system --home-dir /var/lib/dalan --shell /usr/sbin/nologin dalan
id deploy >/dev/null 2>&1 || useradd --create-home --shell /bin/bash deploy
install -d -m 700 -o deploy -g deploy "$root/home/deploy/.ssh"
install -m 600 -o deploy -g deploy "$key_file" "$root/home/deploy/.ssh/authorized_keys"
# deploy.sh needs only this one command with root rights.
echo 'deploy ALL=(root) NOPASSWD: /usr/bin/systemctl restart dalan' > "$root/etc/sudoers.d/dalan-deploy"
chmod 440 "$root/etc/sudoers.d/dalan-deploy"

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
  signing_key=$(openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -outform DER | base64 -w0)
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
# Fill in, then: sudo systemctl restart dalan
# Notification__SmsProvider=kavenegar
# Notification__Kavenegar__ApiKey=
# Identity__AdminPhones=
ENV
  )
fi
chown root:dalan "$env_file"
chmod 640 "$env_file"

echo "Database"
if [ -z "$(sql "SELECT 1 FROM pg_database WHERE datname = 'dalan'")" ]; then
  sql "CREATE DATABASE dalan OWNER dalan"
fi

echo "Caddy and units"
install -m 755 "$here/caddy" "$root/usr/local/bin/caddy"
install -d -m 755 "$root/etc/caddy"
install -m 644 "$here/Caddyfile" "$root/etc/caddy/Caddyfile"
printf 'DALAN_SITE_DOMAIN=%s\nDALAN_API_DOMAIN=%s\n' "$site" "$api" > "$root/etc/dalan/caddy.env"
chmod 644 "$root/etc/dalan/caddy.env"
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

echo "Done. Next: fill in $env_file, then run the deploy from CI."
