using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Contracts;
using Game.Encounters;

namespace Game.Progression
{
    public static class CityEconomy
    {
        public const string HeavyBossEnemyTypeId = "heavy_boss";
        public const string SmallZombieEnemyTypeId = "small_zombie";
        public const int WeaponServicePrice = 3;
        public const int BottlePrice = 4;
        public const double WeaponServiceSeconds = 2;

        internal static IReadOnlyList<string> Stages { get; } =
            Array.AsReadOnly(new[] { "1-1", "1-2", "1-3", "1-4", "1-5" });

        public static bool CanIdentifyDeath(string spawnId, string enemyTypeId) =>
            !string.IsNullOrWhiteSpace(spawnId) && (IsSummon(spawnId) || EnemyTypes.IsKnown(enemyTypeId) ||
                enemyTypeId == HeavyBossEnemyTypeId || enemyTypeId == SmallZombieEnemyTypeId);

        public static (int Ammo, int Coins, int Crystals) RollLoot(int seed, string spawnId, string enemyTypeId)
        {
            if (!CanIdentifyDeath(spawnId, enemyTypeId))
                throw new ArgumentException("Expected a known City enemy and a stable SpawnId.");
            if (IsSummon(spawnId)) return (0, 0, 0);

            int ammo = enemyTypeId == EnemyTypes.Shooter && Roll(seed, spawnId, "ammo", 2) == 0 ? 8 : 0;
            int coins = Roll(seed, spawnId, "coin-chance", 10) < 3
                ? 1 + Roll(seed, spawnId, "coin-count", 3)
                : 0;
            int crystals = enemyTypeId switch
            {
                HeavyBossEnemyTypeId => 270,
                SmallZombieEnemyTypeId => 11 + Roll(seed, spawnId, "crystals", 5),
                _ when EnemyTypes.IsElite(enemyTypeId) => 75,
                _ => 5 + Roll(seed, spawnId, "crystals", 6)
            };
            return (ammo, coins, crystals);
        }

        private static bool IsSummon(string spawnId) =>
            spawnId.IndexOf("/summon/", StringComparison.Ordinal) >= 0;

        internal static int Roll(int seed, string id, string channel, int bound)
        {
            unchecked
            {
                // Hash the two fields separately, with an out-of-range UTF-16 delimiter.
                uint state = 2166136261u ^ (uint)seed;
                foreach (char value in id) state = (state ^ value) * 16777619u;
                state = (state ^ 65536u) * 16777619u;
                foreach (char value in channel) state = (state ^ value) * 16777619u;
                uint limit = uint.MaxValue - uint.MaxValue % (uint)bound;
                uint valueInRange;
                do
                {
                    state += 0x9e3779b9u;
                    valueInRange = state;
                    valueInRange = (valueInRange ^ (valueInRange >> 16)) * 0x85ebca6bu;
                    valueInRange = (valueInRange ^ (valueInRange >> 13)) * 0xc2b2ae35u;
                    valueInRange ^= valueInRange >> 16;
                } while (valueInRange >= limit);
                return (int)(valueInRange % (uint)bound);
            }
        }

        internal static List<ProgressionNpcSnapshot> PlanNpcs(int seed,
            IReadOnlyList<ProgressionTransitionPoint> points)
        {
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<ProgressionNpcSnapshot>();
            AddNpcs(ProgressionNpcKind.WeaponDealer, Roll(seed, "city", "weapon-count", 2), "weapon");
            AddNpcs(ProgressionNpcKind.BottleDealer, Roll(seed, "city", "bottle-count", 3), "bottle");
            return result;

            void AddNpcs(ProgressionNpcKind kind, int count, string channel)
            {
                // Use single-purpose entries first so that shared entries can serve either NPC.
                var candidates = points.Where(point => (point.AllowedNpcs & kind) != 0 &&
                        !occupied.Contains(point.PointId))
                    .OrderBy(point => point.AllowedNpcs != kind)
                    .ThenBy(point => Roll(seed, point.PointId, channel, int.MaxValue))
                    .ThenBy(point => point.PointId, StringComparer.Ordinal)
                    .Take(count)
                    .ToArray();

                bool weapon = kind == ProgressionNpcKind.WeaponDealer;
                for (int i = 0; i < candidates.Length; i++)
                {
                    var point = candidates[i];
                    occupied.Add(point.PointId);
                    string npcId = channel + "-" + (i + 1).ToString(CultureInfo.InvariantCulture);
                    result.Add(new ProgressionNpcSnapshot(point, npcId, kind,
                        weapon ? "weapon-upgrade" : "dealer-bottle",
                        weapon ? WeaponServicePrice : BottlePrice, ProgressionNpcState.Offered));
                }
            }
        }
    }
}
