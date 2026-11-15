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
    public static class Stage2ArenaScene
    {
        public const string VariantPath = "Assets/_Game/Prefabs/Actors/Stage2/SensingZombie.prefab";
        [MenuItem("Game/Setup/Create S2-02 diagnostic arena")]
        public static void Create()
        {
            FoundationScenes.EnsureEditorReady();
            string path = ScenePaths.Asset(ScenePaths.Stage2CombatSandbox);
            if (File.Exists(path) || File.Exists(VariantPath))
                throw new InvalidOperationException("S2 resources already exist; preserved. Inspect them instead of recreating.");
            var layerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = layerSettings.FindProperty("layers");
            int world = LayerMask.NameToLayer("World");
            if (world < 0)
            {
                for (int index=8;index<32;index++)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue)) { world=index;break; }
                if (world < 0) throw new InvalidOperationException("No free user layer for World.");
                layers.GetArrayElementAtIndex(world).stringValue="World";
                layerSettings.ApplyModifiedProperties();
            }
            if(!AssetDatabase.IsValidFolder("Assets/_Game/Prefabs/Actors/Stage2"))
                AssetDatabase.CreateFolder("Assets/_Game/Prefabs/Actors","Stage2");
            var basePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/SandboxEnemy.prefab");
            var square=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/PrototypeSquare.png");
            if(basePrefab==null || square==null)throw new InvalidOperationException("Missing Stage 1 resources.");
            var temporary=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(basePrefab,temporary);
            instance.SetActive(false);
            instance.AddComponent<EnemyPerception>().Configure(8,11,2,1<<world);
            instance.SetActive(true);
            var variant=PrefabUtility.SaveAsPrefabAsset(instance,VariantPath);
            if(variant==null || PrefabUtility.GetPrefabAssetType(variant)!=PrefabAssetType.Variant)
                throw new InvalidOperationException("Expected a separate prefab variant.");
            UnityEngine.Object.DestroyImmediate(instance);
            if(!AssetDatabase.CopyAsset(ScenePaths.Asset(ScenePaths.CombatSandbox),path))
                throw new InvalidOperationException("Could not create new scene copy.");
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaController>();
            foreach(var old in UnityEngine.Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            var hero=UnityEngine.Object.FindFirstObjectByType<PlayerCombatFacade>();
            hero.transform.position=new Vector3(-12,-5,0);
            var positions=new[]{new Vector2(-7,-7),new Vector2(-6,-1),new Vector2(13,5)};
            var names=new[]{"NearZombie","HiddenZombie","FarZombie"};
            var enemies=new SandboxEnemy[3];
            for(int index=0;index<3;index++)
            {
                var enemy=(GameObject)PrefabUtility.InstantiatePrefab(variant,scene);
                enemy.name=names[index];enemy.transform.position=positions[index];
                enemies[index]=enemy.GetComponent<SandboxEnemy>();
            }
            ConfigureWall("North",new Vector2(0,10),new Vector2(36,.5f),world);
            ConfigureWall("South",new Vector2(0,-10),new Vector2(36,.5f),world);
            ConfigureWall("West",new Vector2(-18,0),new Vector2(.5f,20.5f),world);
            ConfigureWall("East",new Vector2(18,0),new Vector2(.5f,20.5f),world);
            Cover("CoverA",new Vector2(-9,-3),new Vector2(2,3),world,square);
            Cover("CoverB",new Vector2(4,2),new Vector2(3,2),world,square);
            var exit=GameObject.Find("Exit").transform;exit.position=new Vector3(15,-7,0);
            arena.Configure(hero,enemies,UnityEngine.Object.FindFirstObjectByType<TargetRegistry>(),
                UnityEngine.Object.FindFirstObjectByType<HudPresenter>(),exit);
            arena.name="Stage2DiagnosticArena";
            UnityEngine.Object.FindFirstObjectByType<ArenaCameraFit>().Configure(new Vector2(19,11));
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("S2 scene save failed.");
            var settings=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            settings.RemoveAll(entry=>entry.path==path);
            settings.Add(new EditorBuildSettingsScene(path,true));
            EditorBuildSettings.scenes=settings.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("S202_CREATED variant, World layer, two covers, three separated enemies. Stage 1 assets preserved.");
        }
        private static void ConfigureWall(string name,Vector2 position,Vector2 size,int world)
        {
            var wall=GameObject.Find(name);
            if(wall==null)throw new InvalidOperationException("Missing wall "+name);
            wall.layer=world;wall.transform.position=position;wall.transform.localScale=new Vector3(size.x,size.y,1);
            var box=wall.GetComponent<BoxCollider2D>();box.size=Vector2.one;box.offset=Vector2.zero;box.isTrigger=false;
        }
        private static void Cover(string name,Vector2 position,Vector2 size,int world,Sprite square)
        {
            var cover=new GameObject(name,typeof(SpriteRenderer),typeof(BoxCollider2D));
            cover.layer=world;cover.transform.position=position;cover.transform.localScale=new Vector3(size.x,size.y,1);
            cover.GetComponent<BoxCollider2D>().size=Vector2.one;
            cover.GetComponent<SpriteRenderer>().sprite=square;
            cover.GetComponent<SpriteRenderer>().color=new Color(.35f,.43f,.48f);
        }
    }
}
