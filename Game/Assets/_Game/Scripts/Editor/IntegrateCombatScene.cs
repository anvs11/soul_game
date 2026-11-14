using System;
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
    public static class IntegrateCombatScene
    {
        public static void Apply()
        {
            FoundationScenes.EnsureEditorReady();
            var heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Player.prefab");
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/SandboxEnemy.prefab");
            var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/PrototypeSquare.png");
            if (heroPrefab == null || enemyPrefab == null || square == null) throw new InvalidOperationException("Missing handoff assets.");
            var scene = EditorSceneManager.OpenScene(ScenePaths.Asset(ScenePaths.CombatSandbox), OpenSceneMode.Single);
            EmptyArenaScreen old = null; int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<ArenaController>(true) != null) { Debug.Log("S105_ALREADY_INTEGRATED"); return; }
                foreach (var candidate in root.GetComponentsInChildren<EmptyArenaScreen>(true)) { old = candidate; count++; }
            }
            if (count != 1) throw new InvalidOperationException("Expected one legacy screen. Scene not saved.");
            var host = old.gameObject;
            UnityEngine.Object.DestroyImmediate(old); // Explicitly authorized replacement of test behaviour only.
            var arena = host.AddComponent<ArenaController>();
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            camera.orthographic = true; camera.orthographicSize = 8; camera.transform.position = new Vector3(0,0,-10);
            Wall("North", new Vector2(0,7),new Vector2(22,.5f),square);
            Wall("South", new Vector2(0,-7),new Vector2(22,.5f),square);
            Wall("West", new Vector2(-11,0),new Vector2(.5f,14),square);
            Wall("East", new Vector2(11,0),new Vector2(.5f,14),square);
            var registry = new GameObject("ArenaRegistry").AddComponent<TargetRegistry>();
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab, scene);
            hero.transform.position = new Vector3(0,-3,0);
            var enemies = new SandboxEnemy[3];
            for (int i=0;i<3;i++)
            {
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab,scene);
                enemy.transform.position = new Vector3(-5+i*5,4,0);
                enemies[i] = enemy.GetComponent<SandboxEnemy>();
            }
            var hud = new GameObject("ArenaHUD").AddComponent<HudPresenter>();
            var exit = new GameObject("Exit",typeof(SpriteRenderer));
            exit.transform.position = new Vector3(8,-4,0);
            exit.GetComponent<SpriteRenderer>().sprite = square; exit.GetComponent<SpriteRenderer>().color = Color.red;
            arena.Configure(hero.GetComponent<PlayerCombatFacade>(),enemies,registry,hud,exit.transform);
            EditorUtility.SetDirty(arena);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            Debug.Log("S105_INTEGRATED");
        }
        private static void Wall(string name, Vector2 position,Vector2 size,Sprite sprite)
        {
            var wall = new GameObject(name,typeof(SpriteRenderer),typeof(BoxCollider2D));
            wall.transform.position=position; wall.transform.localScale=new Vector3(size.x,size.y,1); wall.GetComponent<BoxCollider2D>().size=Vector2.one;
            wall.GetComponent<SpriteRenderer>().sprite=sprite; wall.GetComponent<SpriteRenderer>().color=Color.gray;
        }
    }
}
