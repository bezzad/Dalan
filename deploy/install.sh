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
#   SMS             log (codes only to ADMIN_PHONES, in the server log; before the provider is ready) or smsir
#   SMSIR_KEY       the SMS.ir API key
#   ROTATE_SECRETS  1: new database password, signing key (everyone logs in again) and push keys
#   GITHUB_CLIENT_ID, GITHUB_CLIENT_SECRET   the GitHub OAuth app for "Log in with GitHub" (OPS-14; docs/SERVER.md)
#   PULL_DEPLOY=1   the server builds and deploys main itself (OPS-16): also downloads the .NET SDK (the version in
#                   global.json, newest patch) and Node (the major version CI uses) here and copies them
#   DALAN_CACHE     default ~/.cache/dalan-install: Caddy, Node and the .NET SDK stay here, so the next run downloads
#                   only what changed (a new version)
#   SITES_DOMAIN    the users' sites, for example dalancloud.ir (empty: sites are off); TUNNEL_DOMAIN default tunnel.<site domain> (OPS-15)
#   ARVAN_API_KEY   the ArvanCloud API key, written like "Apikey <key>": one wildcard certificate for the sites (OPS-18);
#                   also downloads lego (LEGO_VERSION, default 5.8.0) here and copies it. CERT_EMAIL: optional, the Let's Encrypt account email
# Example: ADMIN_PHONES=09121234567 SMS=log deploy/install.sh root@203.0.113.10 dalanet.ir api.dalanet.ir ~/.ssh/dalan-deploy.pub
set -euo pipefail

target="${1:?user@server is required}"
site="${2:?site domain is required}"
api="${3:?api domain is required}"
key="${4:?public key file of the CI deploy user is required}"
here="$(cd "$(dirname "$0")" && pwd)"
version="${CADDY_VERSION:-$(sed -nE 's/^ *CADDY_VERSION: "(.*)"/\1/p' "$here/../.github/workflows/ci.yml")}"

work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
cache="${DALAN_CACHE:-$HOME/.cache/dalan-install}"
mkdir -p "$cache"
# fetch <url> <file in the cache> <checksum file> <sha256sum|sha512sum>: download only when the cached file is missing or wrong.
fetch() {
  local check; check() { (cd "$cache" && grep " $2\$" "$3" | "$4" -c - >/dev/null 2>&1); }
  check "$1" "$2" "$3" "$4" && { echo "  $2 (from the cache)"; return 0; }
  curl -fsSL -o "$cache/$2" "$1"
  (cd "$cache" && grep " $2\$" "$3" | "$4" -c -)
}

file="caddy_${version}_linux_amd64.tar.gz"
base="https://github.com/caddyserver/caddy/releases/download/v${version}"
echo "Caddy $version"
curl -fsSL -o "$cache/caddy_checksums.txt" "$base/caddy_${version}_checksums.txt"
fetch "$base/$file" "$file" caddy_checksums.txt sha512sum
# OPS-18: lego gets the wildcard certificate; only needed with the ArvanCloud key.
if [ -n "${ARVAN_API_KEY:-}" ]; then
  lego_version="${LEGO_VERSION:-5.8.0}"
  lego_file="lego_v${lego_version}_linux_amd64.tar.gz"
  lego_base="https://github.com/go-acme/lego/releases/download/v${lego_version}"
  echo "lego $lego_version"
  curl -fsSL -o "$cache/lego_checksums.txt" "$lego_base/lego_${lego_version}_checksums.txt"
  fetch "$lego_base/$lego_file" "$lego_file" lego_checksums.txt sha256sum
fi

# The tool versions the server has (written by setup.sh). A tool with the same version is not sent again.
# No answer (a first install, or the link stalled): everything is sent.
have=$(ssh "$target" 'cat /opt/dalan/tools/*.version 2>/dev/null' || true)
# send <tool> <version>: true when the server needs it; writes <tool>.version into the setup folder for setup.sh.
send() {
  if grep -qxF "$1 $2" <<< "$have"; then echo "  $1 $2 is on the server already"; return 1; fi
  echo "$1 $2" > "$work/dalan-setup/$1.version"
}

mkdir "$work/dalan-setup"
! send caddy "$version" || tar -xzf "$cache/$file" -C "$work/dalan-setup" caddy
[ -z "${lego_file:-}" ] || ! send lego "$lego_version" || tar -xzf "$cache/$lego_file" -C "$work/dalan-setup" lego
cp "$here"/{setup.sh,backup.sh,restore.sh,drill.sh,Caddyfile,dalan.service,caddy.service,dalan-backup.service,dalan-backup.timer,dalan-backup.path,dalan-restore.path,dalan-restore.service} "$work/dalan-setup/"
cp "$here"/{wildcard-cert.sh,dalan-cert.service,dalan-cert.timer} "$work/dalan-setup/"
if [ "${PULL_DEPLOY:-}" = 1 ]; then
  cp "$here"/{pull-deploy.sh,dalan-pull.service,dalan-pull.timer} "$work/dalan-setup/"
  sdk=$(sed -nE 's/.*"version": "([0-9]+\.[0-9]+)\..*/\1/p' "$here/../global.json")
  echo "The .NET SDK $sdk"
  # dotnet-install.sh skips the download when the newest patch is already in the cache folder.
  [ -s "$cache/dotnet-install.sh" ] || curl -fsSL -o "$cache/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
  bash "$cache/dotnet-install.sh" --channel "$sdk" --install-dir "$cache/dotnet-$sdk" --no-path >/dev/null
  sdk_version=$(ls "$cache/dotnet-$sdk/sdk" | sort -V | tail -n 1)
  ! send dotnet "$sdk_version" || cp -a "$cache/dotnet-$sdk" "$work/dalan-setup/dotnet"
  node=$(sed -nE 's/^ *node-version: ([0-9]+).*/\1/p' "$here/../.github/workflows/ci.yml" | head -n 1)
  echo "Node $node"
  base="https://nodejs.org/dist/latest-v$node.x"
  curl -fsSL -o "$cache/node_SHASUMS256.txt" "$base/SHASUMS256.txt"
  file=$(grep -oE "node-v[0-9.]+-linux-x64\.tar\.xz" "$cache/node_SHASUMS256.txt" | head -n 1)
  fetch "$base/$file" "$file" node_SHASUMS256.txt sha256sum
  ! send node "${file%.tar.xz}" || { mkdir "$work/dalan-setup/node" && tar -xJf "$cache/$file" -C "$work/dalan-setup/node" --strip-components=1; }
fi
cp "$key" "$work/dalan-setup/deploy.pub"
# A file, not the command line, so the key is not visible in the server's process list.
(umask 077 && for name in ADMIN_PHONES SMS SMSIR_KEY ROTATE_SECRETS GITHUB_CLIENT_ID GITHUB_CLIENT_SECRET SITES_DOMAIN TUNNEL_DOMAIN PULL_DEPLOY ARVAN_API_KEY CERT_EMAIL; do
  [ -z "${!name:-}" ] || printf '%s=%q\n' "$name" "${!name}"
done > "$work/dalan-setup/settings.env")

echo "Copying to $target"
# tar, not scp -r: scp turns symlinks into copies, and Node's bin/npm is a symlink that breaks as a copy (OPS-16).
tar -cf - -C "$work" dalan-setup | ssh "$target" "rm -rf /tmp/dalan-setup && tar --no-same-owner -xf - -C /tmp"
ssh -t "$target" "sudo bash /tmp/dalan-setup/setup.sh '$site' '$api' /tmp/dalan-setup/deploy.pub; code=\$?; rm -rf /tmp/dalan-setup; exit \$code"
