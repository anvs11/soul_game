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

        public CityRegionRoute(EncounterPlan first) : this(first, false) { }

        internal CityRegionRoute(EncounterPlan first, bool sewer)
        {
            CityRegionDefinition.Load();
            var template = StreetMapCatalog.Create();
            maps[0] = template;
            plans[0] = first ?? throw new ArgumentNullException(nameof(first));
            var planner = new EncounterPlanner();
            for (int index = sewer ? 0 : 1; index < (sewer ? SewerStage.SegmentCount : SegmentCount); index++)
            {
                string segment = sewer ? SewerStage.IdAt(index) : IdAt(index);
                string generationId = sewer ? template.MapId : index == 1 ? "fixed_street_1_2" : index == 3 ? "fixed_street_1_4" : template.MapId;
                var generationMap = new EncounterMap(generationId, template.MapVersion, template.Rooms);
                int seed = sewer ? unchecked(first.Seed + (index + 15) * 1249) :
                    index == 1 || index == 3 ? first.Seed : unchecked(first.Seed + index * 1249);
                var generated = planner.Generate(new EncounterRequest(seed, first.GeneratorVersion, first.ContentVersion, generationMap));
                if (!generated.IsSuccess) throw new InvalidOperationException(generated.Diagnostic);
                var rooms = generationMap.Rooms.ToArray();
                var encounter = generated.Plan.Rooms.Select(room =>
                    new RoomPlan(room.RoomId, room.Waves.Select(wave =>
                        new WavePlan(wave.WaveId, wave.DelaySeconds, wave.Spawns.Select(spawn =>
                            new SpawnPlan(segment + "/" + spawn.SpawnId, spawn.EnemyTypeId, spawn.PointId, spawn.ReservedSummons)))))).ToArray();
                if (sewer || IsBoss(index))
                {
                    var final = rooms[4];
                    bool boss = !sewer || index == SewerStage.SegmentCount - 1;
                    string[] types = boss ? new[] { EnemyTypes.HeavyBoss } : new[] { EnemyTypes.Melee, EnemyTypes.Jumper, EnemyTypes.Shooter };
                    var points = types.Select((type, slot) =>
                        new SpawnPoint(boss ? "boss" : "final_" + slot, boss ? 0 : -7 + slot * 7, 4, boss ? 1.2f : .7f, new[] { type })).ToArray();
                    rooms[4] = new EncounterRoom(final.RoomId, final.CenterX, final.CenterY, final.Width, final.Height,
                        new RoomBudget(types.Length, types.Length, types.Sum(EnemyTypes.Cost), 1), points, boss);
                    int slot = Array.FindIndex(encounter, room => room.RoomId == final.RoomId);
                    encounter[slot] = new RoomPlan(final.RoomId, new[] { new WavePlan(boss ? "boss" : "final", 0, types.Select((type, i) =>
                        new SpawnPlan(segment + (boss ? "/boss" : "/guard/" + i), type, points[i].PointId))) });
                }
                var map = new EncounterMap((sewer ? "sewer_" : "city_") + segment, template.MapVersion, rooms);
                var plan = new EncounterPlan(first.Seed, first.GeneratorVersion, first.ContentVersion, map.MapId, map.MapVersion, encounter);
                var validation = EncounterPlanValidator.Validate(map, plan);
                if (!validation.IsSuccess) throw new InvalidOperationException(validation.Diagnostic);
                maps[index] = map;
                plans[index] = plan;
            }
        }
    }
}
