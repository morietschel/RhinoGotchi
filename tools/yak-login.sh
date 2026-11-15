#!/usr/bin/env bash
# Signs in to Rhino's Package Manager with your Rhino account (opens the browser). Needed once.
set -euo pipefail
source "$(cd "$(dirname "$0")" && pwd)/find-yak.sh"
"$yak" login
