#!/usr/bin/env bash
# Run on your own machine (Linux, macOS, WSL or Git Bash), not on the server (OPS-09).
# Downloads Caddy here, copies it with deploy/ to the server, and runs setup.sh there as root.
# The server downloads nothing from abroad.
#
# Usage: deploy/install.sh <root@server> <site domain> <api domain> <deploy public key file>
# Example: deploy/install.sh root@203.0.113.10 dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
# Settings (environment): CADDY_VERSION (default: the version CI tests)
set -euo pipefail

target="${1:?root@server is required}"
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

echo "Copying to $target"
scp -q -r "$work/dalan-setup" "$target:/root/"
ssh "$target" "bash /root/dalan-setup/setup.sh '$site' '$api' /root/dalan-setup/deploy.pub && rm -rf /root/dalan-setup"
