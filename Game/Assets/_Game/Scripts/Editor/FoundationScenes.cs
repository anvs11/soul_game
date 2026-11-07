using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Sandbox;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class FoundationScenes
    {
        public static readonly string[] SceneNames =
            { ScenePaths.Bootstrap, ScenePaths.Home, ScenePaths.CombatSandbox };

        [MenuItem("Game/Setup/Create foundation scenes")]
        public static void Create()
        {
            EnsureEditorReady();
            EnsureFolder("Assets/_Game/Scenes");
            // Validate every collision before creating the first new scene.
            foreach (var name in SceneNames)
            {
                string path = ScenePaths.Asset(name);
                if (File.Exists(path)) ValidateExisting(path, name);
                else if (File.Exists(path + ".meta"))
                    throw new InvalidOperationException("Orphan .meta preserved: " + path);
            }

            foreach (var name in SceneNames)
            {
                string path = ScenePaths.Asset(name);
                if (File.Exists(path)) continue;
                CreateScene(path, name);
            }

            var settings = new List<EditorBuildSettingsScene>();
            foreach (var name in SceneNames)
                settings.Add(new EditorBuildSettingsScene(ScenePaths.Asset(name), true));
            foreach (var entry in EditorBuildSettings.scenes)
            {
                bool foundation = Array.Exists(SceneNames, name => entry.path == ScenePaths.Asset(name));
                if (!foundation) settings.Add(new EditorBuildSettingsScene(entry.path, false));
            }
            EditorBuildSettings.scenes = settings.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("S1_FOUNDATION_SCENES_READY. Open Bootstrap and press Play. Existing scenes were preserved.");
        }

        public static void EnsureEditorReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play and wait for compilation before this command.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save your open scenes manually before this command.");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static void ValidateExisting(string path, string name)
        {
            var original = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int bootstraps = Count<GameBootstrap>(scene);
                int screens = Count<FlowScreen>(scene);
                bool valid = name == ScenePaths.Bootstrap
                    ? bootstraps == 1 && screens == 0 && Count<SceneFlow>(scene) == 1
                    : bootstraps == 0 && screens == 1 && Count<EventSystem>(scene) == 1
                      && Count<InputSystemUIInputModule>(scene) == 1 && Count<Camera>(scene) == 1
                      && (name == ScenePaths.Home ? Count<HomeScreen>(scene) == 1 : Count<EmptyArenaScreen>(scene) == 1);
                if (!valid)
                    throw new InvalidOperationException("Existing scene has different content; preserved: " + path);
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static int Count<T>(Scene scene) where T : Component
        {
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
                count += root.GetComponentsInChildren<T>(true).Length;
            return count;
        }

        private static void CreateScene(string path, string name)
        {
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                if (name == ScenePaths.Bootstrap)
                    new GameObject("GameServices").AddComponent<GameBootstrap>();
                else
                {
                    var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                    cameraObject.tag = "MainCamera";
                    cameraObject.transform.position = new Vector3(0, 0, -10);
                    var camera = cameraObject.GetComponent<Camera>();
                    camera.orthographic = true;
                    camera.orthographicSize = 5;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.055f, 0.08f, 0.095f);

                    var events = new GameObject("EventSystem", typeof(EventSystem));
                    events.AddComponent<InputSystemUIInputModule>();
                    var screen = new GameObject(name + "Screen");
                    if (name == ScenePaths.Home) screen.AddComponent<HomeScreen>();
                    else screen.AddComponent<EmptyArenaScreen>();
                }

                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new IOException("Could not save new scene: " + path);
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
