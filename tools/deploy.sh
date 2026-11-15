#!/usr/bin/env bash
# Builds the packages and publishes them to Rhino's Package Manager.
#   ./tools/deploy.sh          publish to yak.rhino3d.com (permanent: raise <Version> first)
#   ./tools/deploy.sh --test   publish to the test server, test.yak.rhino3d.com
# Sign in once beforehand with ./tools/yak-login.sh
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
source "$root/tools/find-yak.sh"

server="https://yak.rhino3d.com"
[ "${1:-}" = "--test" ] && server="https://test.yak.rhino3d.com"

"$root/tools/build-yak.sh"

echo
read -r -p "Publish these to $server? A published version can't be replaced. [y/N] " answer
[ "$answer" = "y" ] || { echo "Nothing published."; exit 0; }

for package in "$root"/dist/*.yak; do
  "$yak" push --source "$server" "$package"
done
echo "Published. It can take a minute to show up in Rhino's PackageManager."
