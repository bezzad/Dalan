#!/usr/bin/env bash
# OPS-18: one Let's Encrypt certificate for *.<sites domain>, by a DNS challenge at ArvanCloud (lego).
# dalan-cert.timer runs it daily as root (dalan-cert.service). lego gets the certificate, or renews it when 30 days or
# fewer are left; only then it runs this script again as its deploy hook ("hook <domain>"), which copies the
# certificate where Caddy can read it, writes Caddy's site block for *.<sites domain> and restarts Caddy.
# Caddy has "admin off", so it cannot reload: a restart is the way. Messages: journalctl -u dalan-cert.
#
# Settings (environment; dalan-cert.service reads /etc/dalan/domains.env and /etc/dalan/arvan.env):
#   Sites__Domain        the users' sites, for example dalans.ir
#   ARVANCLOUD_API_KEY   the ArvanCloud API key, written like "Apikey <key>"
#   CERT_EMAIL           optional: the Let's Encrypt account email
#   DALAN_ROOT           for tests only (default empty, the real /)
set -euo pipefail
root="${DALAN_ROOT:-}"
state="$root/var/lib/dalan-cert"

if [ "${1:-}" = hook ]; then
  domain="${2:?hook needs the sites domain}"
  certs="$root/etc/caddy/certs"
  # Caddy runs as a dynamic user in the group dalan-cert (caddy.service); others cannot read the key.
  install -d -m 750 "$certs"
  chgrp dalan-cert "$certs"
  install -m 644 "$state/certificates/_.$domain.crt" "$certs/wildcard.crt"
  install -m 640 "$state/certificates/_.$domain.key" "$certs/wildcard.key"
  chgrp dalan-cert "$certs/wildcard.key"
  # The Caddyfile imports wildcard*.caddy; before the first certificate there is none, and each site gets its own (OPS-15).
  printf '# Made by deploy/wildcard-cert.sh (OPS-18). Every site host uses the wildcard certificate.\n*.%s {\n\ttls %s %s\n\timport site\n}\n' \
    "$domain" "$certs/wildcard.crt" "$certs/wildcard.key" > "$root/etc/caddy/wildcard.caddy"
  # A config Caddy cannot load would keep it down after the restart, dalanet.ir too: then the block goes and Caddy keeps
  # running as it was (each site with its own certificate).
  if ! checked=$(XDG_DATA_HOME="$state" XDG_CONFIG_HOME="$state" caddy validate --config "$root/etc/caddy/Caddyfile" --adapter caddyfile 2>&1); then
    rm -f "$root/etc/caddy/wildcard.caddy"
    printf '%s\n' "$checked" | tail -n 5 >&2
    echo "Caddy cannot use the certificate for *.$domain; Caddy keeps running as it was" >&2
    exit 1
  fi
  systemctl restart caddy.service
  echo "New certificate for *.$domain; Caddy restarted"
  exit 0
fi

domain="${Sites__Domain:?no sites domain (Sites__Domain in /etc/dalan/domains.env)}"
: "${ARVANCLOUD_API_KEY:?no ArvanCloud key (/etc/dalan/arvan.env)}"
export ARVANCLOUD_API_KEY
args=(run --accept-tos --dns arvancloud -d "*.$domain" --path "$state" --renew-days 30 --deploy-hook "$0 hook $domain")
[ -z "${CERT_EMAIL:-}" ] || args+=(--email "$CERT_EMAIL")
lego "${args[@]}"
