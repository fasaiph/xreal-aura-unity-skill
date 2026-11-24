using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AuraXR.Editor
{
    /// <summary>
    /// Lets a terminal (or Claude Code) drive the open Editor: drop a file named
    /// &lt;command&gt;.trigger into the project's Temp folder and the matching action runs, with the
    /// file's text as its options. Results go to Temp/&lt;command&gt;.result so the caller can wait on it.
    ///
    ///   echo ""  > Temp/build.trigger     build the APK to build/&lt;product&gt;.apk
    ///   echo ""  > Temp/play.trigger      enter Play mode (stops it first if running)
    ///   echo ""  > Temp/stop.trigger      exit Play mode
    ///   echo ""  > Temp/refresh.trigger   AssetDatabase.Refresh (reimport edited files)
    ///
    /// The Editor only notices files (and recompiles scripts) while it has focus, so after writing
    /// a trigger, bring Unity to the front: `open -a "Unity"` (from macOS; screen must be unlocked).
    /// Add your own commands to s_Commands.
    /// </summary>
    [InitializeOnLoad]
    static class EditorTriggers
    {
        static readonly Dictionary<string, Func<string, string>> s_Commands = new()
        {
            ["build"] = Build,
            ["play"] = _ => { EditorApplication.isPlaying = true; return "playing"; },
            ["stop"] = _ => { EditorApplication.isPlaying = false; return "stopped"; },
            ["refresh"] = _ => { AssetDatabase.Refresh(); return "refreshed"; },
        };

        static double s_NextCheck;
        static string TempFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));

        static EditorTriggers() => EditorApplication.update += Watch;

        static void Watch()
        {
            if (EditorApplication.timeSinceStartup < s_NextCheck)
                return;
            s_NextCheck = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            foreach (var command in s_Commands)
            {
                string trigger = Path.Combine(TempFolder, command.Key + ".trigger");
                if (!File.Exists(trigger))
                    continue;
                string options = File.ReadAllText(trigger).Trim();
                File.Delete(trigger);
                string result;
                try { result = command.Value(options); }
                catch (Exception e) { result = "error: " + e.Message; Debug.LogException(e); }
                File.WriteAllText(Path.Combine(TempFolder, command.Key + ".result"), result + "\n");
                Debug.Log($"[Aura] {command.Key}: {result}");
            }
        }

        static string Build(string options)
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    scenes.Add(scene.path);
            }
            string apk = Path.GetFullPath(Path.Combine(Application.dataPath, "../build", PlayerSettings.productName.Replace(' ', '-') + ".apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(apk));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = apk,
                target = BuildTarget.Android,
                options = options.Contains("dev") ? BuildOptions.Development : BuildOptions.None,
            });
            return report.summary.result == BuildResult.Succeeded
                ? $"ok {apk} ({report.summary.totalTime.TotalSeconds:0}s)"
                : $"failed {report.summary.result} ({report.summary.totalErrors} errors, see Editor.log)";
        }
    }
}
