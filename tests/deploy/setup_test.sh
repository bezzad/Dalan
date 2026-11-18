#!/usr/bin/env bash
# Tests for deploy/setup.sh and deploy/install.sh with fake system commands and a fake root folder.
set -euo pipefail
deploy="$(cd "$(dirname "$0")/../.." && pwd)/deploy"
fail() { echo "FAIL: $*"; exit 1; }
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
root="$tmp/root"; bin="$tmp/bin"; state="$tmp/state"
mkdir -p "$bin" "$state" "$root"/{etc/sudoers.d,etc/systemd/system,usr/local/bin,home,opt,var/backups}

# The setup folder as install.sh copies it: deploy/ plus a fake caddy binary.
src="$tmp/dalan-setup"; mkdir "$src"
cp "$deploy"/{setup.sh,backup.sh,drill.sh,Caddyfile,dalan.service,caddy.service,dalan-backup.service,dalan-backup.timer} "$src/"
printf '#!/bin/sh\n' > "$src/caddy"
echo 'ssh-ed25519 AAAA ci-deploy' > "$src/deploy.pub"

log() { printf '#!/bin/sh\necho "%s $*" >> %s/commands.log\n%s\n' "$1" "$state" "${2:-}" > "$bin/$1"; }
log apt-get
log swapon
log fallocate 'touch "$3"'
log mkswap
log systemctl
log chown
log useradd 'eval "name=\${$#}"; touch '"$state"'/user-$name'
log id '[ -f '"$state"'/user-$1 ]'
# Real install, without owners (the test is not root).
cat > "$bin/install" <<SH
#!/bin/bash
args=(); while [ \$# -gt 0 ]; do case "\$1" in -o|-g) shift 2;; *) args+=("\$1"); shift;; esac; done
exec /usr/bin/install "\${args[@]}"
SH
# Fake sudo -u postgres psql -Atq -c "<sql>": remembers roles and databases.
cat > "$bin/sudo" <<SH
#!/bin/bash
sql="\${@: -1}"; echo "psql \$sql" >> "$state/commands.log"
case "\$sql" in
  *"FROM pg_roles"*) [ -f "$state/role" ] && echo 1;;
  *"FROM pg_database"*) [ -f "$state/db" ] && echo 1;;
  "CREATE ROLE"*) touch "$state/role";;
  "CREATE DATABASE"*) touch "$state/db";;
esac
exit 0
SH
for tool in curl wget git scp ssh; do printf '#!/bin/sh\necho %s >> %s/forbidden.log\nexit 1\n' "$tool" "$state" > "$bin/$tool"; done
chmod +x "$bin"/*
printf 'MemTotal:        1000000 kB\n' > "$tmp/meminfo"
export PATH="$bin:$PATH" DALAN_ROOT="$root" MEMINFO="$tmp/meminfo"
env_file="$root/etc/dalan/dalan.env"

# Rule: OPS-09 fresh server
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > "$tmp/out1" || { cat "$tmp/out1"; fail "first run failed"; }
for unit in dalan.service caddy.service dalan-backup.service dalan-backup.timer; do
  [ -f "$root/etc/systemd/system/$unit" ] || fail "$unit not installed"
done
for cmd in 'systemctl enable dalan.service' 'systemctl enable caddy.service' 'systemctl enable --now dalan-backup.timer' 'useradd --system' 'apt-get install'; do
  grep -qF "$cmd" "$state/commands.log" || fail "not run: $cmd"
done
grep -q 'postgresql' "$state/commands.log" || fail "PostgreSQL not installed"
grep -qx 'DALAN_SITE_DOMAIN=dalanet.ir' "$root/etc/dalan/caddy.env" || fail "site domain not set for Caddy"
grep -qx 'DALAN_API_DOMAIN=api.dalanet.ir' "$root/etc/dalan/caddy.env" || fail "api domain not set for Caddy"
[ -x "$root/usr/local/bin/caddy" ] || fail "caddy not installed"
[ -x "$root/opt/dalan/tools/backup.sh" ] && [ -x "$root/opt/dalan/tools/drill.sh" ] || fail "tools not installed"
cmp -s "$src/deploy.pub" "$root/home/deploy/.ssh/authorized_keys" || fail "deploy key not installed"
grep -q 'CREATE DATABASE dalan OWNER dalan' "$state/commands.log" || fail "database not made"
[ -f "$state/forbidden.log" ] && fail "setup called: $(cat "$state/forbidden.log")"
echo "PASS OPS-09 fresh server"

# Rule: OPS-09 small server gets swap once
grep -qF 'fallocate -l 2048M' "$state/commands.log" || fail "no swap file on a 1 GB server"
grep -qx '/swapfile none swap sw 0 0' "$root/etc/fstab" || fail "swap not in fstab"
echo "PASS OPS-09 small server gets swap"

# Rule: OPS-09 deploy user rights
[ "$(cat "$root/etc/sudoers.d/dalan-deploy")" = 'deploy ALL=(root) NOPASSWD: /usr/bin/systemctl restart dalan' ] \
  || fail "sudoers: $(cat "$root/etc/sudoers.d/dalan-deploy")"
[ "$(stat -c %a "$root/etc/sudoers.d/dalan-deploy")" = 440 ] || fail "sudoers file mode"
grep -q 'sudo systemctl restart dalan\|SYSTEMCTL:-sudo systemctl' "$deploy/deploy.sh" || fail "deploy.sh does not use the allowed command"
echo "PASS OPS-09 deploy user may only restart dalan"

# Rule: OPS-10 first run makes the secrets
[ "$(stat -c %a "$env_file")" = 640 ] || fail "dalan.env mode is $(stat -c %a "$env_file")"
connection=$(sed -nE 's/^ConnectionStrings__Dalan=(.*)/\1/p' "$env_file")
[[ "$connection" == "Host=localhost;Database=dalan;Username=dalan;"* ]] || fail "no connection string"
[[ "$connection" =~ \;Password=[0-9a-f]{48}$ ]] || fail "no random database password"
password=$(sed -nE 's/.*Password=(.*)/\1/p' "$env_file")
grep -qF "CREATE ROLE dalan LOGIN PASSWORD '$password'" "$state/commands.log" || fail "role password differs from dalan.env"
signing=$(sed -nE 's/^Identity__SigningKey=(.*)/\1/p' "$env_file")
echo "$signing" | base64 -d | openssl pkey -inform DER -noout 2>/dev/null || fail "signing key is not a key"
grep -qE '^Notification__Push__PublicKey=B[A-Za-z0-9_-]{86}$' "$env_file" || fail "no push public key"
grep -qE '^Notification__Push__PrivateKey=[A-Za-z0-9_-]{43}$' "$env_file" || fail "no push private key"
grep -q '^# Notification__Kavenegar__ApiKey=' "$env_file" || fail "no line for the SMS key"
echo "PASS OPS-10 first run makes the secrets"

# Rule: OPS-10 second run keeps dalan.env
echo 'Notification__Kavenegar__ApiKey=owner-key' >> "$env_file"
before=$(sha256sum < "$env_file")
: > "$state/commands.log"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > "$tmp/out2" || { cat "$tmp/out2"; fail "second run failed"; }
[ "$(sha256sum < "$env_file")" = "$before" ] || fail "dalan.env changed"
grep -qE 'CREATE|ALTER|useradd|fallocate' "$state/commands.log" && fail "second run made things again: $(grep -E 'CREATE|ALTER|useradd' "$state/commands.log")"
echo "PASS OPS-10 second run keeps dalan.env"

# Rule: OPS-09 install.sh downloads Caddy on the owner's machine (the CI version, checked) and runs setup over SSH
inst="$tmp/inst"; mkdir -p "$inst/bin"
ci_version=$(sed -nE 's/^ *CADDY_VERSION: "(.*)"/\1/p' "$deploy/../.github/workflows/ci.yml")
cat > "$inst/bin/curl" <<SH
#!/bin/bash
out=""; while [ \$# -gt 0 ]; do case "\$1" in -o) out="\$2"; shift 2;; http*) url="\$1"; shift;; *) shift;; esac; done
echo "\$url" >> "$inst/urls.log"
case "\$url" in
  *checksums.txt) (cd "$inst" && sha512sum caddy_${ci_version}_linux_amd64.tar.gz) > "\$out";;
  *) cp "$inst/caddy_${ci_version}_linux_amd64.tar.gz" "\$out";;
esac
SH
printf '#!/bin/bash\necho "scp $*" >> %s/remote.log\ncp -r "${@: -2:1}" %s/copied\n' "$inst" "$inst" > "$inst/bin/scp"
printf '#!/bin/bash\necho "ssh $*" >> %s/remote.log\n' "$inst" > "$inst/bin/ssh"
chmod +x "$inst/bin"/*
mkdir "$inst/pkg" && printf '#!/bin/sh\n' > "$inst/pkg/caddy" && tar -czf "$inst/caddy_${ci_version}_linux_amd64.tar.gz" -C "$inst/pkg" caddy
PATH="$inst/bin:$PATH" bash "$deploy/install.sh" admin@server dalanet.ir api.dalanet.ir "$src/deploy.pub" >/dev/null || fail "install.sh failed"
grep -q "releases/download/v${ci_version}/caddy_${ci_version}_linux_amd64.tar.gz" "$inst/urls.log" || fail "install.sh did not fetch Caddy $ci_version"
for f in caddy setup.sh drill.sh backup.sh Caddyfile caddy.service deploy.pub; do [ -f "$inst/copied/$f" ] || fail "$f not copied"; done
grep -q "ssh -t admin@server sudo bash /tmp/dalan-setup/setup.sh 'dalanet.ir' 'api.dalanet.ir' /tmp/dalan-setup/deploy.pub" "$inst/remote.log" || fail "setup not run: $(cat "$inst/remote.log")"
echo "PASS OPS-09 install.sh copies Caddy and runs setup"
