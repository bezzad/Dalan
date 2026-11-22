#!/usr/bin/env bash
# Rule: AGT-10
# The one-line install script picks the agent file for the user's system (fake uname and curl, no network).
set -euo pipefail
script="$(cd "$(dirname "$0")/../.." && pwd)/src/Sites/Dalan.Sites/Downloads/install.sh"
fail() { echo "FAIL: $*"; exit 1; }
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT

# install <system> <cpu>: runs the script as the server serves it; prints the file it downloaded.
install() {
  rm -rf "$tmp/bin" "$tmp/home" && mkdir -p "$tmp/bin" "$tmp/home"
  printf '#!/bin/sh\n[ "$1" = -s ] && echo %s || echo %s\n' "$1" "$2" > "$tmp/bin/uname"
  # The fake curl writes a small agent that answers "dalan version", and records the address.
  cat > "$tmp/bin/curl" <<SH
#!/bin/sh
while [ \$# -gt 1 ]; do [ "\$1" = -o ] && out="\$2"; shift; done
echo "\$1" > "$tmp/url"
printf '#!/bin/sh\necho dalan 1.0\n' > "\$out"
SH
  chmod +x "$tmp/bin/uname" "$tmp/bin/curl"
  rm -f "$tmp/url"
  sed 's|__DALAN_URL__|https://dalan.test|g' "$script" > "$tmp/install.sh"
  HOME="$tmp/home" PATH="$tmp/bin:/usr/bin:/bin" sh "$tmp/install.sh" > "$tmp/out" 2>&1
}

for case in 'Linux x86_64 dalan-linux-x64' 'Darwin arm64 dalan-macos-arm64' 'Darwin x86_64 dalan-macos-x64'; do
  set -- $case
  install "$1" "$2" || fail "install on $1 $2 failed: $(cat "$tmp/out")"
  [ "$(cat "$tmp/url")" = "https://dalan.test/downloads/$3" ] || fail "$1 $2 downloaded $(cat "$tmp/url"), not $3"
  [ -x "$tmp/home/.local/bin/dalan" ] || fail "$1 $2: dalan not installed"
done
echo "PASS AGT-10 the install script takes the Linux or the Mac file (Apple or Intel CPU)"

install FreeBSD amd64 && fail "an unknown system must fail"
grep -q 'no Dalan agent for FreeBSD amd64' "$tmp/out" || fail "no clear message: $(cat "$tmp/out")"
[ ! -e "$tmp/url" ] && [ ! -e "$tmp/home/.local/bin/dalan" ] || fail "an unknown system downloaded or installed something"
echo "PASS AGT-10 another system gets a clear message and nothing is installed"
