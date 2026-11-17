#!/usr/bin/env bash
# Build the release: one self-contained linux-x64 file plus appsettings.json (DEL-03).
# The PWA client is built first and embedded in that file (CLI-02).
# Usage: scripts/publish.sh <output-folder>
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:?output folder is required}"

(cd "$root/client" && npm ci && npm run build)

dotnet publish "$root/src/Dalan.Host/Dalan.Host.csproj" -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none -p:IsTransformWebConfigDisabled=true -p:StaticWebAssetsEnabled=false \
  -o "$out"
