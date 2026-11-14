using System;
using System.IO;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Sandbox;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class Stage1PreviewScene
    {
        public const string Path = "Assets/_Game/Scenes/Stage1ArenaPreview.unity";
        [MenuItem("Game/Stage 1/Create HUD preview")]
        public static void Create()
        {
            FoundationScenes.EnsureEditorReady();
            if (File.Exists(Path) || File.Exists(Path + ".meta"))
                throw new InvalidOperationException("Existing preview preserved: " + Path);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Player.prefab");
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/SandboxEnemy.prefab");
            var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/PrototypeSquare.png");
            if (playerPrefab == null || enemyPrefab == null || square == null || playerPrefab.GetComponent<PlayerCombatFacade>() == null
                || playerPrefab.GetComponent<Game.Combat.AutoAimSelector>() == null || enemyPrefab.GetComponent<SandboxEnemy>() == null)
                throw new InvalidOperationException("Import the complete S1-04 handoff first.");
            // Additive creation preserves open scenes. Inspect this new scene separately before Play.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("PreviewCamera", typeof(Camera));
            camera.transform.position = new Vector3(0, 0, -10);
            camera.GetComponent<Camera>().orthographic = true;
            camera.GetComponent<Camera>().orthographicSize = 8;
            camera.GetComponent<Camera>().backgroundColor = new Color(.1f, .13f, .12f);
            Wall("North", new Vector2(0, 7), new Vector2(22, .5f), square);
            Wall("South", new Vector2(0, -7), new Vector2(22, .5f), square);
            Wall("West", new Vector2(-11, 0), new Vector2(.5f, 14), square);
            Wall("East", new Vector2(11, 0), new Vector2(.5f, 14), square);
            var registry = new GameObject("TargetRegistry").AddComponent<TargetRegistry>();
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            hero.transform.position = new Vector3(0, -3, 0);
            var targets = new SandboxEnemy[3];
            for (int i = 0; i < 3; i++)
            {
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
                enemy.transform.position = new Vector3(-5 + i * 5, 4, 0);
                targets[i] = enemy.GetComponent<SandboxEnemy>();
            }
            var hud = new GameObject("HUD").AddComponent<HudPresenter>();
            var bindings = new GameObject("PreviewBindings").AddComponent<Stage1PreviewBindings>();
            bindings.Configure(hero.GetComponent<PlayerCombatFacade>(), targets, registry, hud);
            EditorUtility.SetDirty(bindings);
            if (!EditorSceneManager.SaveScene(scene, Path)) throw new IOException("Could not save preview.");
            Debug.Log("S1_03_PREVIEW_CREATED. Open Stage1ArenaPreview alone before Play. Existing scenes and Build Settings preserved.");
        }
        private static void Wall(string name, Vector2 position, Vector2 size, Sprite sprite)
        {
            var wall = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            wall.transform.position = position; wall.transform.localScale = new Vector3(size.x,size.y,1); wall.GetComponent<BoxCollider2D>().size=Vector2.one;
            wall.GetComponent<SpriteRenderer>().sprite = sprite;
            wall.GetComponent<SpriteRenderer>().color = new Color(.3f, .35f, .3f);
        }
    }
}
