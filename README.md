# XREAL Aura × Unity — Claude skill

A [Claude Code skill](https://docs.claude.com/en/docs/claude-code/skills) for building Unity apps
for the **XREAL Aura** (Android XR): project setup, the see-through blend-mode fix, permissions,
hand pinch, depth surfaces, real-world lighting, wireless adb, and the errors you will hit along
the way, with fixes. Written from building *Aura Flowers* on a real Aura in
November 2026 (Unity 6000.6.9f1, Android XR OpenXR 1.5.1).

Not affiliated with XREAL or Unity — field notes from a creator, *Fasai Builds XR*.

## Install

```sh
git clone <this repo> ~/Developer/xreal-aura-unity-skill
ln -s ~/Developer/xreal-aura-unity-skill/xreal-aura-unity ~/.claude/skills/xreal-aura-unity
```
Claude Code picks it up automatically when you work on Aura / Android XR Unity projects.

## What's inside

```
xreal-aura-unity/
  SKILL.md                 rules that matter most + map of the references
  references/
    setup.md               versions, OpenXR features, player settings, permissions, rig
    display-and-rendering.md   additive display, blend mode, dimming, colour, shaders, occlusion
    tracking.md            hands/pinch, planes, depth, what works and what doesn't
    device-and-deploy.md   wireless adb, install/launch/logcat, screen recordings
    editor-workflow.md     driving Unity from a terminal, offline video rendering with alpha
    troubleshooting.md     error message → cause → fix
  templates/               drop-in Unity package (namespace AuraXR)
  scripts/aura.sh          adb helper: connect, install, log, pull/clear recordings
```

## Use the templates without Claude

Copy `xreal-aura-unity/templates/` next to your Unity project, rename the package in
`package.json`, add `"com.yourcompany.auraxr": "file:../path/to/templates"` to
`Packages/manifest.json`, then run **Aura → 1. Set Up Project** and **Aura → 2. Add XR Rig**.
