using System;
using System.Collections.Generic;

namespace Game.Encounters
{
    // Pure data validation. The Unity exporter remains responsible for physical reachability.
    public static class EncounterPlanValidator
    {
        public static EncounterResult Validate(EncounterMap map, EncounterPlan plan)
        {
            if (map == null || plan == null) return EncounterResult.Failure("NULL_DATA: map/plan required");
            if (plan.MapId != map.MapId || plan.MapVersion != map.MapVersion) return EncounterResult.Failure("MAP_VERSION: plan does not match map");
            if (plan.Rooms.Count != map.Rooms.Count) return EncounterResult.Failure("ROOM_COUNT: every map room requires a plan");
            var spawnIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var roomPlan in plan.Rooms)
            {
                EncounterRoom room = null;
                foreach (var candidate in map.Rooms) if (candidate.RoomId == roomPlan.RoomId) room = candidate;
                if (room == null) return EncounterResult.Failure("UNKNOWN_ROOM: " + roomPlan.RoomId);
                int count = 0, cost = 0, summons = 0;
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
                        if (room.IsElite != EnemyTypes.IsElite(spawn.EnemyTypeId)) return EncounterResult.Failure("ROOM_ENEMY_KIND: " + room.RoomId);
                        for (int previous = 0; previous < index; previous++)
                        {
                            var other = wave.Spawns[previous]; var otherPoint = FindPoint(room, other.PointId);
                            float dx = point.X - otherPoint.X, dy = point.Y - otherPoint.Y;
                            float separation = EnemyTypes.Radius(spawn.EnemyTypeId) + EnemyTypes.Radius(other.EnemyTypeId);
                            if (dx * dx + dy * dy < separation * separation) return EncounterResult.Failure("SPAWN_OVERLAP: " + spawn.SpawnId);
                        }
                        count++; cost += EnemyTypes.Cost(spawn.EnemyTypeId); summons += spawn.ReservedSummons;
                        if (EnemyTypes.IsElite(spawn.EnemyTypeId)) eliteTypes.Add(spawn.EnemyTypeId);
                    }
                }
                if (count < room.Budget.MinEnemies || count > room.Budget.MaxEnemies || cost > room.Budget.MaxCost || summons > room.Budget.MaxSummons)
                    return EncounterResult.Failure("ROOM_BUDGET: " + room.RoomId);
                if (room.IsElite && (count != 3 || eliteTypes.Count != 2)) return EncounterResult.Failure("ELITE_3_2: " + room.RoomId);
            }
            return EncounterResult.Success(plan, false, "VALID: finite encounter data; physics export still required");
        }
        public static SpawnPoint FindPoint(EncounterRoom room, string pointId)
        {
            foreach (var point in room.Points) if (point.PointId == pointId) return point;
            return null;
        }
    }
}
