using System;
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
    public static class JumpingEnemyAssets
    {
        public const string Prefab="Assets/_Game/Prefabs/Actors/Stage2/JumpingEnemy.prefab";
        public const string Definition="Assets/_Game/Prefabs/Actors/Stage2/JumpingEnemyDefinition.asset";
        public const string Preview="Assets/_Game/Scenes/JumpingEnemyPreview.unity";
        public static void Create()
        {
            FoundationScenes.EnsureEditorReady();
            foreach(var path in new[]{Prefab,Preview})if(File.Exists(path)||File.Exists(path+".meta"))throw new InvalidOperationException("Existing resource preserved: "+path);
            var definition=AssetDatabase.LoadAssetAtPath<JumpingEnemyDefinition>(Definition);
            if(definition==null){definition=ScriptableObject.CreateInstance<JumpingEnemyDefinition>();definition.Validate();AssetDatabase.CreateAsset(definition,Definition);AssetDatabase.SaveAssets();}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=new GameObject("JumpingEnemy");go.SetActive(false);
            var body=go.AddComponent<Rigidbody2D>();body.gravityScale=0;body.freezeRotation=true;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().radius=.4f;
            var identity=go.AddComponent<ActorIdentity>();var serialized=new SerializedObject(identity);serialized.FindProperty("faction").enumValueIndex=(int)CombatFaction.Enemy;serialized.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<DamageReceiver>().Configure(definition.Health,0,0);
            go.AddComponent<EnemyPerception>().Configure(8,11,2,LayerMask.GetMask("World"));
            var skin=go.AddComponent<SpriteRenderer>();skin.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/PrototypeSquare.png");skin.color=new Color(.8f,.2f,.9f);
            go.AddComponent<JumpingEnemy>().Configure(AssetDatabase.LoadAssetAtPath<JumpingEnemyDefinition>(Definition),LayerMask.GetMask("World"),~0);
            go.SetActive(true);if(PrefabUtility.SaveAsPrefabAsset(go,Prefab)==null)throw new InvalidOperationException("Prefab save failed");
            UnityEngine.Object.DestroyImmediate(go);
            if(!AssetDatabase.CopyAsset(ScenePaths.Asset(ScenePaths.Stage2CombatSandbox),Preview))throw new InvalidOperationException("Preview copy failed");
            scene=EditorSceneManager.OpenScene(Preview,OpenSceneMode.Single);
            foreach(var old in UnityEngine.Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(old.gameObject);
            UnityEngine.Object.DestroyImmediate(UnityEngine.Object.FindFirstObjectByType<ArenaController>());
            var enemy=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);enemy.transform.position=new Vector3(-7,-5,0);
            var host=new GameObject("Jump preview bindings").AddComponent<JumpingEnemyPreview>();
            host.Configure(UnityEngine.Object.FindFirstObjectByType<PlayerCombatFacade>(),enemy.GetComponent<JumpingEnemy>(),UnityEngine.Object.FindFirstObjectByType<TargetRegistry>(),UnityEngine.Object.FindFirstObjectByType<HudPresenter>());
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Preview save failed");
            AssetDatabase.SaveAssets();Debug.Log("S203A_ASSETS_CREATED. Preview excluded from build settings.");
        }
    }
}
