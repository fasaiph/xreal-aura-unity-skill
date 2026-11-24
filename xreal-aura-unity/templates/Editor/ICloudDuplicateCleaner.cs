using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AuraXR.Editor
{
    /// <summary>
    /// The project lives in an iCloud-synced folder, and iCloud makes numbered conflict copies of
    /// files Unity is writing in its caches (e.g. "nunit.framework 2.dll", "Data 2", "LastBuild 20.buildreport"). Those break
    /// builds and script loading. This deletes them from Library and Temp (never from Assets,
    /// Packages or ProjectSettings) when scripts load, before every build, and from the menu.
    /// </summary>
    [InitializeOnLoad]
    class ICloudDuplicateCleaner : IPreprocessBuildWithReport
    {
        static readonly string[] k_CacheFolders = { "Library", "Temp" };

        static ICloudDuplicateCleaner() => Clean(log: false);

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            int removed = Clean(log: true);
            // A stale Android build cache is what iCloud usually corrupts; rebuild it if anything was found.
            if (removed > 0)
            {
                TryDelete(Path.Combine("Library", "PlayerDataCache", "Android"));
                TryDelete(Path.Combine("Library", "Bee", "artifacts", "Android"));
            }
        }

        [MenuItem("Aura/Clear iCloud Duplicates", priority = 100)]
        static void CleanFromMenu()
        {
            int removed = Clean(log: true);
            EditorUtility.DisplayDialog("Aura",
                removed == 0 ? "No iCloud duplicates found." : $"Removed {removed} iCloud duplicate(s) from Unity's caches.\n\nIf scripts were affected, restart Unity.", "OK");
        }

        static int Clean(bool log)
        {
            int removed = 0;
            foreach (var folder in k_CacheFolders.Where(Directory.Exists))
            {
                var duplicates = Directory.EnumerateFileSystemEntries(folder, "* *", SearchOption.AllDirectories)
                    .Where(IsConflictCopy)
                    .OrderByDescending(path => path.Length) // children before parents
                    .ToList();
                foreach (var path in duplicates)
                {
                    if (TryDelete(path))
                        removed++;
                }
            }
            if (log && removed > 0)
                Debug.Log($"[Aura] Removed {removed} iCloud duplicate(s) from Library/Temp.");
            return removed;
        }

        // iCloud names conflict copies "name 2", "name 3.ext", "name 20.ext.map", and so on.
        static readonly Regex k_ConflictCopy = new(@" \d+(\.[A-Za-z0-9_]+)*$");

        static bool IsConflictCopy(string path) => k_ConflictCopy.IsMatch(Path.GetFileName(path));

        static bool TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                else if (File.Exists(path))
                    File.Delete(path);
                else
                    return false;
                return true;
            }
            catch (IOException)
            {
                return false; // in use; the next pass gets it
            }
            catch (System.UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
