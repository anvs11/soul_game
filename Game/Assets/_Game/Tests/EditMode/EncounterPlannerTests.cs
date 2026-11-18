using System;
using System.Linq;
using System.Globalization;
using Game.Encounters;
using NUnit.Framework;
namespace Game.Tests
{
    public sealed class EncounterPlannerTests
    {
        internal static string Snapshot(EncounterPlan plan) => plan.Seed+"|"+plan.GeneratorVersion+"|"+plan.ContentVersion+"|"+plan.MapId+"|"+plan.MapVersion+"|"+
            string.Join(";",plan.Rooms.Select(r=>r.RoomId+":"+string.Join("/",r.Waves.Select(w=>w.WaveId+"@"+w.DelaySeconds.ToString("R",CultureInfo.InvariantCulture)+":"+string.Join(",",w.Spawns.Select(s=>s.SpawnId+"="+s.EnemyTypeId+"@"+s.PointId+"+"+s.ReservedSummons))))));
        internal static EncounterMap Map(string stage="fixed_street_1_1")
        { var map=EncounterExample.CreateMap();
            return new EncounterMap(stage,2,map.Rooms.Select(r=>r.RoomId=="entry"||r.IsElite?r:new EncounterRoom(r.RoomId,r.CenterX,r.CenterY,r.Width,r.Height,new RoomBudget(6,8,8,2),
                r.Points.Where(p=>p.IsHeroEntry).Concat(Enumerable.Range(0,8).Select(i=>new SpawnPoint("point_"+i,-6+(i%4)*4,-4+(i/4)*8,.6f,new[]{EnemyTypes.Melee,EnemyTypes.Jumper,EnemyTypes.Shooter})))))); }
        private static EncounterPlan Generate(int seed,EncounterMap map=null)
        {
            map=map??Map();var result=new EncounterPlanner().Generate(new EncounterRequest(seed,1,1,map));
            Assert.That(result.IsSuccess,Is.True,result.Diagnostic);Assert.That(result.UsedFallback,Is.False);
            Assert.That(EncounterPlanValidator.Validate(map,result.Plan).IsSuccess,Is.True);return result.Plan;
        }
        [TestCase(0)] [TestCase(123)] [TestCase(-1)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
        public void RepeatAndInterleavedCallsKeepFiveRoomsAndValidBudgets(int seed)
        {
            var planner=new EncounterPlanner();var map=Map();var input=new EncounterRequest(seed,1,1,map);
            var first=planner.Generate(input);Assert.That(first.IsSuccess,Is.True,first.Diagnostic);
            Generate(17);planner.Generate(new EncounterRequest(9,99,1,map));
            Assert.That(Snapshot(planner.Generate(input).Plan),Is.EqualTo(Snapshot(first.Plan)));
            Assert.That(first.Plan.Rooms.Select(r=>r.RoomId),Is.EqualTo(new[]{"entry","combat_a","combat_b","combat_c","elite"}));
            foreach(var room in first.Plan.Rooms.Where(r=>r.RoomId.StartsWith("combat_")))
            {
                var spawns=room.Waves.SelectMany(w=>w.Spawns).ToArray();Assert.That(spawns.Length,Is.InRange(6,8));
                Assert.That(spawns.Select(s=>s.EnemyTypeId).Distinct().Count(),Is.EqualTo(3));
            }
            Assert.That(first.Plan.Rooms.Single(r=>r.RoomId=="combat_b").Waves.Count,Is.EqualTo(2));
            Assert.That(first.Plan.Rooms.Single(r=>r.RoomId=="combat_b").Waves[1].DelaySeconds,Is.EqualTo(2));
            Assert.That(EncounterPlanValidator.Validate(map,first.Plan).IsSuccess,Is.True);
            TestContext.WriteLine(Snapshot(first.Plan));
        }
        [Test] public void VersionOneSeedZeroMatchesFrozenCompletePlan()
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())
            {
                var bytes=sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Snapshot(Generate(0))));
                Assert.That(BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant(),Is.EqualTo("a7d96878cb3f798444225410b6646eb9dcdcd7639f5ccebb65a8f7cc7fdaa222"));
            }
        }
        [Test] public void OriginalSmallContractExampleRemainsSupported()
        { var map=EncounterExample.CreateMap();Generate(0,map);Generate(123,map); }
        [Test] public void SearchWorkStopsAtDocumentedNodeLimit()
        {
            var map=Map();var broken=new EncounterMap(map.MapId,2,map.Rooms.Select(r=>r.RoomId!="combat_a"?r:
                new EncounterRoom(r.RoomId,r.CenterX,r.CenterY,r.Width,r.Height,r.Budget,
                    Enumerable.Range(0,64).Select(i=>new SpawnPoint("blocked_"+i,0,0,.6f,new[]{EnemyTypes.Melee,EnemyTypes.Jumper,EnemyTypes.Shooter})))));
            var result=new EncounterPlanner().Generate(new EncounterRequest(0,1,1,broken));
            Assert.That(result.IsSuccess,Is.False);Assert.That(result.Diagnostic,Does.Contain("attempts=20").And.Contain("searchNodes=1024"));
        }
        [Test] public void ReversedRoomsPointsAndAllowedTypesDoNotChangeResultOrMutateInputs()
        {
            var map=Map();var original=Snapshot(Generate(123,map));
            var reordered=new EncounterMap(map.MapId,map.MapVersion,map.Rooms.Reverse().Select(r=>new EncounterRoom(r.RoomId,r.CenterX,r.CenterY,r.Width,r.Height,r.Budget,
                r.Points.Reverse().Select(p=>new SpawnPoint(p.PointId,p.X,p.Y,p.ClearanceRadius,p.AllowedEnemyTypeIds.Reverse(),p.IsHeroEntry)),r.IsElite)));
            var before=string.Join(";",reordered.Rooms.Select(r=>r.RoomId+string.Join(",",r.Points.Select(p=>p.PointId))));
            Assert.That(Snapshot(Generate(123,reordered)),Is.EqualTo(original));
            Assert.That(string.Join(";",reordered.Rooms.Select(r=>r.RoomId+string.Join(",",r.Points.Select(p=>p.PointId)))),Is.EqualTo(before));
            Assert.That(Snapshot(Generate(123,map)),Is.EqualTo(original));
        }
        [Test] public void UnityRandomStateIsNeitherReadNorConsumed()
        {
            var saved=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(42);var state=UnityEngine.Random.state;var first=Snapshot(Generate(0));
                Assert.That(UnityEngine.Random.state,Is.EqualTo(state));
                UnityEngine.Random.InitState(987);for(int i=0;i<50;i++)UnityEngine.Random.Range(0,1000);
                Assert.That(Snapshot(Generate(0)),Is.EqualTo(first));
            }
            finally { UnityEngine.Random.state=saved; }
        }
        [Test] public void MultipleSeedsGiveSeveralValidRoutesAndSchedulesCoverAllThreeTypes()
        {
            var variants=new System.Collections.Generic.HashSet<string>();
            for(int seed=0;seed<64;seed++)
            {
                var p=Generate(seed);variants.Add(string.Join(";",p.Rooms.SelectMany(r=>r.Waves).SelectMany(w=>w.Spawns).Select(s=>s.EnemyTypeId+"@"+s.PointId)));
                var schedule=EncounterPlanner.CreateEliteSchedule(seed);Assert.That(schedule.Count,Is.EqualTo(3));
                Assert.That(schedule.SelectMany(s=>new[]{s.FirstType,s.SecondType}).Distinct().Count(),Is.EqualTo(3));
                foreach(var selection in schedule.Reverse())
                {
                    var elite=Generate(seed,Map(selection.StageId)).Rooms.Single(r=>r.RoomId=="elite").Waves.Single().Spawns;
                    Assert.That(elite.Count,Is.EqualTo(3));Assert.That(elite.Select(s=>s.EnemyTypeId).Distinct().Count(),Is.EqualTo(2));
                    Assert.That(elite.Count(s=>s.EnemyTypeId==selection.DuplicateType),Is.EqualTo(2));
                    Assert.That(elite.All(s=>s.EnemyTypeId==selection.FirstType||s.EnemyTypeId==selection.SecondType),Is.True);
                }
            }
            Assert.That(variants.Count,Is.GreaterThan(1));TestContext.WriteLine("64 seed variants="+variants.Count);
        }
        [Test] public void UnsupportedVersionsAndMissingRoomsHaveExplicitFailure()
        {
            var planner=new EncounterPlanner();Assert.That(planner.Generate(null).Plan,Is.Null);
            var version=planner.Generate(new EncounterRequest(0,2,1,Map()));Assert.That(version.IsSuccess,Is.False);Assert.That(version.Diagnostic,Does.Contain("UNSUPPORTED_VERSION"));
            Assert.Throws<ArgumentException>(()=>EncounterPlanner.CreateEliteSchedule(0,1,2));
            var bad=new EncounterMap("fixed_street_1_1",1,Map().Rooms.Take(4));
            Assert.That(planner.Generate(new EncounterRequest(0,1,1,bad)).Diagnostic,Does.Contain("MAP_SHAPE"));
        }
        [Test] public void InvalidPointBankExhaustsFinitelyAndRepeatedErrorDoesNotAccumulate()
        {
            var map=Map();var broken=new EncounterMap(map.MapId,1,map.Rooms.Select(r=>r.RoomId!="combat_a"?r:
                new EncounterRoom(r.RoomId,r.CenterX,r.CenterY,r.Width,r.Height,r.Budget,r.Points.Select(p=>p.IsHeroEntry?p:new SpawnPoint(p.PointId,p.X,p.Y,.1f,p.AllowedEnemyTypeIds)))));
            var planner=new EncounterPlanner();var request=new EncounterRequest(-1,1,1,broken);var first=planner.Generate(request);
            Assert.That(first.IsSuccess,Is.False);Assert.That(first.Plan,Is.Null);Assert.That(first.UsedFallback,Is.False);
            Assert.That(first.Diagnostic,Does.Contain("seed=-1").And.Contain("G=1").And.Contain("C=1").And.Contain("attempts=20").And.Contain("POINT_CAPACITY"));
            Generate(0);Assert.That(planner.Generate(request).Diagnostic,Is.EqualTo(first.Diagnostic));
        }
        [Test] public void ImpossibleBudgetDoesNotReturnSuccessfulEncounter()
        {
            var map=Map();var broken=new EncounterMap(map.MapId,1,map.Rooms.Select(r=>r.RoomId!="combat_b"?r:
                new EncounterRoom(r.RoomId,r.CenterX,r.CenterY,r.Width,r.Height,new RoomBudget(2,4,1,2),r.Points)));
            var result=new EncounterPlanner().Generate(new EncounterRequest(0,1,1,broken));
            Assert.That(result.Plan,Is.Null);Assert.That(result.Diagnostic,Does.Contain("ROOM_BUDGET"));
        }
        [Test] public void CollidingDistinctPointsCannotBeSelectedTogether()
        {
            var map=Map();var broken=new EncounterMap(map.MapId,1,map.Rooms.Select(r=>r.RoomId!="elite"?r:
                new EncounterRoom(r.RoomId,r.CenterX,r.CenterY,r.Width,r.Height,r.Budget,r.Points.Select(p=>p.IsHeroEntry?p:new SpawnPoint(p.PointId,0,0,1,p.AllowedEnemyTypeIds)),true)));
            var result=new EncounterPlanner().Generate(new EncounterRequest(0,1,1,broken));Assert.That(result.IsSuccess,Is.False);Assert.That(result.Plan,Is.Null);
        }
    }
}
