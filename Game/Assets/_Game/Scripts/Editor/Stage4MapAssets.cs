using System;
using System.Collections.Generic;
using System.IO;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.StreetMap;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class Stage4MapAssets
    {
        public const string GeometryPath = "Assets/_Game/Prefabs/Rooms/Stage4/FixedStreetGeometry.prefab";
        public const string ScenePath = "Assets/_Game/Scenes/Stage4StreetMap.unity";

        // Read-only scene preview for batch QA; never saves or replaces scene/prefab assets.
        public static void CapturePreviews()
        {
            FoundationScenes.EnsureEditorReady();
            string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                "../TestResults/s402-preview-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            Directory.CreateDirectory(directory);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<Camera>() != null) camera = root.GetComponent<Camera>();
            if (camera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Preview requires a graphics device and the map camera.");
            camera.clearFlags = CameraClearFlags.SolidColor;
            RenderPreview(camera, new Vector2(5.4f, 0), 7.5f, System.IO.Path.Combine(directory, "entry.png"));
            RenderPreview(camera, StreetMapLayout.Rooms[4].Center, 9, System.IO.Path.Combine(directory, "elite.png"));
            RenderPreview(camera, StreetMapLayout.CameraBounds.center, 32, System.IO.Path.Combine(directory, "map.png"));
            Debug.Log("S4_02_PREVIEWS " + directory);
        }

        private static void RenderPreview(Camera camera, Vector2 center, float halfHeight, string path)
        {
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.transform.position = new Vector3(center.x, center.y, -10);
                camera.orthographicSize = halfHeight;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                byte[] bytes = texture.EncodeToPNG();
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) file.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [MenuItem("Game/Stage 4/Create S4-02 fixed map (new assets only)")]
        public static void Create()
        {
            FoundationScenes.EnsureEditorReady();
            foreach (var path in new[] { GeometryPath, ScenePath })
                if (File.Exists(path) || File.Exists(path + ".meta"))
                    throw new InvalidOperationException("Existing Stage 4 asset preserved: " + path);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Player.prefab");
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/SandboxEnemy.prefab");
            var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/PrototypeSquare.png");
            if (playerPrefab == null || enemyPrefab == null || square == null || LayerMask.NameToLayer("World") < 0)
                throw new InvalidOperationException("Stage 1 prefabs/sprite and World layer are required.");
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Prefabs/Rooms/Stage4"))
                AssetDatabase.CreateFolder("Assets/_Game/Prefabs/Rooms", "Stage4");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var geometry = new GameObject("FixedStreetGeometry_v1");
            BuildGeometry(geometry.transform, square);
            if (PrefabUtility.SaveAsPrefabAssetAndConnect(geometry, GeometryPath, InteractionMode.AutomatedAction) == null)
                throw new IOException("Could not save new geometry prefab.");
            var heroObject = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            heroObject.transform.position = StreetMapLayout.Entry;
            var hero = heroObject.GetComponent<PlayerCombatFacade>();
            var camera = new GameObject("MapCamera", typeof(Camera), typeof(BoundedFollowCamera));
            camera.tag = "MainCamera";
            var view = camera.GetComponent<Camera>();
            view.orthographic = true; view.orthographicSize = 7.5f;
            view.backgroundColor = new Color(.035f, .045f, .05f);
            camera.transform.position = new Vector3(5.4f, 0, -10);
            camera.GetComponent<BoundedFollowCamera>().Configure(hero.transform, StreetMapLayout.CameraBounds);
            var registry = new GameObject("TargetRegistry").AddComponent<TargetRegistry>();
            var hud = new GameObject("HUD").AddComponent<HudPresenter>();
            var enemies = new List<SandboxEnemy>();
            for (int index = 1; index <= 3; index++)
                foreach (var point in StreetMapLayout.Rooms[index].SpawnPoints)
                {
                    var enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
                    enemy.name = StreetMapLayout.Rooms[index].Id + "_fixed_" + enemies.Count;
                    enemy.transform.position = point;
                    enemy.SetActive(false);
                    enemies.Add(enemy.GetComponent<SandboxEnemy>());
                }
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<EventSystem>().sendNavigationEvents = false;
            var screen = new GameObject("FixedStreet1_1").AddComponent<StreetMapScreen>();
            screen.Configure(hero, enemies.ToArray(), registry, hud);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save new map scene.");
            var settings = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!settings.Exists(s => s.path == ScenePath)) settings.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = settings.ToArray();
            AssetDatabase.SaveAssets();
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene, true); // Only the newly created saved scene.
            Debug.Log("S4_02_MAP_CREATED geometry v1; 5 rooms; 4 corridors; 6 fixed melee; elite NOT READY.");
        }

        private static void BuildGeometry(Transform root, Sprite square)
        {
            Color[] floors = { new Color(.11f, .22f, .2f), new Color(.16f, .18f, .2f), new Color(.19f, .19f, .15f), new Color(.17f, .15f, .2f), new Color(.23f, .15f, .15f) };
            for (int index = 0; index < StreetMapLayout.Rooms.Count; index++)
            {
                var room = StreetMapLayout.Rooms[index];
                var r = room.Bounds;
                Visual(root, room.Id + "_floor", r.center, r.size, floors[index], square, -10);
                Label(root, room.Id, r.center + Vector2.up * (r.height / 2f - 2), index == 4 ? "ELITE / NOT READY" : room.Id.ToUpperInvariant());
                if (index == 4)
                {
                    Label(root, "EliteWarning", r.center, "S4-02: GEOMETRY ONLY");
                    foreach (var point in room.SpawnPoints)
                        Visual(root, "Placeholder_NOT_AN_ENEMY", point, Vector2.one * 1.5f, new Color(.9f, .5f, .2f), square, -2);
                }
            }
            int corridorIndex = 0;
            foreach (var corridor in StreetMapLayout.Corridors)
            {
                Visual(root, "Corridor_" + corridorIndex++, corridor.center, corridor.size, new Color(.22f, .25f, .25f), square, -10);
                bool horizontal = corridor.width > corridor.height;
                Visual(root, "PassageStripe", corridor.center, horizontal ? new Vector2(corridor.width, .2f) : new Vector2(.2f, corridor.height), new Color(.72f, .61f, .27f), square, -8);
                foreach (var end in new[] { -1, 1 })
                {
                    Vector2 offset = horizontal ? new Vector2(end * corridor.width / 2f, 0) : new Vector2(0, end * corridor.height / 2f);
                    Visual(root, "Threshold", corridor.center + offset, horizontal ? new Vector2(.3f, 4) : new Vector2(4, .3f), new Color(.3f, .55f, .52f), square, -7);
                }
            }
            // Outside cells including corners form a one-unit physical envelope.
            var walls = new HashSet<Vector2Int>();
            foreach (var cell in StreetMapLayout.WalkableCells())
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        var neighbor = cell + new Vector2Int(x, y);
                        if (!StreetMapLayout.IsWalkable(neighbor)) walls.Add(neighbor);
                    }
            var ordered = new List<Vector2Int>(walls);
            ordered.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            foreach (var cell in ordered)
            {
                var wall = Visual(root, "Wall_" + cell.x + "_" + cell.y, (Vector2)cell + Vector2.one * .5f, Vector2.one, new Color(.36f, .4f, .42f), square, 0);
                wall.layer = LayerMask.NameToLayer("World");
                wall.AddComponent<BoxCollider2D>();
            }
            Visual(root, "SafeEntry", StreetMapLayout.Entry, new Vector2(2, 2), new Color(.15f, .5f, .4f), square, -8);
        }
        private static GameObject Visual(Transform parent, string name, Vector2 position, Vector2 size, Color color, Sprite sprite, int order)
        {
            var obj = new GameObject(name, typeof(SpriteRenderer));
            obj.transform.SetParent(parent, false); obj.transform.position = position; obj.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = obj.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
            return obj;
        }
        private static void Label(Transform parent, string name, Vector2 position, string text)
        {
            var label = new GameObject(name + "_label", typeof(TextMesh));
            label.transform.SetParent(parent, false); label.transform.position = position;
            var mesh = label.GetComponent<TextMesh>(); mesh.text = text; mesh.anchor = TextAnchor.MiddleCenter;
            mesh.fontSize = 32; mesh.characterSize = .14f; mesh.color = new Color(.75f, .8f, .78f);
            label.GetComponent<MeshRenderer>().sortingOrder = -6;
        }
    }
}
