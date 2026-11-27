#!/usr/bin/env bash
# deploy/wildcard-cert.sh with a fake lego, systemctl and chgrp, and a fake root folder.
set -euo pipefail
repo="$(cd "$(dirname "$0")/../.." && pwd)"
script="$repo/deploy/wildcard-cert.sh"
fail() { echo "FAIL: $*"; exit 1; }
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
root="$tmp/root"; bin="$tmp/bin"
mkdir -p "$bin" "$root/etc/caddy" "$root/var/lib/dalan-cert"

# The fake lego writes down its arguments and the key it got. With $tmp/NEW it makes the certificate and runs the
# deploy hook, as the real one does when it made or renewed one; without it the certificate is not due: it does nothing.
cat > "$bin/lego" <<SH
#!/bin/bash
echo "\$*" >> "$tmp/lego.log"; echo "\$ARVANCLOUD_API_KEY" > "$tmp/lego.key"
[ -f "$tmp/NEW" ] || exit 0
while [ \$# -gt 0 ]; do case "\$1" in --path) path="\$2"; shift 2;; -d) domain="\$2"; shift 2;; --deploy-hook) hook="\$2"; shift 2;; *) shift;; esac; done
mkdir -p "\$path/certificates"; name="\${domain/\*/_}"
echo cert > "\$path/certificates/\$name.crt"; echo key > "\$path/certificates/\$name.key"
\$hook
SH
printf '#!/bin/sh\necho "systemctl $*" >> %s/commands.log\n' "$tmp" > "$bin/systemctl"
printf '#!/bin/sh\necho "chgrp $*" >> %s/commands.log\n' "$tmp" > "$bin/chgrp"
chmod +x "$bin"/*
export PATH="$bin:$PATH" DALAN_ROOT="$root" Sites__Domain=dalans.ir ARVANCLOUD_API_KEY='Apikey 1234-abcd' CERT_EMAIL=owner@example.ir

# Rule: OPS-18 first certificate: copied for Caddy, the site block written, Caddy restarted once
touch "$tmp/NEW"
bash "$script" > "$tmp/out" || { cat "$tmp/out"; fail "first run failed"; }
args=$(cat "$tmp/lego.log")
for want in '--dns arvancloud' "-d *.dalans.ir" "--path $root/var/lib/dalan-cert" '--accept-tos' '--renew-days 30' \
            '--email owner@example.ir' "--deploy-hook $script hook dalans.ir"; do
  [[ "$args" == *"$want"* ]] || fail "lego not called with '$want': $args"
done
[[ "$args" == "run "* ]] || fail "lego does not start with run: $args"
[ "$(cat "$tmp/lego.key")" = 'Apikey 1234-abcd' ] || fail "lego did not get the ArvanCloud key"
certs="$root/etc/caddy/certs"
[ "$(cat "$certs/wildcard.crt")" = cert ] && [ "$(cat "$certs/wildcard.key")" = key ] || fail "certificate not copied for Caddy"
[ "$(stat -c %a "$certs/wildcard.key")" = 640 ] || fail "key mode is $(stat -c %a "$certs/wildcard.key")"
[ "$(stat -c %a "$certs")" = 750 ] || fail "certificate folder mode is $(stat -c %a "$certs")"
grep -qx "chgrp dalan-cert $certs/wildcard.key" "$tmp/commands.log" || fail "Caddy's group cannot read the key"
grep -qx "chgrp dalan-cert $certs" "$tmp/commands.log" || fail "Caddy's group cannot open the folder"
block="$root/etc/caddy/wildcard.caddy"
grep -qx '\*\.dalans\.ir {' "$block" || fail "no site block for *.dalans.ir: $(cat "$block")"
grep -qF "tls $certs/wildcard.crt $certs/wildcard.key" "$block" || fail "the site block does not use the certificate: $(cat "$block")"
grep -qx '	import site' "$block" || fail "the site block does not proxy like the other sites"
[ "$(grep -c 'systemctl restart caddy.service' "$tmp/commands.log")" = 1 ] || fail "Caddy not restarted once: $(cat "$tmp/commands.log")"
echo "PASS OPS-18 first certificate: Caddy gets it, its block and one restart"

# Rule: OPS-18 nothing due: nothing copied, Caddy not restarted
rm "$tmp/NEW"; : > "$tmp/commands.log"; : > "$tmp/lego.log"
echo old > "$certs/wildcard.crt"
bash "$script" > "$tmp/out" || { cat "$tmp/out"; fail "run with nothing due failed"; }
[ -s "$tmp/lego.log" ] || fail "lego not asked"
[ "$(cat "$certs/wildcard.crt")" = old ] || fail "certificate copied with nothing due"
[ -s "$tmp/commands.log" ] && fail "something ran with nothing due: $(cat "$tmp/commands.log")"
echo "PASS OPS-18 nothing due: no copy, no restart"

# Rule: OPS-18 no email: lego gets no --email; no key or no sites domain: lego is not run
: > "$tmp/lego.log"
CERT_EMAIL='' bash "$script" > /dev/null || fail "run without an email failed"
grep -q -- '--email' "$tmp/lego.log" && fail "lego got an empty email"
: > "$tmp/lego.log"
ARVANCLOUD_API_KEY='' bash "$script" > /dev/null 2>&1 && fail "ran without the key"
Sites__Domain='' bash "$script" > /dev/null 2>&1 && fail "ran without a sites domain"
[ -s "$tmp/lego.log" ] && fail "lego run without the key or the domain"
echo "PASS OPS-18 no key or no sites domain: lego is not run"

# Rule: OPS-18 the units: daily at a quiet hour, missed runs made up, the key read only by dalan-cert.service
timer="$repo/deploy/dalan-cert.timer"; service="$repo/deploy/dalan-cert.service"
grep -qx 'OnCalendar=\*-\*-\* 04:17:00 Asia/Tehran' "$timer" || fail "timer is not daily at 04:17 Tehran time"
grep -qx 'Persistent=true' "$timer" && grep -q '^RandomizedDelaySec=' "$timer" || fail "timer not persistent or not spread"
grep -qx 'EnvironmentFile=/etc/dalan/arvan.env' "$service" || fail "dalan-cert.service does not read the key"
grep -qx 'ExecStart=/opt/dalan/tools/wildcard-cert.sh' "$service" || fail "dalan-cert.service does not run the script"
grep -q 'arvan' "$repo/deploy/dalan.service" && fail "dalan.service must not read the ArvanCloud key"
grep -qx 'SupplementaryGroups=dalan-cert' "$repo/deploy/caddy.service" || fail "Caddy is not in the group that reads the key"
echo "PASS OPS-18 units: daily timer, key only for dalan-cert.service, Caddy reads the key by its group"
