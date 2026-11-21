#!/usr/bin/env bash
# Tests for deploy/setup.sh and deploy/install.sh with fake system commands and a fake root folder.
set -euo pipefail
deploy="$(cd "$(dirname "$0")/../.." && pwd)/deploy"
fail() { echo "FAIL: $*"; exit 1; }
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
root="$tmp/root"; bin="$tmp/bin"; state="$tmp/state"
mkdir -p "$bin" "$state" "$root"/{etc/sudoers.d,etc/dkimkeys,etc/systemd/system,usr/local/bin,home,opt,var/backups}

# The setup folder as install.sh copies it: deploy/ plus a fake caddy binary.
src="$tmp/dalan-setup"; mkdir "$src"
cp "$deploy"/{setup.sh,backup.sh,drill.sh,Caddyfile,dalan.service,caddy.service,dalan-backup.service,dalan-backup.timer} "$src/"
printf '#!/bin/sh\n' > "$src/caddy"
echo 'ssh-ed25519 AAAA ci-deploy' > "$src/deploy.pub"

log() { printf '#!/bin/sh\necho "%s $*" >> %s/commands.log\n%s\n' "$1" "$state" "${2:-}" > "$bin/$1"; }
log apt-get
log apt-cache 'printf "libicu74 - International Components for Unicode\nlibicu76 - International Components for Unicode\n"'
log swapon
log fallocate 'touch "$3"'
log mkswap
log systemctl
log chown
log debconf-set-selections 'cat >> '"$state"'/debconf.txt'
log postconf
# Writes the two files the real one writes; the record is in quoted pieces like the real one.
cat > "$bin/opendkim-genkey" <<SH
#!/bin/bash
dir="\$4"; touch "\$dir/dalan.private" "$state/genkey"
printf 'dalan._domainkey\tIN\tTXT\t( "v=DKIM1; h=sha256; k=rsa; "\n\t  "p=MIIBIjAN"\n\t  "Bgkqhk" )  ; ----- DKIM key dalan for dalanet.ir\n' > "\$dir/dalan.txt"
SH
log useradd 'eval "name=\${$#}"; touch '"$state"'/user-$name'
log id '[ -f '"$state"'/user-$1 ]'
# Real install, without owners (the test is not root). Like install on Ubuntu 26.04 (uutils), it cannot read /dev/stdin.
cat > "$bin/install" <<SH
#!/bin/bash
args=(); while [ \$# -gt 0 ]; do case "\$1" in -o|-g) shift 2;; /dev/stdin) echo "install: No such file or directory" >&2; exit 1;; *) args+=("\$1"); shift;; esac; done
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
grep -qx 'apt-get update -q' "$state/commands.log" || fail "package lists not updated"
grep -q 'apt-get install .*postgresql.* libicu76 ' "$state/commands.log" || fail "PostgreSQL and the newest ICU not installed: $(grep apt-get "$state/commands.log")"
grep -q 'apt-get install .* rsync ' "$state/commands.log" || fail "rsync not installed (deploy.sh sends the release with it)"
domains="$root/etc/dalan/domains.env"
grep -qx 'DALAN_SITE_DOMAIN=dalanet.ir' "$domains" || fail "site domain not set for Caddy"
grep -qx 'DALAN_API_DOMAIN=api.dalanet.ir' "$domains" || fail "api domain not set for Caddy"
grep -qx 'Admin__SiteUrl=https://dalanet.ir' "$domains" || fail "site address not set for the server"
grep -qx 'Admin__BotApiUrl=https://api.dalanet.ir' "$domains" || fail "Bot API address not set for the server"
grep -qx 'Notification__Push__Subject=https://dalanet.ir' "$domains" || fail "push subject not set"
grep -qx 'DALAN_TUNNEL_DOMAIN=tunnel.dalanet.ir' "$domains" || fail "tunnel domain not set for Caddy"
grep -qx 'Sites__TunnelUrl=https://tunnel.dalanet.ir' "$domains" || fail "tunnel address not set for the server"
grep -qx 'Sites__Domain=' "$domains" || fail "sites must be off when no sites domain is given"
grep -q '^EnvironmentFile=/etc/dalan/domains.env' "$root/etc/systemd/system/dalan.service" || fail "dalan does not read the domains"
grep -q '^EnvironmentFile=/etc/dalan/domains.env' "$root/etc/systemd/system/caddy.service" || fail "caddy does not read the domains"
grep -qx 'systemctl try-restart dalan.service' "$state/commands.log" || fail "dalan not restarted for new settings"
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
# .NET imports it with ImportPkcs8PrivateKey: PKCS#8 starts with version 0 and the EC algorithm id, not SEC1's version 1.
[[ "$(echo "$signing" | base64 -d | od -An -tx1 -N 19 | tr -d ' \n')" == 30818702010030130607*2a8648ce3d0201* ]] || fail "signing key is not PKCS#8"
grep -qE '^Notification__Push__PublicKey=B[A-Za-z0-9_-]{86}$' "$env_file" || fail "no push public key"
grep -qE '^Notification__Push__PrivateKey=[A-Za-z0-9_-]{43}$' "$env_file" || fail "no push private key"
grep -q '^# Notification__Kavenegar__ApiKey=' "$env_file" || fail "no line for the SMS key"
echo "PASS OPS-10 first run makes the secrets"

# Rule: OPS-13 mail on a new server
grep -q 'apt-get install .*postfix opendkim opendkim-tools' "$state/commands.log" || fail "Postfix and OpenDKIM not installed"
grep -qx 'postfix postfix/mailname string dalanet.ir' "$state/debconf.txt" || fail "Postfix not told its name"
for setting in 'myhostname = dalanet.ir' 'inet_interfaces = loopback-only' 'inet_protocols = ipv4' 'smtpd_milters = inet:127.0.0.1:8891' 'non_smtpd_milters = $smtpd_milters'; do
  grep -qF "$setting" "$state/commands.log" || fail "Postfix setting missing: $setting"
done
grep -qx 'Domain dalanet.ir' "$root/etc/opendkim.conf" && grep -qx 'Selector dalan' "$root/etc/opendkim.conf" \
  && grep -qx 'Socket inet:8891@127.0.0.1' "$root/etc/opendkim.conf" || fail "OpenDKIM config: $(cat "$root/etc/opendkim.conf")"
grep -qF 'systemctl restart opendkim.service postfix.service' "$state/commands.log" || fail "mail services not started"
for line in 'Notification__EmailProvider=smtp' 'Notification__Smtp__Host=127.0.0.1' 'Notification__Smtp__Port=25' \
  'Notification__Smtp__StartTls=false' 'Notification__Smtp__From=Dalan <no-reply@dalanet.ir>'; do
  grep -qxF "$line" "$env_file" || fail "dalan.env misses: $line"
done
grep -qF 'v=spf1 a -all' "$tmp/out1" || fail "no SPF record printed"
grep -qF 'dalan._domainkey.dalanet.ir     v=DKIM1; h=sha256; k=rsa; p=MIIBIjANBgkqhk' "$tmp/out1" || fail "no DKIM record printed: $(grep -i dkim "$tmp/out1")"
grep -qF '_dmarc.dalanet.ir               v=DMARC1; p=none' "$tmp/out1" || fail "no DMARC record printed"
echo "PASS OPS-13 mail on a new server"

# Rule: OPS-10 second run keeps dalan.env
echo 'Notification__Kavenegar__ApiKey=owner-key' >> "$env_file"
before=$(sha256sum < "$env_file")
: > "$state/commands.log"; rm "$state/genkey"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > "$tmp/out2" || { cat "$tmp/out2"; fail "second run failed"; }
[ "$(sha256sum < "$env_file")" = "$before" ] || fail "dalan.env changed"
grep -qE 'CREATE|ALTER|useradd|fallocate' "$state/commands.log" && fail "second run made things again: $(grep -E 'CREATE|ALTER|useradd' "$state/commands.log")"
echo "PASS OPS-10 second run keeps dalan.env"

# Rule: OPS-13 key made once
[ -f "$state/genkey" ] && fail "DKIM key made again"
grep -qF 'p=MIIBIjANBgkqhk' "$tmp/out2" || fail "DKIM record not printed on the second run"
grep -qF 'try-restart' "$state/commands.log" && fail "dalan restarted with no new lines"
echo "PASS OPS-13 key made once"

# Rule: OPS-13 server made before own mail
grep -v -e '^Notification__Smtp__' -e '^Notification__EmailProvider=' -e '^# Login emails' "$env_file" > "$tmp/old.env"
echo 'Notification__EmailProvider=log' >> "$tmp/old.env"
cp "$tmp/old.env" "$env_file"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > "$tmp/out3" || { cat "$tmp/out3"; fail "third run failed"; }
[ "$(head -n "$(wc -l < "$tmp/old.env")" "$env_file")" = "$(cat "$tmp/old.env")" ] || fail "old lines changed"
[ "$(tail -n 1 "$env_file")" = 'Notification__Smtp__From=Dalan <no-reply@dalanet.ir>' ] || fail "mail lines not added at the end"
grep -qF 'systemctl try-restart dalan.service' "$state/commands.log" || fail "dalan not restarted for the new lines"
added=$(sha256sum < "$env_file")
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > /dev/null || fail "fourth run failed"
[ "$(sha256sum < "$env_file")" = "$added" ] || fail "mail lines added twice"
echo "PASS OPS-13 server made before own mail"

# Rule: OPS-09 a new domain needs only a new run
before=$(sha256sum < "$env_file")
bash "$src/setup.sh" example.ir api.example.ir "$src/deploy.pub" > /dev/null || fail "run with a new domain failed"
grep -qx 'Admin__SiteUrl=https://example.ir' "$root/etc/dalan/domains.env" || fail "new domain not written"
grep -qx 'DALAN_API_DOMAIN=api.example.ir' "$root/etc/dalan/domains.env" || fail "new api domain not written"
[ "$(sha256sum < "$env_file")" = "$before" ] || fail "dalan.env changed by a new domain"
echo "PASS OPS-09 a new domain needs only a new run"

# Rule: OPS-10 settings from install.sh
printf 'ADMIN_PHONES=09121234567\nSMS=log\n' > "$src/settings.env"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > /dev/null || fail "run with settings failed"
for line in Identity__AdminPhones=09121234567 Notification__SmsProvider=log Notification__LogSmsPhones=09121234567 \
            Notification__Kavenegar__ApiKey=owner-key "Identity__SigningKey=$signing"; do
  [ "$(grep -cx "$line" "$env_file")" = 1 ] || fail "dalan.env should have $line once: $(grep -v -i 'password\|key=' "$env_file")"
done
printf 'SMS=kavenegar\nKAVENEGAR_KEY=new-key\n' > "$src/settings.env"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > /dev/null || fail "run with kavenegar failed"
for line in Notification__SmsProvider=kavenegar Notification__Kavenegar__ApiKey=new-key Identity__AdminPhones=09121234567; do
  [ "$(grep -cx "$line" "$env_file")" = 1 ] || fail "dalan.env should have $line once"
done
grep -q '^Notification__LogSmsPhones=' "$env_file" && fail "log sender numbers kept with kavenegar"
[ "$(stat -c %a "$env_file")" = 640 ] || fail "dalan.env mode changed"
printf 'SMS=pigeon\n' > "$src/settings.env"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > /dev/null 2>&1 && fail "unknown SMS provider accepted"
rm "$src/settings.env"
echo "PASS OPS-10 settings from install.sh"

# Rule: OPS-15 the sites domain and the tunnel host from install.sh
printf 'SITES_DOMAIN=dalancloud.ir\nTUNNEL_DOMAIN=t.dalanet.ir\n' > "$src/settings.env"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > "$tmp/out-sites" || fail "run with a sites domain failed"
for line in Sites__Domain=dalancloud.ir DALAN_TUNNEL_DOMAIN=t.dalanet.ir Sites__TunnelUrl=https://t.dalanet.ir; do
  grep -qx "$line" "$root/etc/dalan/domains.env" || fail "domains.env should have $line"
done
grep -qF '*.dalancloud.ir' "$tmp/out-sites" || fail "setup does not print the DNS record of the sites"
rm "$src/settings.env"
echo "PASS OPS-15 sites domain and tunnel host from install.sh"

# Rule: OPS-16 the server builds main itself: tools, the GitHub key on port 443, the timer
cp "$deploy"/{pull-deploy.sh,dalan-pull.service,dalan-pull.timer} "$src/"
mkdir -p "$src/dotnet" "$src/node/bin" && touch "$src/dotnet/dotnet" "$src/node/bin/node"
cat > "$bin/ssh-keygen" <<SH
#!/bin/bash
echo "ssh-keygen \$*" >> "$state/commands.log"
for key; do :; done; echo 'ssh-ed25519 BBBB dalan-pull' > "\$key.pub"; touch "\$key"
SH
chmod +x "$bin/ssh-keygen"
: > "$state/commands.log"
printf 'PULL_DEPLOY=1\n' > "$src/settings.env"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > "$tmp/out-pull" || { cat "$tmp/out-pull"; fail "run with PULL_DEPLOY failed"; }
grep -q 'apt-get install .* git clang zlib1g-dev' "$state/commands.log" || fail "build packages not installed"
for f in opt/dalan/tools/dotnet/dotnet opt/dalan/tools/node/bin/node opt/dalan/tools/pull-deploy.sh \
         etc/systemd/system/dalan-pull.service etc/systemd/system/dalan-pull.timer; do
  [ -e "$root/$f" ] || fail "$f not installed"
done
grep -qx '  Port 443' "$root/home/deploy/.ssh/config" || fail "GitHub is not reached on port 443"
# GitHub's published host keys (fingerprint SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU), never trust on first use.
grep -qxF '[ssh.github.com]:443 ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIOMqqnkVzrm0SdG6UOoqKLsabgH5C9okWi0dh2l9GKJl' \
  "$root/home/deploy/.ssh/known_hosts" || fail "GitHub's host key is not pinned"
grep -qx '  StrictHostKeyChecking yes' "$root/home/deploy/.ssh/config" || fail "an unknown GitHub host key is not refused"
grep -qF 'systemctl enable --now dalan-pull.timer' "$state/commands.log" || fail "pull timer not started"
grep -qF 'ssh-ed25519 BBBB dalan-pull' "$tmp/out-pull" || fail "setup does not print the GitHub deploy key"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > /dev/null || fail "second PULL_DEPLOY run failed"
[ "$(grep -c 'ssh-keygen' "$state/commands.log")" = 1 ] || fail "GitHub key made again"
rm "$src/settings.env"
echo "PASS OPS-16 server set up to build main itself"

# Rule: OPS-10 secrets can be made again
old_secrets=$(grep -E '^(ConnectionStrings__Dalan|Identity__SigningKey|Notification__Push__(Public|Private)Key)=' "$env_file")
printf 'ROTATE_SECRETS=1\n' > "$src/settings.env"
: > "$state/commands.log"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > /dev/null || fail "run with ROTATE_SECRETS failed"
rm "$src/settings.env"
for key in ConnectionStrings__Dalan Identity__SigningKey Notification__Push__PublicKey Notification__Push__PrivateKey; do
  [ "$(grep -c "^$key=" "$env_file")" = 1 ] || fail "$key should be in dalan.env once"
  printf '%s\n' "$old_secrets" | grep -qxF "$(grep "^$key=" "$env_file")" && fail "$key not made again"
done
password=$(sed -nE 's/.*Password=(.*)/\1/p' "$env_file")
grep -qF "ALTER ROLE dalan LOGIN PASSWORD '$password'" "$state/commands.log" || fail "database password not changed"
signing=$(sed -nE 's/^Identity__SigningKey=(.*)/\1/p' "$env_file")
echo "$signing" | base64 -d | openssl pkey -inform DER -noout 2>/dev/null || fail "new signing key is not a key"
grep -qx 'Notification__Kavenegar__ApiKey=new-key' "$env_file" || fail "other settings lost"
[ "$(stat -c %a "$env_file")" = 640 ] || fail "dalan.env mode changed"
echo "PASS OPS-10 secrets can be made again"

# Rule: OPS-09 install.sh downloads Caddy on the owner's machine (the CI version, checked) and runs setup over SSH
inst="$tmp/inst"; mkdir -p "$inst/bin"
export DALAN_CACHE="$inst/cache"
ci_version=$(sed -nE 's/^ *CADDY_VERSION: "(.*)"/\1/p' "$deploy/../.github/workflows/ci.yml")
cat > "$inst/bin/curl" <<SH
#!/bin/bash
out=""; while [ \$# -gt 0 ]; do case "\$1" in -o) out="\$2"; shift 2;; http*) url="\$1"; shift;; *) shift;; esac; done
echo "\$url" >> "$inst/urls.log"
case "\$url" in
  *checksums.txt) (cd "$inst" && sha512sum caddy_${ci_version}_linux_amd64.tar.gz) > "\$out";;
  *dotnet-install.sh) cp "$inst/dotnet-install.sh" "\$out";;
  *SHASUMS256.txt) (cd "$inst" && sha256sum node-v26.0.0-linux-x64.tar.xz) > "\$out";;
  *node-v26.0.0-linux-x64.tar.xz) cp "$inst/node-v26.0.0-linux-x64.tar.xz" "\$out";;
  *) cp "$inst/caddy_${ci_version}_linux_amd64.tar.gz" "\$out";;
esac
SH
# The fake ssh writes each call down; the call that unpacks the setup folder unpacks it into $inst/copied.
cat > "$inst/bin/ssh" <<SH
#!/bin/bash
echo "ssh \$*" >> "$inst/remote.log"
case "\$*" in *"tar --no-same-owner -xf - -C /tmp"*) mkdir -p "$inst/unpack" && tar -xf - -C "$inst/unpack" && mv "$inst/unpack/dalan-setup" "$inst/copied";; esac
SH
chmod +x "$inst/bin"/*
mkdir "$inst/pkg" && printf '#!/bin/sh\n' > "$inst/pkg/caddy" && tar -czf "$inst/caddy_${ci_version}_linux_amd64.tar.gz" -C "$inst/pkg" caddy
PATH="$inst/bin:$PATH" bash "$deploy/install.sh" admin@server dalanet.ir api.dalanet.ir "$src/deploy.pub" >/dev/null || fail "install.sh failed"
grep -q "releases/download/v${ci_version}/caddy_${ci_version}_linux_amd64.tar.gz" "$inst/urls.log" || fail "install.sh did not fetch Caddy $ci_version"
for f in caddy setup.sh drill.sh backup.sh Caddyfile caddy.service deploy.pub; do [ -f "$inst/copied/$f" ] || fail "$f not copied"; done
grep -q "ssh -t admin@server sudo bash /tmp/dalan-setup/setup.sh 'dalanet.ir' 'api.dalanet.ir' /tmp/dalan-setup/deploy.pub" "$inst/remote.log" || fail "setup not run: $(cat "$inst/remote.log")"
[ -f "$inst/copied/settings.env" ] && [ ! -s "$inst/copied/settings.env" ] || fail "settings.env should be empty without settings"
rm -rf "$inst/copied" "$inst/remote.log"
ADMIN_PHONES=09121234567 SMS=log KAVENEGAR_KEY='a b' PATH="$inst/bin:$PATH" \
  bash "$deploy/install.sh" admin@server dalanet.ir api.dalanet.ir "$src/deploy.pub" >/dev/null || fail "install.sh with settings failed"
( . "$inst/copied/settings.env" && [ "$ADMIN_PHONES" = 09121234567 ] && [ "$SMS" = log ] && [ "$KAVENEGAR_KEY" = 'a b' ] ) \
  || fail "settings not passed: $(cat "$inst/copied/settings.env")"
grep -q 'KAVENEGAR\|0912' "$inst/remote.log" && fail "settings on the command line"
echo "PASS OPS-09 install.sh copies Caddy and runs setup"

# Rule: OPS-14 GitHub settings set the GitHub lines and keep the rest
cp "$env_file" "$tmp/env-before"
printf 'GITHUB_CLIENT_ID=abc\nGITHUB_CLIENT_SECRET=s3\n' > "$src/settings.env"
: > "$state/commands.log"
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > "$tmp/out3" || { cat "$tmp/out3"; fail "run with GitHub failed"; }
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > "$tmp/out3" || { cat "$tmp/out3"; fail "second run with GitHub failed"; }
[ "$(grep -c '^Identity__GitHub__ClientId=abc$' "$env_file")" = 1 ] || fail "client id: $(grep GitHub "$env_file")"
[ "$(grep -c '^Identity__GitHub__ClientSecret=s3$' "$env_file")" = 1 ] || fail "client secret"
[ "$(grep -c '^Identity__GitHub__CallbackUrl=https://dalanet.ir/api/auth/github/callback$' "$env_file")" = 1 ] || fail "callback url"
diff <(grep -v '^Identity__GitHub__' "$env_file") <(grep -v '^Identity__GitHub__' "$tmp/env-before") > /dev/null || fail "other lines changed"
[ "$(stat -c %a "$env_file")" = 640 ] || fail "dalan.env mode is $(stat -c %a "$env_file")"
grep -qF 'systemctl try-restart dalan.service' "$state/commands.log" || fail "dalan not restarted"
echo "PASS OPS-14 GitHub settings set the GitHub lines"

# Rule: OPS-14 without GitHub settings dalan.env does not change
rm "$src/settings.env"
before=$(sha256sum < "$env_file")
bash "$src/setup.sh" dalanet.ir api.dalanet.ir "$src/deploy.pub" > "$tmp/out4" || { cat "$tmp/out4"; fail "run without GitHub failed"; }
[ "$(sha256sum < "$env_file")" = "$before" ] || fail "dalan.env changed without GitHub settings"
echo "PASS OPS-14 no GitHub settings keep dalan.env"

# Rule: OPS-16 install.sh with PULL_DEPLOY=1 brings Node and the .NET SDK with their symlinks (npm is one: a copy cannot find its files)
mkdir -p "$inst/node/node-v26.0.0-linux-x64/bin" "$inst/node/node-v26.0.0-linux-x64/lib/node_modules/npm/bin"
echo 'npm' > "$inst/node/node-v26.0.0-linux-x64/lib/node_modules/npm/bin/npm-cli.js"
ln -s ../lib/node_modules/npm/bin/npm-cli.js "$inst/node/node-v26.0.0-linux-x64/bin/npm"
tar -cJf "$inst/node-v26.0.0-linux-x64.tar.xz" -C "$inst/node" node-v26.0.0-linux-x64
cat > "$inst/dotnet-install.sh" <<'SH'
#!/bin/bash
while [ $# -gt 0 ]; do [ "$1" = --install-dir ] && dir="$2"; shift; done
# Like the real script: an SDK that is already there is not installed again.
[ -e "$dir/dotnet" ] || { mkdir -p "$dir/sdk" && echo sdk > "$dir/sdk/dotnet.dll" && ln -s sdk/dotnet.dll "$dir/dotnet"; }
SH
rm -rf "$inst/copied" "$inst/remote.log"
PULL_DEPLOY=1 PATH="$inst/bin:$PATH" bash "$deploy/install.sh" admin@server dalanet.ir api.dalanet.ir "$src/deploy.pub" >/dev/null \
  || fail "install.sh with PULL_DEPLOY failed"
[ "$(readlink "$inst/copied/node/bin/npm")" = ../lib/node_modules/npm/bin/npm-cli.js ] || fail "npm is not a symlink on the server"
[ "$(readlink "$inst/copied/dotnet/dotnet")" = sdk/dotnet.dll ] || fail "the .NET SDK lost its symlinks"
for f in pull-deploy.sh dalan-pull.service dalan-pull.timer; do [ -f "$inst/copied/$f" ] || fail "$f not copied"; done
echo "PASS OPS-16 install.sh brings Node and the .NET SDK with their symlinks"

# Rule: OPS-16 a second install.sh run takes Caddy, Node and the .NET SDK from the cache on this computer
[ -f "$DALAN_CACHE/dotnet-11.0/sdk/dotnet.dll" ] || fail "the .NET SDK is not installed into the cache"
rm -rf "$inst/copied" "$inst/remote.log" "$inst/urls.log"
PULL_DEPLOY=1 PATH="$inst/bin:$PATH" bash "$deploy/install.sh" admin@server dalanet.ir api.dalanet.ir "$src/deploy.pub" >/dev/null \
  || fail "second install.sh with PULL_DEPLOY failed"
grep -E 'caddy_.*linux_amd64\.tar\.gz$|node-v.*\.tar\.xz$|dotnet-install\.sh$' "$inst/urls.log" && fail "downloaded again: $(cat "$inst/urls.log")"
[ "$(readlink "$inst/copied/node/bin/npm")" = ../lib/node_modules/npm/bin/npm-cli.js ] || fail "npm from the cache is not a symlink"
[ "$(readlink "$inst/copied/dotnet/dotnet")" = sdk/dotnet.dll ] || fail "the .NET SDK from the cache lost its symlinks"
[ -f "$inst/copied/caddy" ] || fail "Caddy from the cache not copied"
echo "PASS OPS-16 a second install.sh run downloads Caddy, Node and the .NET SDK only once"

# Rule: OPS-14 install.sh sends the secret in a file, never on a command line
rm -rf "$inst/copied" "$inst/remote.log"
GITHUB_CLIENT_ID=abc GITHUB_CLIENT_SECRET=very-secret PATH="$inst/bin:$PATH" \
  bash "$deploy/install.sh" admin@server dalanet.ir api.dalanet.ir "$src/deploy.pub" >/dev/null || fail "install.sh with GitHub failed"
grep -q 'very-secret' "$inst/remote.log" && fail "secret on a command line"
( . "$inst/copied/settings.env" && [ "$GITHUB_CLIENT_ID" = abc ] && [ "$GITHUB_CLIENT_SECRET" = very-secret ] ) || fail "GitHub settings not sent"
[ "$(stat -c %a "$inst/copied/settings.env")" = 600 ] || fail "settings.env mode is $(stat -c %a "$inst/copied/settings.env")"
echo "PASS OPS-14 install.sh sends the GitHub secret in a file"
