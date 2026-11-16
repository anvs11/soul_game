#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Game.Combat;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Game.Sandbox
{
    // Opt-in instrumentation only. It does not run or certify gameplay cycles.
    public sealed class Stage2Measurements : MonoBehaviour
    {
        [Serializable] private struct Sample
        {
            public float elapsedSeconds, frameIntervalMs;
            public long gcAllocatedBytes;
            public int scene, activeProjectiles, createdProjectiles;
        }
        [Serializable] private sealed class Measurement
        {
            public string unity, platform, os, device, cpu, gpu, buildId, scenario;
            public bool developmentPlayer, gcCounterAvailable;
            public int width, height, errors, sampleCount;
            public float averageFrameIntervalMs, p50FrameIntervalMs, p95FrameIntervalMs;
            public string[] scenes;
            public Sample[] samples;
            public string observerOverhead = "FindObjectsByType pool refresh once/second; GC includes instrumentation. Frame interval includes frame pacing; not isolated CPU frame time.";
        }
        private readonly List<string> sceneNames = new List<string>();
        private readonly Sample[] samples = new Sample[18000];
        private ProjectilePool[] pools = Array.Empty<ProjectilePool>();
        private ProfilerRecorder gc;
        private bool gcAvailable, written;
        private string folder, buildId, scenario;
        private int count, errors, sceneIndex;
        private float start, duration = 60, refresh;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Launch()
        {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "--stage2-measure") < 0) return;
            var root = new GameObject("Stage 2 measurements"); DontDestroyOnLoad(root); root.AddComponent<Stage2Measurements>();
        }
        private static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        private void Start()
        {
            string output = Argument("--stage2-output");
            if (string.IsNullOrEmpty(output) || !Path.IsPathRooted(output))
            { Debug.LogError("Measurements need explicit absolute --stage2-output inside the project."); enabled = false; return; }
            folder = Path.Combine(output, "measure-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(folder);
            buildId = Argument("--stage2-build-id") ?? "unspecified"; scenario = Argument("--stage2-scenario") ?? "unspecified; label the actual actions separately";
            if (float.TryParse(Argument("--stage2-measure-seconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds)) duration = Mathf.Clamp(seconds, 5, 300);
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"); gcAvailable = gc.Valid;
            start = Time.realtimeSinceStartup; refresh = start; written = false;
            SceneManager.sceneLoaded += OnScene; Application.logMessageReceived += OnLog;
            OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        private void OnScene(Scene scene, LoadSceneMode mode)
        {
            sceneIndex = sceneNames.IndexOf(scene.name);
            if (sceneIndex < 0) { sceneIndex = sceneNames.Count; sceneNames.Add(scene.name); }
            refresh = 0;
        }
        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private void LateUpdate()
        {
            if (written || folder == null) return;
            float now = Time.realtimeSinceStartup;
            if (now >= refresh) { pools = FindObjectsByType<ProjectilePool>(FindObjectsSortMode.None); refresh = now + 1; }
            int active = 0, created = 0;
            foreach (var pool in pools) if (pool != null) { active += pool.ActiveCount; created += pool.TotalCount; }
            if (count < samples.Length) samples[count++] = new Sample { elapsedSeconds = now - start,
                frameIntervalMs = Time.unscaledDeltaTime * 1000, gcAllocatedBytes = gcAvailable ? gc.LastValue : -1,
                scene = sceneIndex, activeProjectiles = active, createdProjectiles = created };
            if (now - start >= duration || count == samples.Length) Write();
        }
        private void Write()
        {
            if (written || folder == null || count == 0) return;
            written = true;
            var data = new Sample[count]; Array.Copy(samples, data, count);
            var times = new float[count]; float sum = 0;
            for (int i = 0; i < count; i++) { times[i] = data[i].frameIntervalMs; sum += times[i]; }
            Array.Sort(times);
            var result = new Measurement { unity = Application.unityVersion, platform = Application.platform.ToString(),
                os = SystemInfo.operatingSystem, device = SystemInfo.deviceModel, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                buildId = buildId, scenario = scenario, developmentPlayer = Debug.isDebugBuild, gcCounterAvailable = gcAvailable,
                width = Screen.width, height = Screen.height, errors = errors, sampleCount = count, averageFrameIntervalMs = sum / count,
                p50FrameIntervalMs = times[(count - 1) / 2], p95FrameIntervalMs = times[Mathf.FloorToInt((count - 1) * .95f)],
                scenes = sceneNames.ToArray(), samples = data };
            File.WriteAllText(Path.Combine(folder, "measurements.json"), JsonUtility.ToJson(result, true));
            Debug.Log("STAGE2_MEASUREMENTS " + folder);
            gc.Dispose();
        }
        private void OnApplicationQuit() => Write();
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnScene; Application.logMessageReceived -= OnLog;
            Write(); gc.Dispose();
        }
    }
}
#endif
