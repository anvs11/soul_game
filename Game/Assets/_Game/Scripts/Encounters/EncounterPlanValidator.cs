using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;

namespace Game.Encounters
{
    public sealed class EncounterScheduleValidation
    {
        public bool IsValid => Reasons.Count==0;
        public ReadOnlyCollection<string> Reasons { get; }
        internal EncounterScheduleValidation(IEnumerable<string> reasons) { Reasons=new List<string>(reasons).AsReadOnly(); }
    }
    // Pure data validation. The Unity exporter remains responsible for physical reachability.
    public static class EncounterPlanValidator
    {
        public static EncounterResult Validate(EncounterMap map, EncounterPlan plan)
        {
            if (map == null || plan == null) return EncounterResult.Failure("NULL_DATA: map/plan required");
            if(plan.GeneratorVersion!=1 || plan.ContentVersion!=1)return EncounterResult.Failure("UNSUPPORTED_VERSION: algorithm/content1 required");
            if (plan.MapId != map.MapId || plan.MapVersion != map.MapVersion) return EncounterResult.Failure("MAP_VERSION: plan does not match map");
            if (plan.Rooms.Count != map.Rooms.Count) return EncounterResult.Failure("ROOM_COUNT: every map room requires a plan");
            var spawnIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var roomPlan in plan.Rooms)
            {
                EncounterRoom room = null;
                foreach (var candidate in map.Rooms) if (candidate.RoomId == roomPlan.RoomId) room = candidate;
                if (room == null) return EncounterResult.Failure("UNKNOWN_ROOM: " + roomPlan.RoomId);
                int count = 0, cost = 0, summons = 0;
                bool boss = false;
                var eliteTypes = new HashSet<string>(StringComparer.Ordinal);
                if (roomPlan.Waves.Count > room.Budget.MaxWaves) return EncounterResult.Failure("WAVE_BUDGET: " + room.RoomId);
                foreach (var wave in roomPlan.Waves)
                {
                    for (int index = 0; index < wave.Spawns.Count; index++)
                    {
                        var spawn = wave.Spawns[index];
                        if (!spawnIds.Add(spawn.SpawnId)) return EncounterResult.Failure("DUPLICATE_SPAWN: " + spawn.SpawnId);
                        if (!EnemyTypes.IsKnown(spawn.EnemyTypeId)) return EncounterResult.Failure("UNKNOWN_ENEMY: " + spawn.EnemyTypeId);
                        SpawnPoint point = FindPoint(room, spawn.PointId);
                        if (point == null) return EncounterResult.Failure("UNKNOWN_POINT: " + room.RoomId + "/" + spawn.PointId);
                        if (point.IsHeroEntry || !point.AllowedEnemyTypeIds.Contains(spawn.EnemyTypeId) || point.ClearanceRadius < EnemyTypes.Radius(spawn.EnemyTypeId))
                            return EncounterResult.Failure("POINT_CLEARANCE: " + room.RoomId + "/" + spawn.PointId);
                        if (spawn.ReservedSummons > 0 && spawn.EnemyTypeId != EnemyTypes.SlimeGrenadier)
                            return EncounterResult.Failure("SUMMON_OWNER: " + spawn.SpawnId);
                        foreach(var hero in room.Points.Where(p=>p.IsHeroEntry))
                            if(Overlap(point,EnemyTypes.Radius(spawn.EnemyTypeId),hero,.45f))return EncounterResult.Failure("HERO_OVERLAP: "+spawn.SpawnId);
                        for(int child=0;child<spawn.ReservedSummons;child++)
                            if(!spawnIds.Add(spawn.SummonId(child)))return EncounterResult.Failure("DUPLICATE_SUMMON: "+spawn.SpawnId);
                        bool isBoss = EnemyTypes.IsBoss(spawn.EnemyTypeId);
                        boss |= isBoss;
                        if (room.IsElite != (EnemyTypes.IsElite(spawn.EnemyTypeId) || isBoss))
                            return EncounterResult.Failure("ROOM_ENEMY_KIND: " + room.RoomId);
                        for (int previous = 0; previous < index; previous++)
                        {
                            var other = wave.Spawns[previous]; var otherPoint = FindPoint(room, other.PointId);
                            if (Overlap(point,EnemyTypes.Radius(spawn.EnemyTypeId),otherPoint,EnemyTypes.Radius(other.EnemyTypeId))) return EncounterResult.Failure("SPAWN_OVERLAP: " + spawn.SpawnId);
                        }
                        count++; cost += EnemyTypes.Cost(spawn.EnemyTypeId); summons += spawn.ReservedSummons;
                        if (EnemyTypes.IsElite(spawn.EnemyTypeId)) eliteTypes.Add(spawn.EnemyTypeId);
                    }
                }
                if (count < room.Budget.MinEnemies || count > room.Budget.MaxEnemies || cost > room.Budget.MaxCost || summons > room.Budget.MaxSummons)
                    return EncounterResult.Failure("ROOM_BUDGET: " + room.RoomId);
                if (boss && count != 1) return EncounterResult.Failure("BOSS_COUNT: " + room.RoomId);
                if (room.IsElite && !boss && (count != 3 || eliteTypes.Count != 2))
                    return EncounterResult.Failure("ELITE_3_2: " + room.RoomId);
            }
            return EncounterResult.Success(plan, false, "VALID: finite encounter data; physics export still required");
        }
        private static bool Overlap(SpawnPoint a,float ar,SpawnPoint b,float br)
        { double dx=(double)a.X-b.X,dy=(double)a.Y-b.Y,r=(double)ar+br;return dx*dx+dy*dy<r*r; }
        public static EncounterResult ValidateStage(EncounterMap map,EncounterPlan plan)
        {
            var result=Validate(map,plan);if(!result.IsSuccess)return result;
            var ids=new[]{"entry","combat_a","combat_b","combat_c","elite"};
            if(map.Rooms.Count!=5 || ids.Any(id=>!map.Rooms.Any(r=>r.RoomId==id)))return EncounterResult.Failure("STAGE_SHAPE: five fixed rooms required");
            if(!new[]{"fixed_street_1_1","fixed_street_1_2","fixed_street_1_4"}.Contains(map.MapId))return EncounterResult.Failure("STAGE_ID: unsupported map");
            foreach(var room in map.Rooms)
            {
                if(room.IsElite!=(room.RoomId=="elite"))return EncounterResult.Failure("STAGE_KIND: "+room.RoomId);
                if(room.Points.Count(p=>p.IsHeroEntry)!=1 || room.Points.Any(p=>p.IsHeroEntry && p.ClearanceRadius<.45f))return EncounterResult.Failure("HERO_ENTRY: "+room.RoomId);
                var rp=plan.Rooms.Single(r=>r.RoomId==room.RoomId);
                if(room.RoomId=="entry")
                {
                    if(rp.Waves.Count!=0 || room.Budget.MaxEnemies!=0)return EncounterResult.Failure("ENTRY_BUDGET: safe room required");continue;
                }
                if(rp.Waves.Count<1 || rp.Waves.Count>2 || rp.Waves[0].DelaySeconds!=0)return EncounterResult.Failure("STAGE_WAVES: "+room.RoomId);
                if(RequiresDelayedWave(room) && (rp.Waves.Count!=2 || rp.Waves[1].DelaySeconds<=0))return EncounterResult.Failure("DELAYED_WAVE: combat_b");
                var types=rp.Waves.SelectMany(w=>w.Spawns).Select(s=>s.EnemyTypeId).ToArray();
                if(!room.IsElite && types.Length>=3 && types.Distinct().Count()!=3)return EncounterResult.Failure("ORDINARY_VARIETY: "+room.RoomId);
                if(room.IsElite)
                {
                    if(rp.Waves.Count!=1)return EncounterResult.Failure("ELITE_WAVES: one wave required");
                    var pair=EncounterPlanner.CreateEliteSchedule(plan.Seed,plan.GeneratorVersion,plan.ContentVersion).Single(p=>p.StageId==map.MapId);
                    if(types.Count(t=>t==pair.DuplicateType)!=2 || types.Any(t=>t!=pair.FirstType && t!=pair.SecondType))return EncounterResult.Failure("ELITE_SCHEDULE: pair/duplicate mismatch");
                }
            }
            return result;
        }
        internal static bool RequiresDelayedWave(EncounterRoom room) => room.RoomId == "combat_b" && room.Budget.MaxWaves > 1;
        public static EncounterScheduleValidation ValidateSchedule(IEnumerable<EncounterMap> maps,IEnumerable<EncounterPlan> plans)
        {
            var errors=new List<string>();
            if(maps==null || plans==null)return new EncounterScheduleValidation(new[]{"NULL_SCHEDULE"});
            var bank=maps.ToArray();var output=plans.ToArray();
            var stages=new[]{"fixed_street_1_1","fixed_street_1_2","fixed_street_1_4"};
            if(bank.Length!=3 || output.Length!=3 || stages.Any(id=>bank.Count(m=>m!=null && m.MapId==id)!=1 || output.Count(p=>p!=null && p.MapId==id)!=1))return new EncounterScheduleValidation(new[]{"SCHEDULE_STAGES: three unique maps/plans required"});
            var first=output[0];
            foreach(var plan in output)
            {
                if(plan.Seed!=first.Seed || plan.GeneratorVersion!=first.GeneratorVersion || plan.ContentVersion!=first.ContentVersion)errors.Add("SCHEDULE_METADATA");
                var check=ValidateStage(bank.Single(m=>m.MapId==plan.MapId),plan);if(!check.IsSuccess)errors.Add(check.Diagnostic);
            }
            var types=output.SelectMany(p=>p.Rooms).Where(r=>r.RoomId=="elite").SelectMany(r=>r.Waves).SelectMany(w=>w.Spawns).Select(s=>s.EnemyTypeId).Distinct().ToArray();
            if(types.Length!=3 || types.Any(t=>!EnemyTypes.IsElite(t)))errors.Add("SCHEDULE_COVERAGE");
            return new EncounterScheduleValidation(errors);
        }
        public static SpawnPoint FindPoint(EncounterRoom room, string pointId)
        {
            foreach (var point in room.Points) if (point.PointId == pointId) return point;
            return null;
        }
    }
}
