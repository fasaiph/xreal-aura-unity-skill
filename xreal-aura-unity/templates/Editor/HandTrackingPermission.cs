#if UNITY_ANDROID
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace AuraXR.Editor
{
    /// <summary>
    /// Declares the permissions AuraPermissions asks for at runtime (hand tracking; coarse scene
    /// understanding for planes and light estimation; fine for depth occlusion) in the generated
    /// Gradle manifest, if missing.
    /// </summary>
    class HandTrackingPermission : IPostGenerateGradleAndroidProject
    {
        static readonly string[] k_Permissions = { "android.permission.HAND_TRACKING", "android.permission.SCENE_UNDERSTANDING_COARSE", "android.permission.SCENE_UNDERSTANDING_FINE" };
        const string k_AndroidNamespace = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
                return;

            var manifest = new XmlDocument();
            manifest.Load(manifestPath);
            var declared = new HashSet<string>();
            foreach (XmlElement existing in manifest.GetElementsByTagName("uses-permission"))
                declared.Add(existing.GetAttribute("name", k_AndroidNamespace));

            foreach (var permissionName in k_Permissions)
            {
                if (declared.Contains(permissionName))
                    continue;
                var permission = manifest.CreateElement("uses-permission");
                var name = manifest.CreateAttribute("android", "name", k_AndroidNamespace);
                name.Value = permissionName;
                permission.Attributes.Append(name);
                manifest.DocumentElement.AppendChild(permission);
            }
            manifest.Save(manifestPath);
        }
    }
}
#endif
