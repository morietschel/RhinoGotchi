#!/usr/bin/env bash
# Builds the Package Manager (Yak) packages into dist/:
#   rhinogotchi-<version>-rh8_0-any.yak   Rhino 8, Windows and Mac (net7.0 and net48)
#   rhinogotchi-<version>-rh9_0-any.yak   Rhino 9, Windows and Mac (net7.0)
# Publish with tools/deploy.sh
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
proj="$root/src/RhinoGotchi/RhinoGotchi.csproj"
bin="$root/src/RhinoGotchi/bin/Release"
dist="$root/dist"

source "$root/tools/find-yak.sh"

version="$(dotnet msbuild "$proj" -getProperty:Version)"
rm -rf "$bin" "$dist"
dotnet build "$proj" -c Release -v quiet -nologo

# stage <folder> <from>:<to>...   copies each build (without debug symbols) into the package folder
stage() {
  local dir="$1"; shift
  mkdir -p "$dir"
  cp "$root/yak/manifest.yml" "$root/yak/icon.png" "$dir/"
  for pair in "$@"; do
    mkdir -p "$dir/${pair#*:}"
    (cd "$bin/${pair%%:*}" && tar cf - --exclude='*.pdb' .) | (cd "$dir/${pair#*:}" && tar xf -)
  done
}

# Rhino 8 picks the net7.0 or net48 folder to match the runtime it was started with.
stage "$dist/rh8" net7.0:net7.0 net48:net48
# Rhino 9 is .NET only, so its package is the net7.0 build on its own.
stage "$dist/rh9" net7.0:.

for rh in rh8 rh9; do
  (cd "$dist/$rh" && "$yak" build --platform any --version "$version" >/dev/null)
  built="$(ls "$dist/$rh"/*.yak)"
  # Yak tags a package after the RhinoCommon it was compiled against (Rhino 8.0); retag the Rhino 9 one.
  mv "$built" "$dist/$(basename "$built" | sed -E "s/-rh[0-9]+_[0-9]+-/-${rh}_0-/")"
  rm -rf "$dist/$rh"
done

ls -lh "$dist"
