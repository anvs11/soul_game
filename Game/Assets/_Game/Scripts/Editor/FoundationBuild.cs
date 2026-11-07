using System;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    public static class FoundationBuild
    {
        [MenuItem("Game/Build/Build S1 foundation Windows")]
        public static void BuildWindows()
        {
            FoundationScenes.EnsureEditorReady();
            var scenes = Array.ConvertAll<string, string>(FoundationScenes.SceneNames, ScenePaths.Asset);
            foreach (var scene in scenes)
                if (!File.Exists(scene))
                    throw new BuildFailedException("Create foundation scenes first: " + scene);

            string buildId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string relative = "Builds/Foundation/Windows/" + buildId;
            string project = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(project, relative);
            if (Directory.Exists(output)) throw new BuildFailedException("Build folder already exists.");
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(output, "Game.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });

            string summary = "result=" + report.summary.result + "\nerrors=" + report.summary.totalErrors +
                "\nwarnings=" + report.summary.totalWarnings + "\neditor=" + Application.unityVersion +
                "\nscenes=Bootstrap,Home,CombatSandbox\n";
            string summaryPath = Path.Combine(output, "foundation-build-summary.txt");
            using (var stream = new FileStream(summaryPath, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream)) writer.Write(summary);

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Foundation build failed: " + report.summary.result);
            Debug.Log("S1_FOUNDATION_BUILD_OK " + relative + "/Game.exe");
        }
    }
}
