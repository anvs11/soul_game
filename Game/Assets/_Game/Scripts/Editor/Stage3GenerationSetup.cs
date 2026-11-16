using System;
using System.IO;
using Game.Street;
using UnityEditor;
using UnityEngine;
namespace Game.Editor
{
    public static class Stage3GenerationSetup
    {
        [Serializable] private sealed class Catalog
        {
            public int schemaVersion=1, generatorVersion=1, contentVersion=1;
            public Module[] modules;
        }
        [Serializable] private sealed class Module
        {
            public string id, kind;
            public int width, height;
            public Vector2 origin;
            public float cellSize, actorClearance;
            public bool[] walkable;
            public int[] entry, exit;
            public Vector2Int[] spawnCells;
        }
        public static void Create()
        {
            FoundationScenes.EnsureEditorReady();
            const string assetPath="Assets/_Game/Resources/StreetCatalog.asset";
            const string exportPath="Docs/Exports/S3_STREET_CATALOG.json";
            if(File.Exists(assetPath)||File.Exists(exportPath))throw new InvalidOperationException("Generation resources already exist, preserved.");
            Directory.CreateDirectory("Assets/_Game/Resources");Directory.CreateDirectory("Docs/Exports");AssetDatabase.Refresh();
            var ids=new[]{"street_entry","street_a","street_b","street_c","street_rest","street_boss"};
            var data=ScriptableObject.CreateInstance<StreetCatalogAsset>();data.Modules=new StreetModuleDefinition[ids.Length];
            var export=new Catalog{modules=new Module[ids.Length]};
            for(int i=0;i<ids.Length;i++)
            {
                var d=AssetDatabase.LoadAssetAtPath<StreetModuleDefinition>("Assets/_Game/Data/Stages/Street/"+ids[i]+".asset");
                if(d==null)throw new InvalidOperationException("Missing module "+ids[i]);
                data.Modules[i]=d;
                export.modules[i]=new Module{id=d.Id,kind=d.Kind.ToString(),width=d.Width,height=d.Height,origin=d.GridOrigin,
                    cellSize=d.CellSize,actorClearance=d.ActorClearance,walkable=d.Walkable,entry=new[]{d.Entry.x,d.Entry.y},exit=new[]{d.Exit.x,d.Exit.y},spawnCells=d.SpawnCells};
            }
            StreetGenerationAdapter.BuildCatalog(data.Modules);
            AssetDatabase.CreateAsset(data,assetPath);AssetDatabase.SaveAssets();
            File.WriteAllText(exportPath,JsonUtility.ToJson(export,true));
            Debug.Log("S306_CATALOG_READY");
        }
    }
}
