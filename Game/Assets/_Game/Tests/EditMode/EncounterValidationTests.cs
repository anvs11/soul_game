using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Game.Encounters;
using NUnit.Framework;
using UnityEngine;
namespace Game.Tests
{
    public sealed class EncounterValidationTests
    {
        private static EncounterMap Map()=>EncounterPlannerTests.Map();
        private static EncounterPlan Plan(EncounterMap m,int seed=0)
        { var r=new EncounterPlanner().Generate(new EncounterRequest(seed,1,1,m));Assert.That(r.IsSuccess,Is.True,r.Diagnostic);return r.Plan; }
        private static EncounterPlan Edit(EncounterPlan p,string roomId,Func<RoomPlan,RoomPlan> edit)=>new EncounterPlan(p.Seed,p.GeneratorVersion,p.ContentVersion,p.MapId,p.MapVersion,p.Rooms.Select(r=>r.RoomId==roomId?edit(r):r));
        private static EncounterPlan SpawnEdit(EncounterPlan p,Func<SpawnPlan,SpawnPlan> edit)=>Edit(p,"combat_a",r=>new RoomPlan(r.RoomId,r.Waves.Select((w,wi)=>new WavePlan(w.WaveId,w.DelaySeconds,w.Spawns.Select((s,i)=>wi==0&&i==0?edit(s):s)))));
        [TestCase("enemy", "UNKNOWN_ENEMY")][TestCase("point","UNKNOWN_POINT")][TestCase("hero","POINT_CLEARANCE")][TestCase("summon","SUMMON_OWNER")]
        public void InvalidSpawnFieldsReturnExplicitFailure(string field,string code)
        {
            var m=Map();var p=Plan(m);var broken=SpawnEdit(p,s=>new SpawnPlan(s.SpawnId,field=="enemy"?"unknown":s.EnemyTypeId,field=="point"?"missing":field=="hero"?"hero_entry":s.PointId,field=="summon"?1:0));
            var r=EncounterPlanValidator.ValidateStage(m,broken);Assert.That(r.IsSuccess,Is.False);Assert.That(r.Plan,Is.Null);Assert.That(r.Diagnostic,Does.StartWith(code+":"));
        }
        [Test] public void UnknownRoomGlobalDuplicateAndSameWaveOverlapAreRejected()
        {
            var m=Map();var p=Plan(m);var source=p.Rooms.First(r=>r.RoomId=="combat_b").Waves[0].Spawns[0];
            var duplicate=SpawnEdit(p,s=>new SpawnPlan(source.SpawnId,s.EnemyTypeId,s.PointId));
            Assert.That(EncounterPlanValidator.ValidateStage(m,duplicate).Diagnostic,Does.StartWith("DUPLICATE_SPAWN"));
            var room=Edit(p,"combat_a",r=>new RoomPlan("unknown",r.Waves));Assert.That(EncounterPlanValidator.ValidateStage(m,room).Diagnostic,Does.StartWith("UNKNOWN_ROOM"));
            var overlap=Edit(p,"combat_a",r=>new RoomPlan(r.RoomId,r.Waves.Select(w=>new WavePlan(w.WaveId,w.DelaySeconds,w.Spawns.Select(s=>new SpawnPlan(s.SpawnId,s.EnemyTypeId,w.Spawns[0].PointId))))));
            Assert.That(EncounterPlanValidator.ValidateStage(m,overlap).IsSuccess,Is.False);
        }
        [Test] public void MissingEliteWrongCardinalityAndUndelayedSecondWaveAreRejected()
        {
            var m=Map();var p=Plan(m);
            var missing=new EncounterPlan(p.Seed,1,1,p.MapId,p.MapVersion,p.Rooms.Where(r=>r.RoomId!="elite"));
            Assert.That(EncounterPlanValidator.ValidateStage(m,missing).Diagnostic,Does.StartWith("ROOM_COUNT"));
            var two=Edit(p,"elite",r=>new RoomPlan(r.RoomId,new[]{new WavePlan("e",0,r.Waves[0].Spawns.Take(2))}));Assert.That(EncounterPlanValidator.ValidateStage(m,two).IsSuccess,Is.False);
            var wave=Edit(p,"combat_b",r=>new RoomPlan(r.RoomId,r.Waves.Select(w=>new WavePlan(w.WaveId,0,w.Spawns))));Assert.That(EncounterPlanValidator.ValidateStage(m,wave).Diagnostic,Does.StartWith("DELAYED_WAVE"));
        }
        [Test] public void HeroClearanceAndCapacityAreCheckedBeforeSuccess()
        {
            var m=Map();var p=Plan(m);
            var changed=new EncounterMap(m.MapId,m.MapVersion,m.Rooms.Select(r=>r.RoomId!="combat_a"?r:new EncounterRoom(r.RoomId,r.CenterX,r.CenterY,r.Width,r.Height,r.Budget,
                r.Points.Select(t=>t.IsHeroEntry?new SpawnPoint(t.PointId,r.Points.First(x=>!x.IsHeroEntry).X,r.Points.First(x=>!x.IsHeroEntry).Y,.6f,Array.Empty<string>(),true):t))));
            Assert.That(EncounterPlanValidator.ValidateStage(changed,p).IsSuccess,Is.False);
            var narrow=new EncounterMap(m.MapId,m.MapVersion,m.Rooms.Select(r=>r.RoomId!="elite"?r:new EncounterRoom(r.RoomId,r.CenterX,r.CenterY,r.Width,r.Height,r.Budget,r.Points.Select(t=>t.IsHeroEntry?t:new SpawnPoint(t.PointId,t.X,t.Y,.9f,t.AllowedEnemyTypeIds)),true)));
            Assert.That(EncounterPlanValidator.ValidateStage(narrow,p).Diagnostic,Does.StartWith("POINT_CLEARANCE"));
        }
        [Test] public void EliteThreeTwoIsInsufficientWhenSeededDuplicateIsWrong()
        {
            var m=Map();var p=Plan(m);var pair=EncounterPlanner.CreateEliteSchedule(0).Single(x=>x.StageId==m.MapId);
            var other=pair.DuplicateType==pair.FirstType?pair.SecondType:pair.FirstType;
            var wrong=Edit(p,"elite",r=>new RoomPlan(r.RoomId,new[]{new WavePlan("elite",0,r.Waves[0].Spawns.Select((v,i)=>new SpawnPlan(v.SpawnId,i==0?pair.FirstType:i==1?pair.SecondType:other,v.PointId)))}));
            Assert.That(EncounterPlanValidator.Validate(m,wrong).IsSuccess,Is.True,"Structurally3/2; complete stage must still reject wrong seeded duplicate");
            Assert.That(EncounterPlanValidator.ValidateStage(m,wrong).Diagnostic,Does.StartWith("ELITE_SCHEDULE"));
            var single=Edit(p,"elite",r=>new RoomPlan(r.RoomId,new[]{new WavePlan("elite",0,r.Waves[0].Spawns.Select(v=>new SpawnPlan(v.SpawnId,pair.FirstType,v.PointId)))}));
            Assert.That(EncounterPlanValidator.ValidateStage(m,single).Diagnostic,Does.StartWith("ELITE_3_2"));
        }
        private static EncounterMap Crowded()
        {
            var m=Map();return new EncounterMap(m.MapId,m.MapVersion,m.Rooms.Select(r=>r.RoomId!="combat_a"?r:new EncounterRoom(r.RoomId,r.CenterX,r.CenterY,r.Width,r.Height,r.Budget,
                r.Points.Concat(Enumerable.Range(0,8192).Select(i=>new SpawnPoint("zzbad_"+i.ToString("D5"),0,0,.1f,new[]{EnemyTypes.Melee,EnemyTypes.Jumper,EnemyTypes.Shooter}))))));
        }
        [Test] public void TwentyAttemptsTransitionToValidatedFallbackAndExplicitBadFallbackFails()
        {
            var m=Crowded();var request=new EncounterRequest(0,1,1,m);var r=new EncounterPlanner().Generate(request);
            Assert.That(r.IsSuccess,Is.True,r.Diagnostic);Assert.That(r.UsedFallback,Is.True);Assert.That(r.Diagnostic,Does.Contain("attempts=20").And.Contain("seed=0").And.Contain("G=1").And.Contain("C=1").And.Contain("validated fallback"));
            Assert.That(EncounterPlanValidator.ValidateStage(m,r.Plan).IsSuccess,Is.True);
            Assert.That(EncounterPlannerTests.Snapshot(new EncounterPlanner().Generate(request,r.Plan).Plan),Is.EqualTo(EncounterPlannerTests.Snapshot(r.Plan)));
            var wrong=new EncounterPlan(99,1,1,m.MapId,m.MapVersion,r.Plan.Rooms);
            Assert.That(new EncounterPlanner().Generate(request,wrong).Diagnostic,Does.Contain("FALLBACK_METADATA"));
            var bad=SpawnEdit(r.Plan,s=>new SpawnPlan(s.SpawnId,"bad",s.PointId));var failed=new EncounterPlanner().Generate(request,bad);
            Assert.That(failed.IsSuccess,Is.False);Assert.That(failed.Plan,Is.Null);Assert.That(failed.UsedFallback,Is.False);Assert.That(failed.Diagnostic,Does.Contain("FALLBACK_INVALID"));
            TestContext.WriteLine(r.Diagnostic);
        }
        [Test] public void BrokenDefaultFallbackAndRepeatedErrorsNeverReturnPlan()
        {
            var m=Map();var broken=new EncounterMap(m.MapId,m.MapVersion,m.Rooms.Select(r=>r.RoomId!="elite"?r:new EncounterRoom(r.RoomId,r.CenterX,r.CenterY,r.Width,r.Height,r.Budget,r.Points.Where(p=>p.IsHeroEntry),true)));
            var request=new EncounterRequest(-1,1,1,broken);var planner=new EncounterPlanner();string message=null;
            for(int i=0;i<4;i++){var r=planner.Generate(request);Assert.That(r.Plan,Is.Null);Assert.That(r.Diagnostic,Does.Contain("FALLBACK_FAILED"));if(message!=null)Assert.That(r.Diagnostic,Is.EqualTo(message));message=r.Diagnostic;Plan(m);}
        }
        [Test] public void FullScheduleRejectsMissingStagesAndMixedSeeds()
        {
            var m=Map();var maps=new[]{m,new EncounterMap("fixed_street_1_2",m.MapVersion,m.Rooms),new EncounterMap("fixed_street_1_4",m.MapVersion,m.Rooms)};
            var plans=maps.Select(x=>Plan(x)).ToArray();Assert.That(EncounterPlanValidator.ValidateSchedule(maps.Reverse(),plans.Reverse()).IsValid,Is.True);
            Assert.That(EncounterPlanValidator.ValidateSchedule(maps,plans.Take(2)).IsValid,Is.False);
            plans[1]=Plan(maps[1],123);Assert.That(EncounterPlanValidator.ValidateSchedule(maps,plans).IsValid,Is.False);
        }
        [Test] public void ThousandReferenceSeedsRepeatAndValidateFutureSchedule()
        {
            var map=Map();var timer=Stopwatch.StartNew();var first=new string[1000];int fallbacks=0;
            var maps=new[]{map,new EncounterMap("fixed_street_1_2",map.MapVersion,map.Rooms),new EncounterMap("fixed_street_1_4",map.MapVersion,map.Rooms)};
            for(int pass=0;pass<2;pass++)for(int seed=0;seed<1000;seed++)
            {
                var results=maps.Select(m=>new EncounterPlanner().Generate(new EncounterRequest(seed,1,1,m))).ToArray();foreach(var r in results)Assert.That(r.IsSuccess,Is.True,r.Diagnostic);
                var validation=EncounterPlanValidator.ValidateSchedule(maps,results.Select(r=>r.Plan));Assert.That(validation.IsValid,Is.True,string.Join("|",validation.Reasons));
                var snapshot=string.Join("|",results.Select(r=>EncounterPlannerTests.Snapshot(r.Plan)));if(pass==0){first[seed]=snapshot;if(results[0].UsedFallback)fallbacks++;}else Assert.That(snapshot,Is.EqualTo(first[seed]));
            }
            TestContext.WriteLine("REFERENCE seeds0..999 x2; 3 future plan stages; success=1000 failure=0 fallback="+fallbacks+" elapsedMs="+timer.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
        }
        [Serializable] private sealed class Export { public string mapId;public int mapVersion,geometryVersion;public bool elitePrefabsCertified;public Room[] rooms;public UnityEngine.Vector2[] certifiedSummonPoints; }
        [Serializable] private sealed class Room { public string roomId;public float centerX,centerY,width,height;public bool isElite;public int minEnemies,maxEnemies,maxCost,maxWaves,maxSummons;public Point[] points; }
        [Serializable] private sealed class Point { public string pointId;public float x,y,clearanceRadius;public bool isHeroEntry;public string[] allowedEnemyTypeIds; }
        [TestCase("S4_FIXED_MAP_V2.json", false)]
        [TestCase("S4_FIXED_MAP_V2_COMBAT.json", true)]
        public void ActualExportAndUnityCatalogThousandSeedsMatchSeparately(string filename, bool verifyCombat)
        {
            var relativePath=Path.Combine("Docs", "Exports", filename);
            var path=File.Exists(relativePath) ? relativePath : Path.Combine("Game", relativePath);
            var bytes=File.ReadAllBytes(path);string hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            var data=JsonUtility.FromJson<Export>(System.Text.Encoding.UTF8.GetString(bytes));Assert.That(data.rooms.Length,Is.EqualTo(5));Assert.That(data.elitePrefabsCertified,Is.EqualTo(verifyCombat));
            if (verifyCombat) Assert.That(data.certifiedSummonPoints.Distinct().Count(), Is.EqualTo(24));
            var map=new EncounterMap(data.mapId,data.mapVersion,data.rooms.Select(r=>new EncounterRoom(r.roomId,r.centerX,r.centerY,r.width,r.height,new RoomBudget(r.minEnemies,r.maxEnemies,r.maxCost,r.maxWaves,r.maxSummons),r.points.Select(p=>new SpawnPoint(p.pointId,p.x,p.y,p.clearanceRadius,p.allowedEnemyTypeIds,p.isHeroEntry)),r.isElite)));
            var unity=Game.StreetMap.StreetMapCatalog.Create();
            Assert.That(data.geometryVersion,Is.EqualTo(1));Assert.That(map.MapId,Is.EqualTo(unity.MapId));Assert.That(map.MapVersion,Is.EqualTo(unity.MapVersion));
            foreach(var r in map.Rooms)
            {
                var u=unity.Rooms.Single(x=>x.RoomId==r.RoomId);
                Assert.That(new[]{r.CenterX,r.CenterY,r.Width,r.Height},Is.EqualTo(new[]{u.CenterX,u.CenterY,u.Width,u.Height}));Assert.That(r.IsElite,Is.EqualTo(u.IsElite));
                Assert.That(new[]{r.Budget.MinEnemies,r.Budget.MaxEnemies,r.Budget.MaxCost,r.Budget.MaxWaves,r.Budget.MaxSummons},Is.EqualTo(new[]{u.Budget.MinEnemies,u.Budget.MaxEnemies,u.Budget.MaxCost,u.Budget.MaxWaves,u.Budget.MaxSummons}));
                Assert.That(r.Points.Count,Is.EqualTo(u.Points.Count));
                foreach(var point in r.Points)
                {
                    var up=u.Points.Single(x=>x.PointId==point.PointId);
                    Assert.That(new[]{point.X,point.Y,point.ClearanceRadius},Is.EqualTo(new[]{up.X,up.Y,up.ClearanceRadius}));
                    Assert.That(point.IsHeroEntry,Is.EqualTo(up.IsHeroEntry));Assert.That(point.AllowedEnemyTypeIds.OrderBy(x=>x,StringComparer.Ordinal),Is.EqualTo(up.AllowedEnemyTypeIds.OrderBy(x=>x,StringComparer.Ordinal)));
                }
            }
            var timer=Stopwatch.StartNew();int fallback=0;
            for(int seed=0;seed<1000;seed++)
            {
                var a=new EncounterPlanner().Generate(new EncounterRequest(seed,1,1,map));var b=new EncounterPlanner().Generate(new EncounterRequest(seed,1,1,unity));
                Assert.That(a.IsSuccess,Is.True,a.Diagnostic);Assert.That(b.IsSuccess,Is.True,b.Diagnostic);Assert.That(EncounterPlanValidator.ValidateStage(map,a.Plan).IsSuccess,Is.True);
                Assert.That(EncounterPlannerTests.Snapshot(a.Plan),Is.EqualTo(EncounterPlannerTests.Snapshot(b.Plan)));if(a.UsedFallback)fallback++;
            }
            Assert.That(fallback, Is.Zero);
            TestContext.WriteLine("ACTUAL EXPORT sha256="+hash+" MapVersion="+data.mapVersion+" GeometryVersion="+data.geometryVersion+" eliteCertified="+data.elitePrefabsCertified+" seeds=1000 success=1000 failure=0 fallback="+fallback+" elapsedMs="+timer.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
