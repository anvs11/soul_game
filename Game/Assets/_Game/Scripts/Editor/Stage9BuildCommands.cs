using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static partial class BuildCommands
    {
        [MenuItem("Game/Build/Stage 9 Windows")]
        public static void BuildStage9Windows() =>
            BuildStreet(9, Stage4ScenePaths(), BuildTarget.StandaloneWindows64, "Windows", "ZombieExpedition.exe");

        [MenuItem("Game/Build/Stage 9 Mac")]
        public static void BuildStage9Mac() =>
            BuildStreet(9, Stage4ScenePaths(), BuildTarget.StandaloneOSX, "Mac", "ZombieExpedition.app");

        public static void ReportStage9Modules()
        {
            Debug.Log($"S9_MODULES windows={BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64)} " +
                $"mac={BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX)} " +
                $"android={BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)}");
        }
    }
}
