#!/bin/sh
# Installs the Dalan agent (AGT-10). Run: curl -fsSL __DALAN_URL__/install.sh | sh
# Puts one file, "dalan", in ~/.local/bin (or $DALAN_INSTALL). Needs no root and no .NET.
set -eu

case "$(uname -s)-$(uname -m)" in
  Linux-x86_64 | Linux-amd64) file=dalan-linux-x64 ;;
  Darwin-arm64) file=dalan-macos-arm64 ;;
  Darwin-x86_64) file=dalan-macos-x64 ;;
  *) echo "Sorry, there is no Dalan agent for $(uname -s) $(uname -m) yet." >&2; exit 1 ;;
esac

dir="${DALAN_INSTALL:-$HOME/.local/bin}"
mkdir -p "$dir"
echo "Downloading $file"
curl -fSL --progress-bar -o "$dir/dalan.new" "__DALAN_URL__/downloads/$file" \
  || { rm -f "$dir/dalan.new"; echo "This server has no agent for $(uname -s) $(uname -m) yet." >&2; exit 1; }
chmod +x "$dir/dalan.new"
mv -f "$dir/dalan.new" "$dir/dalan"
echo "Installed: $dir/dalan ($("$dir/dalan" version))"

case ":$PATH:" in
  *":$dir:"*) ;;
  *)
    for profile in "$HOME/.bashrc" "$HOME/.zshrc" "$HOME/.profile"; do
      [ -f "$profile" ] || continue
      grep -qs "$dir" "$profile" || printf '\nexport PATH="%s:$PATH"\n' "$dir" >> "$profile"
    done
    echo "Open a new terminal (or run: export PATH=\"$dir:\$PATH\") so the command dalan is found."
    ;;
esac
echo "Next: the login command from the My apps page."
