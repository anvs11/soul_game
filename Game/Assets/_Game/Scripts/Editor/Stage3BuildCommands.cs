using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Game.Editor
{
    public static partial class BuildCommands
    {
        // Include legacy profiles because Home retains its Stage1 primary action.
        public static string[] Stage3ScenePaths() => new[] {
            "Assets/_Game/Scenes/Bootstrap.unity", "Assets/_Game/Scenes/Home.unity",
            "Assets/_Game/Scenes/CombatSandbox.unity", "Assets/_Game/Scenes/Stage2CombatSandbox.unity",
            "Assets/_Game/Scenes/Stage2BossSandbox.unity", "Assets/_Game/Scenes/Stage3StreetSandbox.unity" };
        [MenuItem("Game/Build/Stage 3 Windows")]
        public static void BuildStage3Windows() => BuildStreet(3, Stage3ScenePaths(), BuildTarget.StandaloneWindows64, "Windows", "ZombieExpedition.exe");
        [MenuItem("Game/Build/Stage 3 Android")]
        public static void BuildStage3Android() => BuildStreet(3, Stage3ScenePaths(), BuildTarget.Android, "Android", "ZombieExpedition.apk");
        [MenuItem("Game/Build/Stage 3 Mac")]
        public static void BuildStage3Mac() => BuildStreet(3, Stage3ScenePaths(), BuildTarget.StandaloneOSX, "Mac", "ZombieExpedition.app");
        public static string[] Stage4ScenePaths() => Stage3ScenePaths().Append(Stage4MapAssets.ScenePath).ToArray();
        [MenuItem("Game/Build/Stage 4 Windows")]
        public static void BuildStage4Windows() => BuildStreet(4, Stage4ScenePaths(), BuildTarget.StandaloneWindows64, "Windows", "ZombieExpedition.exe");
        [MenuItem("Game/Build/Stage 4 Android")]
        public static void BuildStage4Android() => BuildStreet(4, Stage4ScenePaths(), BuildTarget.Android, "Android", "ZombieExpedition.apk");
        [Serializable] private sealed class StreetBuildManifest
        {
            public string buildId, platform, result, editor, executable, buildGuid;
            public string[] scenes, packedScenes, messages;
            public int errors, warnings;
            public double durationSeconds;
            public ulong sizeBytes;
        }
        private static void BuildStreet(int stage, string[] scenes, BuildTarget target, string platform, string executable)
        {
            string id = $"s{stage}-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0,8);
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Builds",$"Stage{stage}",platform,id);
            if (Directory.Exists(folder)) throw new BuildFailedException("Build directory collision.");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder,"build-report.json");
            var data = new StreetBuildManifest { buildId=id, platform=platform, result="Preflight", editor=Application.unityVersion,
                executable=executable, scenes=scenes, packedScenes=Array.Empty<string>(), messages=Array.Empty<string>() };
            try
            {
                FoundationScenes.EnsureEditorReady();
                if (Application.unityVersion!="6000.3.28f1") throw new BuildFailedException("Pinned Unity required.");
                if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target),target))
                    throw new BuildFailedException("Build module unavailable: " + target);
                if (EditorUserBuildSettings.activeBuildTarget!=target) throw new BuildFailedException("Launch with -buildTarget " + target);
                foreach(var scene in data.scenes) if(!File.Exists(scene)) throw new BuildFailedException("Missing scene " + scene);
                data.result="BuildStarted";
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=data.scenes, target=target,
                    locationPathName=Path.Combine(folder,executable), options=BuildOptions.Development | BuildOptions.DetailedBuildReport });
                data.result=report.summary.result.ToString(); data.errors=report.summary.totalErrors; data.warnings=report.summary.totalWarnings;
                data.sizeBytes=report.summary.totalSize; data.durationSeconds=report.summary.totalTime.TotalSeconds;
                data.buildGuid=report.summary.guid.ToString();
                data.packedScenes=report.packedAssets.SelectMany(p=>p.contents).Select(c=>c.sourceAssetPath)
                    .Where(p=>p.EndsWith(".unity",StringComparison.Ordinal)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
                data.messages=report.steps.SelectMany(s=>s.messages.Where(m=>m.type==LogType.Warning || m.type==LogType.Error || m.type==LogType.Exception || m.type==LogType.Assert)
                    .Select(m=>s.name+": "+m.type+": "+m.content)).ToArray();
                File.WriteAllText(path,JsonUtility.ToJson(data,true));
                if(report.summary.result!=BuildResult.Succeeded || data.errors!=0) throw new BuildFailedException("BuildReport not successful: "+path);
                if(!File.Exists(Path.Combine(folder,executable)) && !Directory.Exists(Path.Combine(folder,executable))) throw new BuildFailedException("Player output missing.");
                Debug.Log($"STAGE{stage}_BUILD_OK " + path);
            }
            catch(Exception e)
            {
                if(data.result=="Preflight" || data.result=="BuildStarted") data.result += "Failed";
                data.messages=(data.messages ?? Array.Empty<string>()).Concat(new[]{e.ToString()}).ToArray();
                File.WriteAllText(path,JsonUtility.ToJson(data,true)); throw;
            }
        }
    }
}
