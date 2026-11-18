#!/usr/bin/env bash
# Run on your own machine (Linux, macOS, WSL or Git Bash), not on the server (OPS-09).
# Downloads Caddy here, copies it with deploy/ to the server, and runs setup.sh there as root.
# The server downloads nothing from abroad.
#
# Usage: deploy/install.sh <user@server> <site domain> <api domain> <deploy public key file>
# The user is root, or a user who may use sudo (it may ask for the password).
# Example: deploy/install.sh root@203.0.113.10 dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
# Settings (environment):
#   CADDY_VERSION          default: the version CI tests
#   GITHUB_CLIENT_ID       with GITHUB_CLIENT_SECRET: the GitHub OAuth app for "Log in with GitHub" (OPS-13).
#   GITHUB_CLIENT_SECRET   Sent in a file, never on a command line. docs/SERVER.md says how to make the app.
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
if [ -n "${GITHUB_CLIENT_ID:-}" ] && [ -n "${GITHUB_CLIENT_SECRET:-}" ]; then
  (umask 077 && printf 'GITHUB_CLIENT_ID=%s\nGITHUB_CLIENT_SECRET=%s\n' "$GITHUB_CLIENT_ID" "$GITHUB_CLIENT_SECRET" > "$work/dalan-setup/github.env")
fi

echo "Copying to $target"
ssh "$target" "rm -rf /tmp/dalan-setup"
scp -q -r "$work/dalan-setup" "$target:/tmp/"
ssh -t "$target" "sudo bash /tmp/dalan-setup/setup.sh '$site' '$api' /tmp/dalan-setup/deploy.pub; code=\$?; rm -rf /tmp/dalan-setup; exit \$code"
