#!/usr/bin/env bash
# Run on your own machine (Linux, macOS, WSL or Git Bash), not on the server (OPS-09).
# Downloads Caddy here, copies it with deploy/ to the server, and runs setup.sh there as root.
# The server downloads nothing from abroad.
#
# Usage: deploy/install.sh <user@server> <site domain> <api domain> <deploy public key file>
# The user is root, or a user who may use sudo (it may ask for the password).
# Example: deploy/install.sh root@203.0.113.10 dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
# Settings (environment):
#   CADDY_VERSION   default: the version CI tests
#   ADMIN_PHONES    admin mobile numbers, comma separated
#   SMS             log (codes only to ADMIN_PHONES, in the server log; before the provider is ready) or kavenegar
#   KAVENEGAR_KEY   the Kavenegar API key
# Example: ADMIN_PHONES=09121234567 SMS=log deploy/install.sh root@203.0.113.10 dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
set -euo pipefail

target="${1:?user@server is required}"
site="${2:?site domain is required}"
api="${3:?api domain is required}"
key="${4:?public key file of the CI deploy user is required}"
here="$(cd "$(dirname "$0")" && pwd)"
version="${CADDY_VERSION:-$(sed -nE 's/^ *CADDY_VERSION: "(.*)"/\1/p' "$here/../.github/workflows/ci.yml")}"

work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
file="caddy_${version}_linux_amd64.tar.gz"
base="https://github.com/caddyserver/caddy/releases/download/v${version}"
echo "Downloading Caddy $version"
curl -fsSL -o "$work/$file" "$base/$file"
curl -fsSL -o "$work/checksums.txt" "$base/caddy_${version}_checksums.txt"
(cd "$work" && grep " $file\$" checksums.txt | sha512sum -c -)

mkdir "$work/dalan-setup"
tar -xzf "$work/$file" -C "$work/dalan-setup" caddy
cp "$here"/{setup.sh,backup.sh,drill.sh,Caddyfile,dalan.service,caddy.service,dalan-backup.service,dalan-backup.timer} "$work/dalan-setup/"
cp "$key" "$work/dalan-setup/deploy.pub"
# A file, not the command line, so the key is not visible in the server's process list.
(umask 077 && for name in ADMIN_PHONES SMS KAVENEGAR_KEY; do
  [ -z "${!name:-}" ] || printf '%s=%q\n' "$name" "${!name}"
done > "$work/dalan-setup/settings.env")

echo "Copying to $target"
ssh "$target" "rm -rf /tmp/dalan-setup"
scp -q -r "$work/dalan-setup" "$target:/tmp/"
ssh -t "$target" "sudo bash /tmp/dalan-setup/setup.sh '$site' '$api' /tmp/dalan-setup/deploy.pub; code=\$?; rm -rf /tmp/dalan-setup; exit \$code"
