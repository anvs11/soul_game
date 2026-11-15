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
    public static class HazardBossAssets
    {
        private const string Folder = "Assets/_Game/Prefabs/Actors/Stage2/";
        public static void Create()
        {
            FoundationScenes.EnsureEditorReady();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<PoliceBossDefinition>(Folder + "PoliceBossDefinition.asset") == null)
            { AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PoliceBossDefinition>(), Folder + "PoliceBossDefinition.asset"); AssetDatabase.SaveAssets(); }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "ExplosiveBarrel.prefab") == null)
            {
                var go = new GameObject("ExplosiveBarrel"); go.SetActive(false);
                go.AddComponent<BoxCollider2D>().size = new Vector2(.7f, 1);
                go.AddComponent<DamageReceiver>().Configure(1, 0, 0);
                Sprite(go, "Barrel body", Vector2.zero, new Vector2(.7f, 1), new Color(.7f, .2f, .1f));
                go.AddComponent<ExplosiveBarrel>().Configure(3.5f, 50, 3, 2, LayerMask.GetMask("World"));
                go.SetActive(true); Save(go, "ExplosiveBarrel");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "ShieldMinion.prefab") == null)
            {
                var go = Actor("ShieldMinion", 30, 15, .4f);
                var skin = go.AddComponent<SpriteRenderer>(); skin.sprite = Square(); skin.color = new Color(.2f, .35f, .6f);
                var movement = go.AddComponent<SandboxEnemy>(); movement.enabled = false;
                var serialized = new SerializedObject(movement); serialized.FindProperty("speed").floatValue = 4; serialized.ApplyModifiedPropertiesWithoutUndo();
                go.AddComponent<ShieldMinion>();
                Sprite(go, "Shield", new Vector2(-.25f, 0), new Vector2(.55f, 1), Color.gray);
                // Stored inactive so Initialize runs before activation during summons.
                Save(go, "ShieldMinion");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PoliceBoss.prefab") == null)
            {
                var go = Actor("PoliceBoss", 600, 0, 2);
                var pool = go.AddComponent<ProjectilePool>();
                pool.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Combat/Projectile.prefab").GetComponent<Projectile>(), 128);
                var body = Sprite(go, "Police body", Vector2.zero, new Vector2(4, 5), new Color(.2f, .35f, .65f));
                Sprite(body, "Police cap", new Vector2(0, .48f), new Vector2(.9f, .1f), Color.black);
                Sprite(body, "Badge", new Vector2(-.15f, .2f), new Vector2(.08f, .08f), Color.yellow);
                Sprite(body, "Pistol L", new Vector2(-.48f, -.1f), new Vector2(.12f, .25f), Color.black);
                Sprite(body, "Pistol R", new Vector2(.48f, -.1f), new Vector2(.12f, .25f), Color.black);
                go.AddComponent<PoliceBoss>().Configure(AssetDatabase.LoadAssetAtPath<PoliceBossDefinition>(Folder + "PoliceBossDefinition.asset"), pool,
                    AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "ShieldMinion.prefab"), LayerMask.GetMask("World"));
                go.SetActive(true); Save(go, "PoliceBoss");
            }
            AssetDatabase.SaveAssets();
            var ownedBoss = PrefabUtility.LoadPrefabContents(Folder + "PoliceBoss.prefab");
            ownedBoss.transform.Find("Police body").localScale = new Vector3(3.2f, 4, 1);
            PrefabUtility.SaveAsPrefabAsset(ownedBoss, Folder + "PoliceBoss.prefab");
            PrefabUtility.UnloadPrefabContents(ownedBoss);
            Preview("BarrelPreview", false); Preview("PoliceBossPreview", true);
            Debug.Log("S205_S206_ASSETS_READY");
        }
        private static Sprite Square() => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/PrototypeSquare.png");
        private static GameObject Actor(string name, float hp, float armor, float radius)
        {
            var go = new GameObject(name); go.SetActive(false);
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().radius = radius;
            var identity = new SerializedObject(go.AddComponent<ActorIdentity>()); identity.FindProperty("faction").enumValueIndex = 2; identity.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<DamageReceiver>().Configure(hp, armor, 0); return go;
        }
        private static GameObject Sprite(GameObject parent, string name, Vector2 position, Vector2 scale, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform, false); go.transform.localPosition = position; go.transform.localScale = scale;
            var skin = go.AddComponent<SpriteRenderer>(); skin.sprite = Square(); skin.color = color; skin.sortingOrder = 1; return go;
        }
        private static void Save(GameObject go, string name)
        {
            if (PrefabUtility.SaveAsPrefabAsset(go, Folder + name + ".prefab") == null) throw new InvalidOperationException("Prefab save failed");
            UnityEngine.Object.DestroyImmediate(go);
        }
        private static void Preview(string name, bool boss)
        {
            string path = ScenePaths.Folder + name + ".unity";
            if (System.IO.File.Exists(path)) return;
            if (!AssetDatabase.CopyAsset(ScenePaths.Asset(ScenePaths.Stage2CombatSandbox), path)) throw new InvalidOperationException("Preview copy failed");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (component is IEnemyTarget && component != null) UnityEngine.Object.DestroyImmediate(component.gameObject);
            UnityEngine.Object.DestroyImmediate(UnityEngine.Object.FindFirstObjectByType<ArenaController>());
            if (boss)
            { UnityEngine.Object.DestroyImmediate(GameObject.Find("CoverA")); UnityEngine.Object.DestroyImmediate(GameObject.Find("CoverB")); }
            var hero = UnityEngine.Object.FindFirstObjectByType<PlayerCombatFacade>(); hero.transform.position = new Vector3(-8, -4, 0);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + (boss ? "PoliceBoss" : "ExplosiveBarrel") + ".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = boss ? new Vector3(3, 2, 0) : new Vector3(-6, -4, 0);
            var preview = new GameObject("Preview bindings").AddComponent<HazardBossPreview>();
            preview.Configure(hero, boss ? instance.GetComponent<PoliceBoss>() : null, boss ? null : instance.GetComponent<ExplosiveBarrel>(),
                UnityEngine.Object.FindFirstObjectByType<TargetRegistry>(), UnityEngine.Object.FindFirstObjectByType<HudPresenter>());
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Preview save failed");
        }
    }
}
