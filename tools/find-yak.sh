# Sets $yak to the Yak command line tool. Sourced by the other scripts; override with YAK=/path/to/yak.
yak="${YAK:-}"
if [ -z "$yak" ]; then
  for candidate in "$(command -v yak || true)" \
      "/Applications/Rhino 8.app/Contents/Resources/bin/yak" \
      "/Applications/RhinoWIP.app/Contents/Resources/bin/yak" \
      "/c/Program Files/Rhino 8/System/Yak.exe"; do
    if [ -n "$candidate" ] && [ -x "$candidate" ]; then yak="$candidate"; break; fi
  done
fi
[ -n "$yak" ] || { echo "yak not found; set YAK=/path/to/yak" >&2; exit 1; }
