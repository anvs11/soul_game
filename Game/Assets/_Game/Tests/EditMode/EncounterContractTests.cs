using System;
using System.Collections.Generic;
using System.Linq;
using Game.Encounters;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class EncounterContractTests
    {
        [Test]
        public void FiveRoomExample_HasFiniteWavesEliteThreeTwoAndExplicitSummonIds()
        {
            var map = EncounterExample.CreateMap(); var plan = EncounterExample.CreateFixedPlan(42);
            var result = EncounterPlanValidator.Validate(map,plan);
            Assert.That(result.IsSuccess,Is.True,result.Diagnostic);
            Assert.That(map.Rooms.Select(r=>r.RoomId),Is.EqualTo(new[] {"entry","combat_a","combat_b","combat_c","elite"}));
            Assert.That(plan.Rooms[2].Waves[1].DelaySeconds,Is.EqualTo(2));
            Assert.That(plan.Seed,Is.EqualTo(42));
            var elite = plan.Rooms[4].Waves[0].Spawns;
            Assert.That(elite.Count,Is.EqualTo(3));
            Assert.That(elite.Select(s=>s.EnemyTypeId).Distinct().Count(),Is.EqualTo(2));
            Assert.That(elite.Sum(s=>s.ReservedSummons),Is.EqualTo(8));
            Assert.That(elite[0].SummonId(3),Is.EqualTo("elite/0/summon/3"));
            Assert.Throws<ArgumentOutOfRangeException>(()=>elite[0].SummonId(4));
        }
        [Test]
        public void Constructors_CopyMutableCollectionsAndNeverLeakASetter()
        {
            var allowed = new List<string>{EnemyTypes.Melee};
            var point = new SpawnPoint("p",0,0,1,allowed); allowed.Clear();
            Assert.That(point.AllowedEnemyTypeIds,Is.EqualTo(new[]{EnemyTypes.Melee}));
            var spawns = new List<SpawnPlan>{new SpawnPlan("a",EnemyTypes.Melee,"p")};
            var wave = new WavePlan("w",1,spawns); spawns.Clear();
            Assert.That(wave.Spawns.Count,Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(()=>((IList<SpawnPlan>)wave.Spawns).Clear());
            var map=EncounterExample.CreateMap(); var rooms=map.Rooms.ToList();
            var copied=new EncounterMap("copy",1,rooms); rooms.Clear();
            Assert.That(copied.Rooms.Count,Is.EqualTo(5));
        }
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        [TestCase(61f)]
        public void NonFiniteOrUnboundedDelay_IsRejectedBeforeRuntime(float delay)
        { Assert.Throws<ArgumentException>(()=>new WavePlan("w",delay,new[]{new SpawnPlan("a",EnemyTypes.Melee,"p")})); }
        [Test]
        public void DuplicatesAndUnsafePointDeclarations_AreRejected()
        {
            var point=new SpawnPoint("p",0,0,1,new[]{EnemyTypes.Melee});
            Assert.Throws<ArgumentException>(()=>new EncounterRoom("r",0,0,10,10,new RoomBudget(1,2,2,1),new[]{point,point}));
            Assert.Throws<ArgumentException>(()=>new SpawnPoint("hero",0,0,1,new[]{EnemyTypes.Melee},true));
            Assert.Throws<ArgumentException>(()=>new EncounterRoom("r",0,0,2,2,new RoomBudget(1,2,2,1),new[]{new SpawnPoint("p",1,0,1,new[]{EnemyTypes.Melee})}));
            Assert.Throws<ArgumentException>(()=>new RoomBudget(1,257,257,1));
        }
        [TestCase("unknown", "ordinary_0", "UNKNOWN_ENEMY")]
        [TestCase("melee", "missing", "UNKNOWN_POINT")]
        [TestCase("melee", "hero_entry", "POINT_CLEARANCE")]
        public void InvalidPlan_ReturnsReadableFailureWithoutPlan(string type,string point,string code)
        {
            var original=EncounterExample.CreateFixedPlan(); var rooms=original.Rooms.ToArray();
            rooms[1]=new RoomPlan("combat_a",new[]{new WavePlan("w",0,new[]{new SpawnPlan("a",type,point)})});
            var broken=new EncounterPlan(0,1,1,original.MapId,1,rooms);
            var failure=EncounterPlanValidator.Validate(EncounterExample.CreateMap(),broken);
            Assert.That(failure.IsSuccess,Is.False); Assert.That(failure.Plan,Is.Null); Assert.That(failure.UsedFallback,Is.False);
            Assert.That(failure.Diagnostic,Does.StartWith(code+":"));
        }
        [Test]
        public void DuplicateSpawnAcrossRoomsAndOverlappingWave_AreRejected()
        {
            var original=EncounterExample.CreateFixedPlan(); var rooms=original.Rooms.ToArray();
            rooms[2]=new RoomPlan("combat_b",new[]{new WavePlan("w",0,new[]{new SpawnPlan("combat_a/0",EnemyTypes.Melee,"ordinary_0")})});
            var result=EncounterPlanValidator.Validate(EncounterExample.CreateMap(),new EncounterPlan(0,1,1,original.MapId,1,rooms));
            Assert.That(result.Diagnostic,Does.StartWith("DUPLICATE_SPAWN:"));
            rooms=original.Rooms.ToArray();
            rooms[1]=new RoomPlan("combat_a",new[]{new WavePlan("w",0,new[]{new SpawnPlan("x",EnemyTypes.Melee,"ordinary_0"),new SpawnPlan("y",EnemyTypes.Melee,"ordinary_0")})});
            result=EncounterPlanValidator.Validate(EncounterExample.CreateMap(),new EncounterPlan(0,1,1,original.MapId,1,rooms));
            Assert.That(result.Diagnostic,Does.StartWith("SPAWN_OVERLAP:"));
        }
        [Test]
        public void SummonOwnerAndVersionMismatch_AreExplicitFailures()
        {
            var original=EncounterExample.CreateFixedPlan(); var map=EncounterExample.CreateMap();
            Assert.That(EncounterPlanValidator.Validate(map,new EncounterPlan(0,1,1,map.MapId,2,original.Rooms)).Diagnostic,Does.StartWith("MAP_VERSION:"));
            var rooms=original.Rooms.ToArray();
            rooms[1]=new RoomPlan("combat_a",new[]{new WavePlan("w",0,new[]{new SpawnPlan("x",EnemyTypes.Melee,"ordinary_0",1)})});
            Assert.That(EncounterPlanValidator.Validate(map,new EncounterPlan(0,1,1,map.MapId,1,rooms)).Diagnostic,Does.StartWith("SUMMON_OWNER:"));
        }
    }
}
