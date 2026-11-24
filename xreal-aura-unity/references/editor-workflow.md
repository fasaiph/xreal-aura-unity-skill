# Editor workflow (driving Unity from a terminal / Claude Code)

## Getting Unity to notice your edits

- Unity recompiles and reimports only while it's the **frontmost app**, and not while the Mac
  screen is locked or asleep (`screencapture` returns black then — a quick way to tell).
- Bring it forward: `open -a "/Applications/Unity/Hub/Editor/<version>/Unity.app"`. If Unity is
  already front, first `osascript -e 'tell application "Finder" to activate'`.
- Confirm the compile actually happened before testing:
  `find Library/ScriptAssemblies -name 'MyAsm.dll' -newer path/to/Edited.cs`.
- The Editor log is **per project in Unity 6**: `<Project>/Logs/Editor.log` (not
  `~/Library/Logs/Unity/`). Grep for `error CS` (plain "Error" also matches
  `ScriptAssembliesAndTypeDB`), `Importing`, `Domain Reload`, and for builds
  `Build Finished, Result:` / `Build completed with a result of`.
- `osascript` keystrokes (e.g. ⌘P) didn't work (no Accessibility permission). Use trigger files.
- A recompile during Play mode reloads the domain and kills coroutines: wait for the compile.
- The official Unity MCP needs a Unity AI subscription; everything here works via files and logs.

## Trigger files

`templates/Editor/EditorTriggers.cs`: an `[InitializeOnLoad]` class polls `<Project>/Temp/` once a
second (skipping while compiling) for `<command>.trigger`, reads its text as options, deletes it,
runs the command, and writes `<command>.result`. Built in: `build`, `play`, `stop`, `refresh`.

```sh
P=~/Developer/MyApp
echo dev > $P/Temp/build.trigger; open -a Unity
until [ -f $P/Temp/build.result ]; do sleep 2; done; cat $P/Temp/build.result
```

Patterns for custom commands:
- If Play mode is running, stop it and let the next poll start fresh.
- Pass state into Play mode with `SessionState`, and add your runtime component in
  `playModeStateChanged == EnteredPlayMode`.

## Previewing in Edit mode (Scene/Game view without Play)

Coroutines and `Destroy` don't run outside Play mode. Give generators an "instant" path, use
`DestroyImmediate`, mark preview objects `HideFlags.DontSave`, and clear them on
`PlayModeStateChange.ExitingEditMode`. After a script reload your static list is empty, so also
find leftover `DontSave` objects when clearing. Frame them with `SceneView.lastActiveSceneView.Frame(bounds)`.

## Static state survives Play mode

With domain reload disabled (Enter Play Mode options), statics persist while Unity objects in them
are destroyed → `MissingReferenceException`. Cache lookups should be
`cache.TryGetValue(k, out var v) && v != null`, regenerating otherwise.

## Testing without the glasses

`MousePinch`: left mouse = pinch, scroll while held = push/pull, right-drag = look. It turns off
when `XRSettings.isDeviceActive`. With origin mode Device the camera starts at y = 0, so put
test content relative to the camera.

## Offline rendering for videos (transparent, with synced audio)

On-device recordings look poor; render the content in the Editor and composite over real footage.

- Deterministic time: `Time.captureFramerate = 30`; audio with `AudioRenderer.Start()`, per frame
  `AudioRenderer.GetSampleCountForCaptureFrame()` + `Render(buffer)` (48 kHz stereo), `Stop()`.
- Render a disabled camera with `RenderPipeline.SubmitRenderRequest` + `StandardRequest` into an
  sRGB ARGB32 RenderTexture; read back and encode PNGs on a worker (`ImageConversion.EncodeArrayToPNG`).
- **Difference matting** for correct alpha with an additive look: render each frame over black and
  over white; `alpha = 1 - avg(white - black)`, `colour = black / alpha`. Supersample 2× (MSAA is off).
- Pin random seeds so different takes/looks have the same layout.
- Encode with alpha:
  `ffmpeg -framerate 30 -i f_%05d.png -i audio.wav -c:v prores_ks -profile:v 4444 -pix_fmt yuva444p10le -alpha_bits 16 -vendor apl0 -c:a pcm_s16le out.mov`
- Grade while keeping alpha:
  `format=rgba,split[a][b];[a]eq=...,colorchannelmixer=...[c];[b]alphaextract[al];[c][al]alphamerge`
- In the editor/NLE, composite with Screen/Add to mimic the glasses, or Normal in a bright room.

## iCloud and Unity

If the project is under an iCloud-synced folder, iCloud writes numbered conflict copies
(" 2", " 3" … " 20") into Library/Temp and builds break (see troubleshooting). Fix in order of
permanence: `ICloudDuplicateCleaner` (deletes `* <n>` copies in Library/Temp on load and before
builds; regex `" \d+(\.[A-Za-z0-9_]+)*$"`) → symlink Library outside iCloud → **rename the project
folder to `*.nosync`** (iCloud skips it) or move it to `~/Developer`. Restart Unity after a cleanup;
duplicate assemblies stay loaded.

## zsh gotchas (macOS default shell)

- An unmatched glob aborts the whole command ("no matches found"); quote patterns.
- `$var[...]` and `$var:x` are parsed as subscripts/modifiers; write `${var}`.
- macOS bash is 3.2: no `mapfile`, no `wait -n`. No `timeout` command.
