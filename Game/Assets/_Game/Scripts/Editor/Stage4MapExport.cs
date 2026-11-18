using System;
using System.Collections.Generic;
using System.IO;
using Game.StreetMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Encounters;

namespace Game.Editor
{
    public static class Stage4MapExport
    {
        [Serializable] public sealed class PointData {public string pointId;public float x,y,clearanceRadius;public bool isHeroEntry;public string[] allowedEnemyTypeIds;}
        [Serializable] public sealed class RoomData {public string roomId;public float centerX,centerY,width,height;public bool isElite;public int minEnemies,maxEnemies,maxCost,maxWaves,maxSummons;public PointData[] points;}
        [Serializable] public sealed class ExportData {public string mapId;public int mapVersion;public int geometryVersion;public float cellSize=1;public bool elitePrefabsCertified=false;public string certification="World colliders, ordinary proxy .6 and hero .45; elite proxy 1 only";public int reachableCells;public RoomData[] rooms;public RectInt[] corridors;public Vector2[] certifiedSummonPoints;}
        public static void Export() => Write($"Docs/Exports/S4_FIXED_MAP_V{StreetMapCatalog.Version}.json", false);
        public static void ExportCombat() => Write($"Docs/Exports/S4_FIXED_MAP_V{StreetMapCatalog.Version}_COMBAT.json", true);
        private static void Write(string output, bool certifyCombat)
        {
            FoundationScenes.EnsureEditorReady();
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
            if (certifyCombat) CertifyElite(data, map.Rooms[4], reached);
            using(var writer=new StreamWriter(new FileStream(output,FileMode.CreateNew)))writer.Write(JsonUtility.ToJson(data,true));
            Debug.Log("S404B_EXPORT "+output+" reachable="+reached.Count+" points="+result.Count);
            // Existing scene is never saved.
        }
        private static void CertifyElite(ExportData data, EncounterRoom room, HashSet<Vector2Int> reached)
        {
            foreach (string id in new[] { "SlimeGrenadier", "ShieldShotgun", "TeleportNurse", "MiniSlime" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Stage4Combat/" + id + ".prefab");
                var circle = prefab != null ? prefab.GetComponent<CircleCollider2D>() : null;
                if (circle == null || circle.radius + circle.offset.magnitude > (id == "MiniSlime" ? .28f : 1f) ||
                    prefab.transform.localScale != Vector3.one) throw new InvalidOperationException("Unexpected combat collider: " + id);
            }
            var points = new List<Vector2>();
            int world = LayerMask.GetMask("World");
            foreach (var point in room.Points)
            {
                if (point.IsHeroEntry) continue;
                Vector2 origin = new Vector2(room.CenterX + point.X, room.CenterY + point.Y);
                foreach (var candidate in StreetMapCatalog.SummonCandidates(origin, 0))
                {
                    Vector2 delta = candidate - origin;
                    if (!reached.Contains(Vector2Int.FloorToInt(candidate)) || Physics2D.OverlapCircle(candidate, .32f, world) ||
                        Physics2D.CircleCast(origin, .28f, delta.normalized, delta.magnitude, world))
                        throw new InvalidOperationException("Blocked summon point at " + candidate);
                    points.Add(candidate);
                }
            }
            data.elitePrefabsCertified = true;
            data.certification = "Stage4Combat circle colliders at all elite points. Summon ring validated at authored starts; runtime rechecks live occupancy and walls.";
            data.certifiedSummonPoints = points.ToArray();
        }
    }
}
