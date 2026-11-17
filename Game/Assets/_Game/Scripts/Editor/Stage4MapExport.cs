using System;
using System.Collections.Generic;
using System.IO;
using Game.StreetMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class Stage4MapExport
    {
        [Serializable] public sealed class PointData {public string pointId;public float x,y,clearanceRadius;public bool isHeroEntry;public string[] allowedEnemyTypeIds;}
        [Serializable] public sealed class RoomData {public string roomId;public float centerX,centerY,width,height;public bool isElite;public int minEnemies,maxEnemies,maxCost,maxWaves,maxSummons;public PointData[] points;}
        [Serializable] public sealed class ExportData {public string mapId;public int mapVersion;public int geometryVersion;public float cellSize=1;public bool elitePrefabsCertified=false;public string certification="World colliders, ordinary proxy .6 and hero .45; elite proxy 1 only";public int reachableCells;public RoomData[] rooms;public RectInt[] corridors;}
        public static void Export()
        {
            FoundationScenes.EnsureEditorReady();
            const string output="Docs/Exports/S4_FIXED_MAP_V2.json";
            if(File.Exists(output))throw new InvalidOperationException("Existing export preserved: "+output);
            var scene=EditorSceneManager.OpenScene(Stage4MapAssets.ScenePath,OpenSceneMode.Single);
            var map=StreetMapCatalog.Create();var reached=StreetMapCatalog.Reachable(.45f);
            var result=new List<RoomData>();
            foreach(var room in map.Rooms)
            {
                if(!reached.Contains(Vector2Int.FloorToInt(new Vector2(room.CenterX,room.CenterY))))throw new InvalidOperationException("Unreachable room: "+room.RoomId);
                var points=new List<PointData>();
                foreach(var point in room.Points)
                {
                    var world=new Vector2(room.CenterX+point.X,room.CenterY+point.Y);
                    if(Physics2D.OverlapCircle(world,point.ClearanceRadius,LayerMask.GetMask("World")) || !reached.Contains(Vector2Int.FloorToInt(world)))throw new InvalidOperationException("Unsafe point: "+room.RoomId+"/"+point.PointId);
                    points.Add(new PointData{pointId=point.PointId,x=point.X,y=point.Y,clearanceRadius=point.ClearanceRadius,isHeroEntry=point.IsHeroEntry,allowedEnemyTypeIds=new List<string>(point.AllowedEnemyTypeIds).ToArray()});
                }
                result.Add(new RoomData{roomId=room.RoomId,centerX=room.CenterX,centerY=room.CenterY,width=room.Width,height=room.Height,isElite=room.IsElite,minEnemies=room.Budget.MinEnemies,maxEnemies=room.Budget.MaxEnemies,maxCost=room.Budget.MaxCost,maxWaves=room.Budget.MaxWaves,maxSummons=room.Budget.MaxSummons,points=points.ToArray()});
            }
            // Also require a physical path to a final exit candidate in elite.
            if(!reached.Contains(new Vector2Int(85,24)))throw new InvalidOperationException("Final exit not reachable.");
            Directory.CreateDirectory("Docs/Exports");
            var data=new ExportData{mapId=map.MapId,mapVersion=map.MapVersion,geometryVersion=StreetMapLayout.GeometryVersion,reachableCells=reached.Count,rooms=result.ToArray(),corridors=new List<RectInt>(StreetMapLayout.Corridors).ToArray()};
            using(var writer=new StreamWriter(new FileStream(output,FileMode.CreateNew)))writer.Write(JsonUtility.ToJson(data,true));
            Debug.Log("S404B_EXPORT "+output+" reachable="+reached.Count+" points="+result.Count);
            // Existing scene is never saved.
        }
    }
}
