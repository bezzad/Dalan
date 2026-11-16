#!/usr/bin/env bash
# Build the release: one self-contained linux-x64 file plus appsettings.json (DEL-03).
# Usage: scripts/publish.sh <output-folder>
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:?output folder is required}"

dotnet publish "$root/src/Dalan.Host/Dalan.Host.csproj" -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none -p:IsTransformWebConfigDisabled=true -p:StaticWebAssetsEnabled=false \
  -o "$out"
