using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Game.Editor
{
    public static partial class BuildCommands
    {
        [Serializable] private sealed class Manifest
        {
            public string buildId, platform, result, editor, executable;
            public string[] scenes;
            public int errors, warnings;
            public bool androidSupported;
        }
        [MenuItem("Game/Build/Stage 1 Windows")]
        public static void BuildStage1Windows() => Build(BuildTarget.StandaloneWindows64);
        [MenuItem("Game/Build/Stage 1 Android")]
        public static void BuildStage1Android() => Build(BuildTarget.Android);
        // Return a new list: callers/tests cannot mutate the build contract.
        public static string[] Stage2ScenePaths() => new[]
        {
            "Assets/_Game/Scenes/Bootstrap.unity",
            "Assets/_Game/Scenes/Home.unity",
            "Assets/_Game/Scenes/Stage2CombatSandbox.unity",
            "Assets/_Game/Scenes/Stage2BossSandbox.unity"
        };
        [MenuItem("Game/Build/Stage 2 Mac")]
        public static void BuildStage2Mac() => BuildStage2(BuildTarget.StandaloneOSX, "Mac", "ZombieExpedition.app");
        [MenuItem("Game/Build/Stage 2 Windows")]
        public static void BuildStage2Windows() => BuildStage2(BuildTarget.StandaloneWindows64, "Windows", "ZombieExpedition.exe");
        [MenuItem("Game/Build/Stage 2 Android")]
        public static void BuildStage2Android() => BuildStage2(BuildTarget.Android, "Android", "ZombieExpedition.apk");
        [Serializable] private sealed class Stage2Manifest
        {
            public string buildId, platform, result, editor, executable;
            public string[] scenes, messages;
            public int errors, warnings;
            public double durationSeconds;
            public ulong sizeBytes;
        }
        private static void BuildStage2(BuildTarget target, string platform, string executableName)
        {
            string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Stage2", platform, id);
            if (Directory.Exists(folder)) throw new BuildFailedException("Build directory already exists.");
            Directory.CreateDirectory(folder);
            string manifestPath = Path.Combine(folder, "build-report.json");
            var manifest = new Stage2Manifest { buildId = id, platform = platform, result = "NotStarted",
                editor = Application.unityVersion, executable = executableName, scenes = Stage2ScenePaths(), messages = Array.Empty<string>() };
            try
            {
                FoundationScenes.EnsureEditorReady();
                if (Application.unityVersion != "6000.3.28f1") throw new BuildFailedException("Use pinned Unity 6000.3.28f1.");
                if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                    throw new BuildFailedException("Build module unavailable; no module was installed: " + target);
                if (EditorUserBuildSettings.activeBuildTarget != target)
                    throw new BuildFailedException("Select target first or launch Unity with -buildTarget " + target);
                foreach (var scene in manifest.scenes)
                    if (!File.Exists(scene)) throw new BuildFailedException("Missing runtime scene: " + scene);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = manifest.scenes,
                    locationPathName = Path.Combine(folder, executableName), target = target, options = BuildOptions.Development });
                manifest.result = report.summary.result.ToString(); manifest.errors = report.summary.totalErrors;
                manifest.warnings = report.summary.totalWarnings; manifest.durationSeconds = report.summary.totalTime.TotalSeconds;
                manifest.sizeBytes = report.summary.totalSize;
                var messages = new System.Collections.Generic.List<string>();
                foreach (var step in report.steps) foreach (var message in step.messages)
                    if (message.type == LogType.Warning || message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Assert)
                        messages.Add(step.name + ": " + message.type + ": " + message.content);
                manifest.messages = messages.ToArray();
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
                if (report.summary.result != BuildResult.Succeeded || manifest.errors != 0)
                    throw new BuildFailedException("Stage 2 BuildReport failed: " + manifestPath);
                Debug.Log("STAGE2_BUILD_OK " + manifestPath);
            }
            catch (Exception exception)
            {
                if (manifest.result == "NotStarted") { manifest.result = "PreflightFailed"; manifest.errors = 1; manifest.messages = new[] { exception.ToString() }; }
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
                throw;
            }
        }
        private static void Build(BuildTarget target)
        {
            FoundationScenes.EnsureEditorReady();
            var group=BuildPipeline.GetBuildTargetGroup(target);
            if(!BuildPipeline.IsBuildTargetSupported(group,target))throw new BuildFailedException("Build module unavailable: "+target);
            if(EditorUserBuildSettings.activeBuildTarget!=target)throw new BuildFailedException("Start Unity with -buildTarget "+target);
            string[] scenes={"Assets/_Game/Scenes/Bootstrap.unity","Assets/_Game/Scenes/Home.unity","Assets/_Game/Scenes/CombatSandbox.unity"};
            foreach(var scene in scenes)if(!File.Exists(scene))throw new BuildFailedException("Missing scene: "+scene);
            string platform=target==BuildTarget.Android?"Android":"Windows";
            string id=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Builds","Stage1",platform,id);
            if(Directory.Exists(folder))throw new BuildFailedException("Build directory already exists");
            Directory.CreateDirectory(folder);
            string executable=Path.Combine(folder,target==BuildTarget.Android?"ZombieExpedition.apk":"ZombieExpedition.exe");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=executable,target=target,options=BuildOptions.Development});
            var manifest=new Manifest{buildId=id,platform=platform,result=report.summary.result.ToString(),editor=Application.unityVersion,
                executable=Path.GetFileName(executable),scenes=scenes,errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,
                androidSupported=BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android)};
            File.WriteAllText(Path.Combine(folder,"build-report.json"),JsonUtility.ToJson(manifest,true));
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Stage 1 build failed: "+manifest.result);
            Debug.Log("STAGE1_BUILD_OK "+folder);
        }
    }
}
