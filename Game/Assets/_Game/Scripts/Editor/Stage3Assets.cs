using System;
using System.Collections.Generic;
using System.IO;
using Game.Actors;
using Game.Combat;
using Game.Core;
using Game.Sandbox;
using Game.Street;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Game.Editor
{
    public static class Stage3Assets
    {
        private const string Rooms = "Assets/_Game/Prefabs/Rooms/Street/";
        private const string Data = "Assets/_Game/Data/Stages/Street/";
        private static readonly string[] Ids = { "street_entry", "street_a", "street_b", "street_c", "street_rest", "street_boss" };
        public static void Create()
        {
            FoundationScenes.EnsureEditorReady();
            string path = ScenePaths.Asset(ScenePaths.Stage3StreetSandbox);
            if (File.Exists(path)) throw new InvalidOperationException("Street scene exists, preserved.");
            foreach (string id in Ids)
                if (File.Exists(Rooms + id + ".prefab") || File.Exists(Data + id + ".asset"))
                    throw new InvalidOperationException("Existing module preserved: " + id);
            Directory.CreateDirectory(Rooms); Directory.CreateDirectory(Data); AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var definitions = new List<StreetModuleDefinition>();
            foreach (string id in Ids) definitions.Add(CreateModule(id));
            if (!AssetDatabase.CopyAsset(ScenePaths.Asset(ScenePaths.Stage2BossSandbox), path))
                throw new InvalidOperationException("Street shell copy failed.");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.layer == LayerMask.NameToLayer("World") || root.GetComponent<IEnemyTarget>() != null || root.name == "Exit")
                    UnityEngine.Object.DestroyImmediate(root);
            var arena = UnityEngine.Object.FindFirstObjectByType<ArenaController>();
            arena.name = "StreetArena";
            arena.gameObject.AddComponent<StreetSceneLoader>().Configure(definitions.ToArray());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Street scene save failed.");
            var settings = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            settings.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = settings.ToArray();
            AssetDatabase.SaveAssets();
            Export(definitions);
            Debug.Log("STREET_ASSETS_READY six modules and fixed five-segment route");
        }
        private static StreetModuleDefinition CreateModule(string id)
        {
            var root = new GameObject(id);
            var module = root.AddComponent<StreetModule>();
            bool boss = id == "street_boss";
            bool combat = id == "street_a" || id == "street_b" || id == "street_c";
            Visual(root, "Asphalt", Vector2.zero, new Vector2(36,20), new Color(.07f,.1f,.11f), false, -5);
            Visual(root, "North", new Vector2(0,10), new Vector2(36,.5f), Color.gray, true);
            Visual(root, "South", new Vector2(0,-10), new Vector2(36,.5f), Color.gray, true);
            Visual(root, "West", new Vector2(-18,0), new Vector2(.5f,20.5f), Color.gray, true);
            Visual(root, "East", new Vector2(18,0), new Vector2(.5f,20.5f), Color.gray, true);
            module.Entry = Marker(root, "Entry", new Vector2(-14.5f,-6.5f), new Color(.1f,.4f,.4f));
            module.Exit = Marker(root, "Exit", new Vector2(14.5f,-6.5f), combat ? Color.red : Color.green);
            if (boss) module.Exit.GetComponent<SpriteRenderer>().enabled = false;
            if (id == "street_a") Cover(root, new Vector2(-10,6), new Vector2(4,3));
            if (id == "street_b")
            {
                Cover(root,new Vector2(0,6),new Vector2(6,2));
                Actor(root,"Assets/_Game/Prefabs/Actors/Stage2/ExplosiveBarrel.prefab",new Vector2(-5.5f,-6.5f));
            }
            if (id == "street_c")
            {
                Cover(root,new Vector2(-11,6),new Vector2(3,2));
                Cover(root,new Vector2(11,6),new Vector2(3,2));
            }
            var actors = new List<MonoBehaviour>();
            var spawnCells = new List<Vector2Int>();
            if (combat)
            {
                string[] names = { "SensingZombie", "JumpingEnemy", "ShootingEnemy" };
                Vector2[] positions = { new Vector2(-9.5f,-6.5f), new Vector2(-1.5f,-2.5f), new Vector2(10.5f,-4.5f) };
                for (int i = 0; i < names.Length; i++)
                {
                    var actor = Actor(root,"Assets/_Game/Prefabs/Actors/Stage2/"+names[i]+".prefab",positions[i]);
                    foreach (var component in actor.GetComponents<MonoBehaviour>())
                        if (component is IEnemyTarget) actors.Add(component);
                    spawnCells.Add(Cell(positions[i]));
                }
            }
            if (boss)
            {
                var actor = Actor(root,"Assets/_Game/Prefabs/Actors/Stage2/PoliceBoss.prefab",new Vector2(3.5f,1.5f));
                foreach(var component in actor.GetComponents<MonoBehaviour>()) if(component is IEnemyTarget) actors.Add(component);
                spawnCells.Add(Cell(actor.transform.position));
            }
            module.Enemies = actors.ToArray();
            var definition = ScriptableObject.CreateInstance<StreetModuleDefinition>();
            definition.Id=id;
            definition.Kind = boss ? StreetModuleKind.Boss : combat ? StreetModuleKind.Combat : id == "street_entry" ? StreetModuleKind.Entry : StreetModuleKind.Rest;
            definition.Entry=Cell(module.Entry.position); definition.Exit=Cell(module.Exit.position); definition.SpawnCells=spawnCells.ToArray();
            Physics2D.SyncTransforms();
            definition.Walkable=new bool[definition.Width*definition.Height];
            for(int y=0;y<definition.Height;y++) for(int x=0;x<definition.Width;x++)
                definition.Walkable[y*definition.Width+x]=Physics2D.OverlapCircle(definition.CellCenter(new Vector2Int(x,y)),definition.ActorClearance,LayerMask.GetMask("World"))==null;
            definition.Prefab=PrefabUtility.SaveAsPrefabAsset(root,Rooms+id+".prefab");
            if(definition.Prefab==null)throw new InvalidOperationException("Module save failed: "+id);
            AssetDatabase.CreateAsset(definition,Data+id+".asset");
            UnityEngine.Object.DestroyImmediate(root);
            return definition;
        }
        private static Vector2Int Cell(Vector2 position) => new Vector2Int(Mathf.FloorToInt(position.x+18),Mathf.FloorToInt(position.y+10));
        private static GameObject Actor(GameObject root,string path,Vector2 position)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)throw new InvalidOperationException("Missing actor "+path);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);
            instance.transform.position=position;return instance;
        }
        private static Transform Marker(GameObject root,string name,Vector2 position,Color color)
            => Visual(root,name,position,Vector2.one*.8f,color,false).transform;
        private static void Cover(GameObject root,Vector2 position,Vector2 size)
            => Visual(root,"Concrete barrier",position,size,new Color(.35f,.43f,.48f),true);
        private static GameObject Visual(GameObject root,string name,Vector2 position,Vector2 size,Color color,bool solid,int order=0)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localScale=new Vector3(size.x,size.y,1);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/PrototypeSquare.png");renderer.color=color;renderer.sortingOrder=order;
            if(solid){go.layer=LayerMask.NameToLayer("World");go.AddComponent<BoxCollider2D>();}
            return go;
        }
        [Serializable] private sealed class CatalogExport { public int contentVersion=1; public ModuleExport[] modules; }
        [Serializable] private sealed class ModuleExport
        {
            public string id,kind;public int width,height;public Vector2 origin;public float cellSize,actorClearance;
            public bool[] walkable;public Vector2Int entry,exit;public Vector2Int[] spawnCells;
        }
        private static void Export(List<StreetModuleDefinition> definitions)
        {
            var export=new CatalogExport{modules=new ModuleExport[definitions.Count]};
            for(int i=0;i<definitions.Count;i++)
            {
                var d=definitions[i];export.modules[i]=new ModuleExport{id=d.Id,kind=d.Kind.ToString(),width=d.Width,height=d.Height,origin=d.GridOrigin,cellSize=d.CellSize,actorClearance=d.ActorClearance,walkable=d.Walkable,entry=d.Entry,exit=d.Exit,spawnCells=d.SpawnCells};
            }
            File.WriteAllText("Docs/S3_STREET_CATALOG.json",JsonUtility.ToJson(export,true));
        }
    }
}
