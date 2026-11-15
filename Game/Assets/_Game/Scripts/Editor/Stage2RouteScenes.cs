using System;
using System.Collections.Generic;
using System.IO;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Sandbox;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Game.Editor
{
    public static class Stage2RouteScenes
    {
        [MenuItem("Game/Setup/Integrate Stage 2 route")]
        public static void Apply()
        {
            FoundationScenes.EnsureEditorReady();
            var arena = EditorSceneManager.OpenScene(ScenePaths.Asset(ScenePaths.Stage2CombatSandbox), OpenSceneMode.Single);
            if (UnityEngine.Object.FindFirstObjectByType<ExplosiveBarrel>() == null)
            {
                var barrel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Stage2/ExplosiveBarrel.prefab"), arena);
                barrel.name = "ArenaBarrel"; barrel.transform.position = new Vector3(-5, -6, 0);
                EditorSceneManager.MarkSceneDirty(arena);
                if (!EditorSceneManager.SaveScene(arena)) throw new InvalidOperationException("Arena save failed");
            }
            string bossPath = ScenePaths.Asset(ScenePaths.Stage2BossSandbox);
            if (!File.Exists(bossPath))
            {
                if (File.Exists(bossPath + ".meta")) throw new InvalidOperationException("Orphan meta preserved");
                if (!AssetDatabase.CopyAsset(ScenePaths.Folder + "PoliceBossPreview.unity", bossPath)) throw new InvalidOperationException("Boss scene copy failed");
                var scene = EditorSceneManager.OpenScene(bossPath, OpenSceneMode.Single);
                var preview = UnityEngine.Object.FindFirstObjectByType<HazardBossPreview>();
                UnityEngine.Object.DestroyImmediate(preview); // Only the diagnostic binding in the new copy.
                var controller = new GameObject("Stage2BossArena").AddComponent<ArenaController>();
                controller.ConfigureMixed(UnityEngine.Object.FindFirstObjectByType<PlayerCombatFacade>(),
                    new MonoBehaviour[] { UnityEngine.Object.FindFirstObjectByType<PoliceBoss>() },
                    UnityEngine.Object.FindFirstObjectByType<TargetRegistry>(), UnityEngine.Object.FindFirstObjectByType<HudPresenter>(), GameObject.Find("Exit").transform);
                GameObject.Find("Exit").GetComponent<SpriteRenderer>().enabled = false;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Boss scene save failed");
            }
            var settings = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!settings.Exists(s => s.path == bossPath)) settings.Add(new EditorBuildSettingsScene(bossPath, true));
            EditorBuildSettings.scenes = settings.ToArray();
            AssetDatabase.SaveAssets(); Debug.Log("S207_ROUTE_SCENES_READY");
        }
    }
}
