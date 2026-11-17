using System;
using System.Collections.Generic;

namespace Game.Encounters
{
    // S4-01 reference data, not a seed planner or a physically certified catalog.
    public static class EncounterExample
    {
        public static EncounterMap CreateMap()
        {
            var ordinary = new[] { EnemyTypes.Melee, EnemyTypes.Jumper, EnemyTypes.Shooter };
            var elite = new[] { EnemyTypes.SlimeGrenadier, EnemyTypes.ShieldShotgun, EnemyTypes.TeleportNurse };
            var safe = new RoomBudget(0, 0, 0, 0);
            var budget = new RoomBudget(2, 4, 4, 2);
            return new EncounterMap("fixed_street_1_1", 1, new[]
            {
                new EncounterRoom("entry", 0, 0, 14, 12, safe, new[] { Entry(0,0) }),
                Ordinary("combat_a",23,0,budget,ordinary,Entry(-8,0)),
                Ordinary("combat_b",23,24,budget,ordinary,Entry(0,-6)),
                Ordinary("combat_c",51,24,budget,ordinary,Entry(-8,0)),
                new EncounterRoom("elite",83,24,28,24,new RoomBudget(3,3,9,1,12),new[]
                {
                    Entry(-12,0), new SpawnPoint("elite_0",-7,4,1,elite),
                    new SpawnPoint("elite_1",0,4,1,elite), new SpawnPoint("elite_2",7,4,1,elite)
                },true)
            });
        }
        private static SpawnPoint Entry(float x,float y) => new SpawnPoint("hero_entry",x,y,.6f,Array.Empty<string>(),true);
        private static EncounterRoom Ordinary(string id,float x,float y,RoomBudget budget,string[] types,SpawnPoint entry) =>
            new EncounterRoom(id,x,y,20,16,budget,new[] { entry, new SpawnPoint("ordinary_0",-3,2,.6f,types), new SpawnPoint("ordinary_1",3,2,.6f,types) });
        public static RoomPlan OrdinaryPlan(string roomId, bool delayed = false)
        {
            var first = new SpawnPlan(roomId + "/0",EnemyTypes.Melee,"ordinary_0");
            var second = new SpawnPlan(roomId + "/1",EnemyTypes.Melee,"ordinary_1");
            return delayed
                ? new RoomPlan(roomId,new[] {new WavePlan("wave_0",0,new[] {first}),new WavePlan("wave_1",2,new[] {second})})
                : new RoomPlan(roomId,new[] {new WavePlan("wave_0",0,new[] {first,second})});
        }
        public static EncounterPlan CreateFixedPlan(int seed = 0)
        {
            var rooms = new List<RoomPlan>
            {
                new RoomPlan("entry",Array.Empty<WavePlan>()), OrdinaryPlan("combat_a"), OrdinaryPlan("combat_b",true), OrdinaryPlan("combat_c"),
                new RoomPlan("elite",new[] {new WavePlan("elite_wave",0,new[]
                {
                    new SpawnPlan("elite/0",EnemyTypes.SlimeGrenadier,"elite_0",4),
                    new SpawnPlan("elite/1",EnemyTypes.ShieldShotgun,"elite_1"),
                    new SpawnPlan("elite/2",EnemyTypes.SlimeGrenadier,"elite_2",4)
                })})
            };
            return new EncounterPlan(seed,1,1,"fixed_street_1_1",1,rooms);
        }
    }
}
