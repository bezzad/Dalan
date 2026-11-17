#!/usr/bin/env bash
# Shutdown drill (OPS-12): block new outbound connections from this server, as if international links were cut.
# Inbound connections (users, bots, SSH) and their replies still pass. Run as root, on the server.
#
# Usage: drill.sh start | stop
#   start  block outbound except loopback, private ranges and the allow list; stop by itself after DRILL_MINUTES
#   stop   remove the block and print each blocked address once, with its ports
#
# Settings: /etc/dalan/drill.env may set DRILL_MINUTES (default 60).
# Allow list: /etc/dalan/drill-allow.txt, one IPv4 or IPv6 range per line (for example the Iranian ranges).
# For tests: DRILL_ENV, DRILL_ALLOW_FILE, DRILL_STATE.
set -euo pipefail

env_file="${DRILL_ENV:-/etc/dalan/drill.env}"
allow_file="${DRILL_ALLOW_FILE:-/etc/dalan/drill-allow.txt}"
state="${DRILL_STATE:-/run/dalan-drill.start}"
[ -f "$env_file" ] && . "$env_file"
minutes="${DRILL_MINUTES:-60}"
table=dalan_drill
self="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"

remove_block() {
  nft list table inet "$table" >/dev/null 2>&1 && nft delete table inet "$table"
  systemctl stop dalan-drill-stop.timer >/dev/null 2>&1 || true
  systemctl reset-failed dalan-drill-stop.timer dalan-drill-stop.service >/dev/null 2>&1 || true
}

# Ranges from the allow list, comma separated: IPv4 when $1 is 4, IPv6 when $1 is 6.
ranges() {
  [ -f "$allow_file" ] || return 0
  local lines
  lines=$(grep -vE '^\s*(#|$)' "$allow_file" | tr -d ' \r' || true)
  if [ "$1" = 6 ]; then printf '%s\n' "$lines" | grep ':'; else printf '%s\n' "$lines" | grep -v ':'; fi | grep . | paste -sd, - || true
}

start() {
  remove_block
  v4="127.0.0.0/8, 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 100.64.0.0/10, 169.254.0.0/16"
  v6="::1/128, fc00::/7, fe80::/10"
  extra4=$(ranges 4); extra6=$(ranges 6)
  nft -f - <<NFT
table inet $table {
  set allow4 { type ipv4_addr; flags interval; auto-merge; elements = { $v4${extra4:+, $extra4} } }
  set allow6 { type ipv6_addr; flags interval; auto-merge; elements = { $v6${extra6:+, $extra6} } }
  chain output {
    type filter hook output priority 0; policy accept;
    oif "lo" accept
    ct state established,related accept
    ip daddr @allow4 accept
    ip6 daddr @allow6 accept
    limit rate 20/second burst 50 packets log prefix "dalan-drill " level info
    drop
  }
}
NFT
  date +%s > "$state"
  systemd-run --quiet --unit=dalan-drill-stop --on-active="${minutes}m" "$self" stop
  echo "Drill is on. It stops by itself in $minutes minutes, or run: $self stop"
}

stop() {
  remove_block
  since=$(cat "$state" 2>/dev/null || echo 0)
  rm -f "$state"
  echo "Drill is off. Blocked outbound addresses:"
  journalctl -k -q -o cat --since "@$since" --grep 'dalan-drill ' 2>/dev/null \
    | sed -nE 's/.* DST=([^ ]+) .* DPT=([0-9]+).*/\1 \2/p' | sort -u \
    | awk '{ p = ($1 in ports) ? ports[$1] "," $2 : $2; ports[$1] = p } END { for (a in ports) print "  " a " (ports " ports[a] ")" }' | sort
}

case "${1:-}" in
  start) start ;;
  stop) stop ;;
  *) echo "Usage: $0 start | stop" >&2; exit 2 ;;
esac
