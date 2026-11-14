using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Game.Editor
{
    public static class BuildCommands
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
