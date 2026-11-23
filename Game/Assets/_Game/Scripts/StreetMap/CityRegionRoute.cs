using System;
using System.Linq;
using Game.Encounters;
using Game.Progression;

namespace Game.StreetMap
{
    public sealed class CityRegionRoute
    {
        public const int SegmentCount = 15;
        private readonly EncounterMap[] maps = new EncounterMap[SegmentCount];
        private readonly EncounterPlan[] plans = new EncounterPlan[SegmentCount];
        public EncounterMap MapAt(int index) => maps[index];
        public EncounterPlan PlanAt(int index) => plans[index];
        public static string IdAt(int index) => CityEconomy.FullRoute[index];
        public static int DistrictAt(int index) => index / 5;
        public static bool IsBoss(int index) => CityRegionDefinition.Load().HasBoss(index);

        public CityRegionRoute(EncounterPlan first)
        {
            CityRegionDefinition.Load();
            var template = StreetMapCatalog.Create();
            maps[0] = template;
            plans[0] = first ?? throw new ArgumentNullException(nameof(first));
            var planner = new EncounterPlanner();
            for (int index = 1; index < SegmentCount; index++)
            {
                string segment = IdAt(index);
                string generationId = index == 1 ? "fixed_street_1_2" : index == 3 ? "fixed_street_1_4" : template.MapId;
                var generationMap = new EncounterMap(generationId, template.MapVersion, template.Rooms);
                int seed = index == 1 || index == 3 ? first.Seed : unchecked(first.Seed + index * 1249);
                var generated = planner.Generate(new EncounterRequest(seed, first.GeneratorVersion, first.ContentVersion, generationMap));
                if (!generated.IsSuccess) throw new InvalidOperationException(generated.Diagnostic);
                var rooms = generationMap.Rooms.ToArray();
                var encounter = generated.Plan.Rooms.Select(room =>
                    new RoomPlan(room.RoomId, room.Waves.Select(wave =>
                        new WavePlan(wave.WaveId, wave.DelaySeconds, wave.Spawns.Select(spawn =>
                            new SpawnPlan(segment + "/" + spawn.SpawnId, spawn.EnemyTypeId, spawn.PointId, spawn.ReservedSummons)))))).ToArray();
                if (IsBoss(index))
                {
                    var final = rooms[4];
                    const string point = "boss";
                    rooms[4] = new EncounterRoom(final.RoomId, final.CenterX, final.CenterY, final.Width, final.Height,
                        new RoomBudget(1, 1, EnemyTypes.Cost(EnemyTypes.HeavyBoss), 1),
                        new[] { new SpawnPoint(point, 0, 4, 1.2f, new[] { EnemyTypes.HeavyBoss }) }, true);
                    int slot = Array.FindIndex(encounter, room => room.RoomId == final.RoomId);
                    encounter[slot] = new RoomPlan(final.RoomId, new[] { new WavePlan("boss", 0,
                        new[] { new SpawnPlan(segment + "/boss", EnemyTypes.HeavyBoss, point) }) });
                }
                var map = new EncounterMap("city_" + segment, template.MapVersion, rooms);
                var plan = new EncounterPlan(first.Seed, first.GeneratorVersion, first.ContentVersion, map.MapId, map.MapVersion, encounter);
                var validation = EncounterPlanValidator.Validate(map, plan);
                if (!validation.IsSuccess) throw new InvalidOperationException(validation.Diagnostic);
                maps[index] = map;
                plans[index] = plan;
            }
        }
    }
}
