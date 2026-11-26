using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Encounters;
using Game.Progression;
using UnityEngine;

namespace Game.StreetMap
{
    public static class RanchLayout
    {
        public const string SegmentId = "ranch/5-1";
        public const string EntryId = "entry";
        public const string YardId = "yard";
        public const string PenId = "pen";
        public const string BarnId = "barn";
        public const float SpawnClearance = .65f;
        public const int PassageWidth = 4;
        internal const float WallWidth = .44f;

        public static IReadOnlyList<StreetMapLayout.Room> Rooms { get; } = Array.AsReadOnly(new[]
        {
            new StreetMapLayout.Room(EntryId, new RectInt(-7, -6, 14, 12)),
            new StreetMapLayout.Room(YardId, new RectInt(13, -9, 20, 18)),
            new StreetMapLayout.Room(PenId, new RectInt(39, -9, 22, 18)),
            new StreetMapLayout.Room(BarnId, new RectInt(67, -11, 26, 22))
        });

        public static IReadOnlyList<RectInt> Corridors { get; } = Array.AsReadOnly(Rooms.Zip(Rooms.Skip(1),
            (from, to) => new RectInt(from.Bounds.xMax, -PassageWidth / 2,
                to.Bounds.xMin - from.Bounds.xMax, PassageWidth)).ToArray());

        public static Rect CameraBounds => new(-8, -12, 102, 24);
        public static Vector2 TrapPosition => new(48, 5.6f);
        public static Vector2 AmmoPosition => new(64, .65f);
        public static Vector2 ExitPosition => new(89, 4.5f);

        private static readonly string[] Ordinary = { EnemyTypes.Melee, EnemyTypes.Jumper, EnemyTypes.Shooter };
        private static readonly EncounterMap Map = new(SegmentId, 1,
            Rooms.Select(CreateRoom).ToArray());

        public static EncounterMap CreateMap() => Map;

        public static EncounterPlan Plan(int seed)
        {
            var rooms = Map.Rooms.Select(room => PlanRoom(seed, room));
            var plan = new EncounterPlan(seed, 1, 1, Map.MapId, Map.MapVersion, rooms);
            var check = EncounterPlanValidator.Validate(Map, plan);
            if (!check.IsSuccess) throw new InvalidOperationException(check.Diagnostic);
            return plan;
        }

        internal static string RoomLabel(string roomId) => roomId switch
        {
            YardId => "Двор",
            PenId => "Загон",
            BarnId => "Амбар",
            _ => "Вход на ранчо"
        };

        private static EncounterRoom CreateRoom(StreetMapLayout.Room room)
        {
            bool entry = room.Id == EntryId;
            bool barn = room.Id == BarnId;
            var budget = entry ? new RoomBudget(0, 0, 0, 0) : barn
                ? new RoomBudget(1, 1, EnemyTypes.Cost(EnemyTypes.RanchDeer), 1)
                : new RoomBudget(4, 6, 6, 1);
            var points = new List<SpawnPoint>
            {
                new("hero", entry ? 0 : -room.Bounds.width / 2f + 1.5f, 0, .45f, Array.Empty<string>(), true)
            };
            if (barn)
                points.Add(new SpawnPoint("deer", 4, 0, EnemyTypes.Radius(EnemyTypes.RanchDeer), new[] { EnemyTypes.RanchDeer }));
            else if (!entry)
            {
                var offsets = new (float X, float Y)[]
                {
                    (-4, -4), (-4, 4), (.5f, -4), (.5f, 4), (5, -4), (5, 4), (5, 0)
                };
                for (int i = 0; i < offsets.Length; i++)
                    points.Add(new SpawnPoint("slot_" + Number(i), offsets[i].X, offsets[i].Y, SpawnClearance, Ordinary));
            }
            return new EncounterRoom(room.Id, room.Center.x, room.Center.y, room.Bounds.width, room.Bounds.height,
                budget, points, barn);
        }

        private static RoomPlan PlanRoom(int seed, EncounterRoom room)
        {
            if (room.RoomId == EntryId) return new RoomPlan(room.RoomId, Array.Empty<WavePlan>());
            string prefix = SegmentId + "/" + room.RoomId + "/";
            if (room.RoomId == BarnId)
                return new RoomPlan(room.RoomId, new[] { new WavePlan("boss", 0,
                    new[] { new SpawnPlan(prefix + "deer", EnemyTypes.RanchDeer, "deer") }) });

            int count = 4 + CityEconomy.Roll(seed, prefix, "count", 3);
            int typeOffset = CityEconomy.Roll(seed, prefix, "typeOffset", Ordinary.Length);
            var available = room.Points.Where(point => !point.IsHeroEntry).ToList();
            var spawns = new SpawnPlan[count];
            for (int index = 0; index < count; index++)
            {
                string number = Number(index);
                int chosen = CityEconomy.Roll(seed, prefix + number, "point", available.Count);
                var point = available[chosen];
                available.RemoveAt(chosen);
                string enemy = Ordinary[(index + typeOffset) % Ordinary.Length];
                spawns[index] = new SpawnPlan(prefix + number, enemy, point.PointId);
            }
            return new RoomPlan(room.RoomId, new[] { new WavePlan("ranch", 0, spawns) });
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
