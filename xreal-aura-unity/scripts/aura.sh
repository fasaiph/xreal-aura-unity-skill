#!/bin/zsh
# XREAL Aura helper: connect over wireless adb, install/launch/log an app, pull or clear screen recordings.
#
#   aura.sh status                     devices + mdns services
#   aura.sh connect                    connect to the Aura advertised over mdns
#   aura.sh install <apk> [package]    install (replacing) and launch
#   aura.sh launch <package>           launch an installed app
#   aura.sh log [tag]                  logcat for Unity (or a tag), from now on
#   aura.sh recordings                 list screen recordings and screenshots on the device
#   aura.sh pull [dir]                 adb pull recordings not already in dir (default ./recordings)
#   aura.sh clear-recordings           delete recordings that exist locally with the same size (asks first)
#
# Set ADB to override the adb path. Run from Claude Code with the sandbox disabled (USB + local network).

ADB=${ADB:-$(command -v adb || ls -d /Applications/Unity/Hub/Editor/*/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb 2>/dev/null | tail -1)}
[ -x "$ADB" ] || { echo "adb not found; set ADB=/path/to/adb" >&2; exit 1; }

# The device's name changes on every reconnect (and can contain spaces, e.g. "adb-XXXX (2)._adb-tls-connect._tcp"),
# so look it up fresh each call instead of caching a serial or transport id.
serial() { "$ADB" devices | sed 1d | awk -F'\t' '$2=="device" && $1!~/^emulator/{print $1; exit}'; }
need() {
  S=$(serial)
  if [ -z "$S" ]; then connect >/dev/null; S=$(serial); fi
  [ -n "$S" ] || { echo "Aura not connected. Wake it, check Developer options → Wireless debugging, then: aura.sh connect" >&2; exit 1; }
}
a() { "$ADB" -s "$S" "$@"; }

connect() {
  local target
  target=$("$ADB" mdns services | awk '/_adb-tls-connect/{print $NF; exit}')
  if [ -z "$target" ]; then echo "No Aura advertised on the network (asleep, or Wireless debugging off)." >&2; return 1; fi
  "$ADB" connect "$target"
}

remote_media() { a shell 'ls /sdcard/Movies/*.mp4 /sdcard/Pictures/Screenshots/* 2>/dev/null' | tr -d '\r'; }

case "$1" in
  status) "$ADB" devices -l; "$ADB" mdns services ;;
  connect) connect && "$ADB" devices -l ;;
  install)
    need; apk=$2; pkg=$3
    a install -r "$apk" || exit 1
    if [ -z "$pkg" ]; then
      aapt=$(ls -d "${ADB:h:h}"/build-tools/*/aapt 2>/dev/null | tail -1)   # SDK/build-tools, next to platform-tools
      [ -n "$aapt" ] && pkg=$("$aapt" dump badging "$apk" | sed -n "s/^package: name='\([^']*\)'.*/\1/p")
    fi
    [ -n "$pkg" ] && a shell monkey -p "$pkg" -c android.intent.category.LAUNCHER 1 >/dev/null && echo "launched $pkg" ;;
  launch) need; a shell monkey -p "$2" -c android.intent.category.LAUNCHER 1 ;;
  log) need; a logcat -c; a logcat -v time -s "${2:-Unity}" ;;
  recordings) need; a shell 'ls -l /sdcard/Movies/*.mp4 /sdcard/Pictures/Screenshots/* 2>/dev/null' ;;
  pull)
    need; dir=${2:-recordings}; mkdir -p "$dir"
    for f in $(remote_media); do
      name=${f:t}
      # Skip files already pulled (anywhere under dir) with a matching size.
      size=$(a shell stat -c %s "$f" | tr -d '\r')
      local_copy=$(find "$dir" -name "$name" -size "${size}c" 2>/dev/null | head -1)
      if [ -n "$local_copy" ]; then echo "have $name"; continue; fi
      a pull "$f" "$dir/" || echo "FAILED $name (reconnect and run pull again; finished files are skipped)"
    done ;;
  clear-recordings)
    need; dir=${2:-recordings}; todo=()
    for f in $(remote_media); do
      size=$(a shell stat -c %s "$f" | tr -d '\r')
      if [ -n "$(find "$dir" -name "${f:t}" -size "${size}c" 2>/dev/null | head -1)" ]; then todo+=$f; else echo "keeping ${f:t} (no matching local copy)"; fi
    done
    [ ${#todo} -eq 0 ] && { echo "nothing to clear"; exit 0; }
    printf '%s\n' $todo; read -q "?Delete these ${#todo} files from the Aura? [y/N] " || exit 0; echo
    for f in $todo; do a shell rm "'$f'"; done; echo "deleted ${#todo}" ;;
  *) sed -n '2,13p' "$0" ;;
esac
