using System;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.StreetMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class Stage4CombatPrefabSetup
    {
        private const string Folder = "Assets/_Game/Prefabs/Stage4Combat";
        private const string Catalog = "Assets/_Game/Resources/Stage4CombatAssets.asset";
        public static void Create()
        {
            var names = new[] { "SlimeGrenadier", "ShieldShotgun", "TeleportNurse", "MiniSlime", "AmmoPickup", "HealthPickup", "ArmorPickup" };
            foreach (string name in names) RequireNew(Folder + "/" + name + ".prefab");
            RequireNew(Catalog);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/PrototypeSquare.png");
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Combat/Projectile.prefab").GetComponent<Projectile>();
            if (sprite == null || projectile == null) throw new InvalidOperationException("Missing prototype assets.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "Stage4Combat");
            var asset = ScriptableObject.CreateInstance<Stage4CombatAssets>();
            asset.Configure(
                Actor<SlimeGrenadier>(names[0], 200, .60f, new Color(.38f, .82f, .14f), sprite, projectile),
                Actor<ShieldShotgun>(names[1], 300, .70f, new Color(.22f, .44f, .72f), sprite, projectile),
                Actor<TeleportNurse>(names[2], 120, .42f, new Color(.85f, .24f, .61f), sprite, projectile),
                Actor<MiniSlime>(names[3], 12, .25f, new Color(.58f, 1f, .2f), sprite, projectile),
                Pickup(names[4], PickupKind.Ammo, 6, "AMMO", new Color(.9f, .68f, .2f), sprite),
                Pickup(names[5], PickupKind.Health, 10, "+", new Color(.34f, .88f, .48f), sprite),
                Pickup(names[6], PickupKind.Armor, 5, "ARM", new Color(.34f, .7f, 1f), sprite));
            AssetDatabase.CreateAsset(asset, Catalog);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Game/Stage 4/Create combat preview")]
        public static void CreatePresentation()
        {
            const string marker = "Assets/_Game/Resources/Stage4AreaMarker.png";
            const string scene = "Assets/_Game/Scenes/Stage4CombatPreview.unity";
            RequireNew(marker);
            RequireNew(scene);
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float radius = new Vector2(x - 31.5f, y - 31.5f).magnitude;
                    texture.SetPixel(x, y, new Color(1, 1, 1, radius > 31.5f ? 0 : radius > 28 ? 1 : .6f));
                }
            texture.Apply();
            using (var file = new System.IO.FileStream(marker, System.IO.FileMode.CreateNew))
            {
                byte[] bytes = texture.EncodeToPNG();
                file.Write(bytes, 0, bytes.Length);
            }
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(marker, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(marker);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64;
            importer.SaveAndReimport();

            var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Player.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(template);
            player.transform.position = new Vector2(0, -4);
            var controller = new GameObject("Stage4CombatPreview");
            controller.AddComponent<TargetRegistry>();
            controller.AddComponent<Stage4CombatPreview>().Configure(player.GetComponent<PlayerCombatFacade>());
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 9;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .1f, .13f);
            var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/PrototypeSquare.png");
            Wall(new Vector2(-12, 0), new Vector2(1, 16), square);
            Wall(new Vector2(12, 0), new Vector2(1, 16), square);
            Wall(new Vector2(0, -8), new Vector2(25, 1), square);
            Wall(new Vector2(0, 8), new Vector2(25, 1), square);
            if (!EditorSceneManager.SaveScene(preview, scene)) throw new InvalidOperationException("Preview scene save failed.");
        }
        private static void Wall(Vector2 point, Vector2 size, Sprite sprite)
        {
            var root = new GameObject("World wall");
            root.transform.position = point;
            root.transform.localScale = size;
            root.layer = LayerMask.NameToLayer("World");
            root.AddComponent<BoxCollider2D>();
            var skin = root.AddComponent<SpriteRenderer>();
            skin.sprite = sprite;
            skin.color = Color.gray;
        }
        private static T Actor<T>(string name, int hp, float radius, Color color, Sprite sprite, Projectile projectile) where T : EliteEnemy
        {
            var root = new GameObject(name);
            root.SetActive(false);
            try
            {
                var visual = root.AddComponent<SpriteRenderer>();
                visual.sprite = sprite;
                visual.color = color;
                visual.sortingOrder = 2;
                var identity = new SerializedObject(root.AddComponent<ActorIdentity>());
                identity.FindProperty("faction").intValue = (int)CombatFaction.Enemy;
                identity.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<DamageReceiver>().Configure(hp, 0, .12f);
                root.AddComponent<CircleCollider2D>().radius = radius;
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                root.AddComponent<T>();
                if (typeof(T) == typeof(MiniSlime))
                    root.GetComponent<ProjectilePool>().Configure(projectile, 3);
                // Templates can be initialized safely under an inactive room parent.
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab").GetComponent<T>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static RoomPickup Pickup(string name, PickupKind kind, int amount, string label, Color color, Sprite sprite)
        {
            var root = new GameObject(name);
            try
            {
                var visual = root.AddComponent<SpriteRenderer>();
                visual.sprite = sprite;
                visual.color = color;
                visual.sortingOrder = 2;
                var text = new GameObject("Symbol").AddComponent<TextMesh>();
                text.transform.SetParent(root.transform, false);
                text.text = label;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = new Color(.07f, .09f, .12f);
                text.fontSize = 48;
                text.characterSize = .12f;
                text.GetComponent<MeshRenderer>().sortingOrder = 3;
                var drop = root.AddComponent<RoomPickup>();
                drop.Configure(kind, amount);
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab").GetComponent<RoomPickup>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void RequireNew(string path)
        {
            if (System.IO.File.Exists(path) || System.IO.File.Exists(path + ".meta"))
                throw new InvalidOperationException("Existing asset preserved: " + path);
        }
    }
}
