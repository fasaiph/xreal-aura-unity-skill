# Device, adb and deploying

`scripts/aura.sh` wraps everything below. In Claude Code, adb needs the sandbox disabled
(`dangerouslyDisableSandbox: true`) for local-network and USB access.

## adb

adb isn't on PATH; Unity bundles it at
`/Applications/Unity/Hub/Editor/<version>/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`.

## Developer mode

Settings → About → tap Build number 7× → Developer options → Wireless debugging (and USB debugging).

## USB: the puck's ports

- ⚡ charging / DP-in port: Mac screen mirroring (glasses become an external display). No adb.
- 👓 glasses port: documented as "Full ADB support", but a Mac plugged in enumerated nothing
  (`ioreg -p IOUSB`), and with the glasses unplugged you can't see the "Allow USB debugging" prompt.
- Wireless was the only path that worked.

## Wireless debugging

```sh
# once: Developer options → Wireless debugging → Pair device with pairing code
adb pair <ip>:<pair-port> <code>        # permanent: "Successfully paired ... [guid=adb-XXXX]"
adb mdns services                         # adb-XXXX  _adb-tls-connect._tcp  <ip>:<port>
adb connect <ip>:<port>
```
- IP and port change; rediscover with `adb mdns services`.
- You may get two entries for one puck: `<ip>:<port>` and the mDNS name
  `adb-XXXX._adb-tls-connect._tcp` (sometimes `adb-XXXX (2)._adb-tls-connect._tcp`, with a space).
  The mDNS entry was more stable. Parse `adb devices` with tab as separator, and skip stale
  `emulator-*` entries.
- When the Aura sleeps or Wireless debugging switches off the connection is refused, and on
  reconnect the serial and transport id change, so retries pinned to the old `-s`/`-t` fail.
  Look the device up fresh each time and ask the user to keep the Aura awake during long jobs.
- Unity's Run Device list shows both entries ("Android XR Puck"); either works.

## Build, install, launch

- Builds were done from the Editor: File → Build And Run (⌘B), Run Device "Android XR Puck".
  If the Build Profiles window is open, a Unity bug (`ObjectDisposedException: SerializedProperty
  m_PlatformBuildProfile.m_BuildAppBundle has disappeared!`) marks the build Failed and skips the
  install even though the APK built. Close that window, or install with adb.
- From a terminal while the Editor is open: `EditorTriggers` (`Temp/build.trigger`). Batchmode
  builds need the Editor closed (project lock); untested here.
- Install and launch:
  ```sh
  adb -s $S install -r build/MyApp.apk
  adb -s $S shell am start -S -n <pkg>/com.unity3d.player.UnityPlayerGameActivity
  adb -s $S shell dumpsys package <pkg> | grep lastUpdateTime   # confirm the new build landed
  ```
- `UnityException: No Android devices connected` = adb isn't connected (pair/connect first).

## Logs

```sh
adb logcat -d -v brief --pid=$(adb shell pidof <pkg>)
adb logcat -d -v brief 'Unity:V' 'OpenXR:V' '*:S' | grep -iE 'openxr|blend|permission|error|exception'
```
- Quote tag filters in zsh (`'Unity:*'`), or it fails with "no matches found".
- macOS has no `timeout`; background the logcat and kill it, or use `-d` (dump and exit).
- XR startup lines start with `[XR]`: extension list, available blend modes, unsupported features.
- Add an app-side status log every 5–10 s with a unique prefix; it's the best remote debugger.
- Permissions: `adb shell dumpsys package <pkg> | grep -E "HAND_TRACKING|SCENE_UNDERSTANDING"`;
  grant with `adb shell pm grant <pkg> <permission>`, then `am force-stop <pkg>` and relaunch.

## Seeing what the user sees

- `adb exec-out screencap -p` returned the 1920×1200 passthrough camera image, not rendered content.
- Screen recordings from the Quick Settings tile land in `/sdcard/Movies/screen-YYYYMMDD-HHMMSS.mp4`;
  screenshots in `/sdcard/Pictures/Screenshots/`. Users found their quality poor for publishing.
- scrcpy was installed but not tested (needs `ADB=<path>`).

## Pulling recordings

- Plain `adb pull` over Wi-Fi: ~22–30 MB/s when the connection holds. Multi-GB files are common
  (a 17-minute recording was 1.6 GB).
- `adb pull` can't resume; a drop mid-file loses that file. Pull files one by one so finished
  ones survive, skip ones already present with the same size, and keep the Aura awake. Chunked
  `dd` copies work around drops but are much slower; users preferred the plain pull.
- Verify before deleting: device `stat -c %s` vs local `stat -f %z` (md5sum on both if needed).
  Users may have moved earlier pulls elsewhere, so search before re-pulling.
- Only delete from the device when the user asks; they may want to review footage first.
- `/sdcard/Movies/.thumbnails` comes along with `adb pull /sdcard/Movies/.`; drop it locally.

## Device facts

Manufacturer XREAL, model "Android XR Puck", product `axros`, device `axrpuck`; Android 14 /
API 34, arm64-v8a, Adreno (Vulkan level 1), 72 Hz default; features
`android.hardware.xr.input.hand_tracking`, `android.software.xr.api.openxr`,
`android.software.xr.api.spatial`. Unity's device string: "XREAL Android XR Puck".
