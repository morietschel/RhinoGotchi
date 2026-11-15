#!/usr/bin/env bash
# Makes Rhino load your local Debug build at startup, the same way it loads a Package Manager
# install, so you don't need to drag the .rhp in or press F5:
#   ~/Library/Application Support/McNeel/Rhinoceros/packages/<rhino>/Rhinogotchi/<version>
#     -> src/RhinoGotchi/bin/Debug/net7.0   (a link: every rebuild is picked up on the next start)
#
#   ./tools/dev-install.sh             Rhino 9 (default)
#   ./tools/dev-install.sh 8.0         Rhino 8
#   ./tools/dev-install.sh --remove    undo (removes only the link this script made)
#
# Restart Rhino afterwards. Uninstall any Package Manager copy of Rhinogotchi first: both use
# the same folder, and this script refuses to replace a real install.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
proj="$root/src/RhinoGotchi/RhinoGotchi.csproj"
build="$root/src/RhinoGotchi/bin/Debug/net7.0"

rhino="9.0"
remove=false
for arg in "$@"; do
  case "$arg" in
    --remove) remove=true ;;
    *) rhino="$arg" ;;
  esac
done

case "$(uname)" in
  Darwin) packages="$HOME/Library/Application Support/McNeel/Rhinoceros/packages/$rhino" ;;
  *) packages="${APPDATA:?}/McNeel/Rhinoceros/packages/$rhino" ;;
esac
pkg="$packages/Rhinogotchi"
version="$(dotnet msbuild "$proj" -getProperty:Version)"

if $remove; then
  if [ -L "$pkg/$version" ]; then
    rm "$pkg/$version"
    rm -f "$pkg/manifest.txt"
    rmdir "$pkg" 2>/dev/null || true
    echo "Removed the dev link. Restart Rhino."
  else
    echo "No dev link at $pkg/$version; nothing removed."
  fi
  exit 0
fi

if [ -e "$pkg/$version" ] && [ ! -L "$pkg/$version" ]; then
  echo "A real Rhinogotchi $version install is in $pkg." >&2
  echo "Uninstall it in Rhino's PackageManager first, then run this again." >&2
  exit 1
fi

dotnet build "$proj" -c Debug -f net7.0 -v quiet -nologo

# Package Manager reads the package's name and version from manifest.yml next to the plugin.
sed "s/^version: .*/version: $version/" "$root/yak/manifest.yml" > "$build/manifest.yml"
cp "$root/yak/icon.png" "$build/icon.png"

mkdir -p "$pkg"
ln -sfn "$build" "$pkg/$version"
printf '%s\n' "$version" > "$pkg/manifest.txt"

echo "Rhinogotchi $version now loads from $build"
echo "Restart Rhino (Cmd+Q, then open it) and type RhinoGotchi."
