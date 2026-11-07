using System.Text;
using Game.Contracts;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class BaselineReport
    {
        [MenuItem("Game/Setup/Apply serialization settings")]
        public static void ApplySerializationSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            AssetDatabase.SaveAssets();
            Debug.Log("Force Text and Visible Meta Files applied. No forced reserialization performed.");
        }

        [MenuItem("Game/Setup/Print baseline")]
        public static void Print()
        {
            var report = new StringBuilder();
            report.AppendLine("S1-01 environment report (not build acceptance)");
            report.AppendLine("Editor: " + Application.unityVersion);
            report.AppendLine("Serialization: " + EditorSettings.serializationMode);
            report.AppendLine("Meta: " + VersionControlSettings.mode);
            report.AppendLine("Runtime: " + typeof(IPlayerCombatSource).Assembly.GetName().Name);
            AppendPackage(report, PackageInfo.FindForAssembly(typeof(InputAction).Assembly));
            AppendPackage(report, PackageInfo.FindForAssembly(typeof(Graphic).Assembly));

            foreach (var package in PackageInfo.GetAllRegisteredPackages())
            {
                if (package.name == "com.unity.test-framework")
                    AppendPackage(report, package);
            }

            report.AppendLine("Input System enabled: " + IsInputSystemEnabled());
            Debug.Log(report.ToString());
        }

        private static void AppendPackage(StringBuilder report, PackageInfo package)
        {
            if (package == null)
            {
                report.AppendLine("Package lookup failed; inspect Package Manager.");
                return;
            }

            report.AppendLine(package.name + ": " + package.version);
        }

        private static bool IsInputSystemEnabled()
        {
#if ENABLE_INPUT_SYSTEM
            return true;
#else
            return false;
#endif
        }
    }
}
