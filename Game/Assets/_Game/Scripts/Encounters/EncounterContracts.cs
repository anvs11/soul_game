using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Encounters
{
    internal static class ContractGuard
    {
        public static string Id(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() != value) throw new ArgumentException("Nonblank stable Id required: " + name);
            return value;
        }
        public static float Number(float value, string name, float minimum = 0)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum) throw new ArgumentException("Finite value >= " + minimum + " required: " + name);
            return value;
        }
        public static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values, Func<T, string> key, string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            var copy = new List<T>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (value == null || !ids.Add(Id(key(value), name))) throw new ArgumentException("Null or duplicate " + name);
                copy.Add(value);
            }
            return copy.AsReadOnly();
        }
    }

    public static class EnemyTypes
    {
        public const string Melee = "melee", Jumper = "jumper", Shooter = "shooter";
        public const string SlimeGrenadier = "elite_slime_grenadier", ShieldShotgun = "elite_shield_shotgun", TeleportNurse = "elite_teleport_nurse";
        public static bool IsElite(string id) => id == SlimeGrenadier || id == ShieldShotgun || id == TeleportNurse;
        public static bool IsKnown(string id) => id == Melee || id == Jumper || id == Shooter || IsElite(id);
        public static float Radius(string id) => IsElite(id) ? 1f : .5f;
        public static int Cost(string id) => IsElite(id) ? 3 : 1;
    }
    public sealed class SpawnPoint
    {
        public string PointId { get; }
        public float X { get; }
        public float Y { get; }
        public float ClearanceRadius { get; }
        public bool IsHeroEntry { get; }
        public ReadOnlyCollection<string> AllowedEnemyTypeIds { get; }
        public SpawnPoint(string pointId, float x, float y, float clearanceRadius, IEnumerable<string> allowedEnemyTypeIds, bool isHeroEntry = false)
        {
            PointId = ContractGuard.Id(pointId, nameof(pointId));
            X = ContractGuard.Number(x, nameof(x), float.MinValue); Y = ContractGuard.Number(y, nameof(y), float.MinValue);
            ClearanceRadius = ContractGuard.Number(clearanceRadius, nameof(clearanceRadius), .001f);
            AllowedEnemyTypeIds = ContractGuard.Copy(allowedEnemyTypeIds, id => id, nameof(allowedEnemyTypeIds));
            IsHeroEntry = isHeroEntry;
            foreach (var id in AllowedEnemyTypeIds) if (!EnemyTypes.IsKnown(id)) throw new ArgumentException("Unknown enemy type: " + id);
            if (isHeroEntry && AllowedEnemyTypeIds.Count != 0) throw new ArgumentException("Hero entry cannot spawn enemies.");
        }
    }
    public sealed class RoomBudget
    {
        public int MinEnemies { get; }
        public int MaxEnemies { get; }
        public int MaxCost { get; }
        public int MaxWaves { get; }
        public int MaxSummons { get; }
        public RoomBudget(int minEnemies, int maxEnemies, int maxCost, int maxWaves, int maxSummons = 0)
        {
            if (minEnemies < 0 || maxEnemies < minEnemies || maxEnemies > 256 || maxCost < 0 || maxCost > 768 || maxWaves < 0 || maxWaves > 16 || maxSummons < 0 || maxSummons > 64 || (maxEnemies > 0 && maxWaves == 0))
                throw new ArgumentException("Invalid finite room budget.");
            MinEnemies = minEnemies; MaxEnemies = maxEnemies; MaxCost = maxCost; MaxWaves = maxWaves; MaxSummons = maxSummons;
        }
    }
    public sealed class EncounterRoom
    {
        public string RoomId { get; }
        public float CenterX { get; }
        public float CenterY { get; }
        public float Width { get; }
        public float Height { get; }
        public bool IsElite { get; }
        public RoomBudget Budget { get; }
        public ReadOnlyCollection<SpawnPoint> Points { get; }
        public EncounterRoom(string roomId, float centerX, float centerY, float width, float height, RoomBudget budget, IEnumerable<SpawnPoint> points, bool isElite = false)
        {
            RoomId = ContractGuard.Id(roomId, nameof(roomId));
            CenterX = ContractGuard.Number(centerX, nameof(centerX), float.MinValue); CenterY = ContractGuard.Number(centerY, nameof(centerY), float.MinValue);
            Width = ContractGuard.Number(width, nameof(width), .01f); Height = ContractGuard.Number(height, nameof(height), .01f);
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));
            Points = ContractGuard.Copy(points, p => p.PointId, nameof(points)); IsElite = isElite;
            foreach (var point in Points)
                if (Math.Abs(point.X) + point.ClearanceRadius > Width / 2 || Math.Abs(point.Y) + point.ClearanceRadius > Height / 2)
                    throw new ArgumentException("Point outside room clearance: " + RoomId + "/" + point.PointId);
        }
    }
    public sealed class EncounterMap
    {
        public string MapId { get; }
        public int MapVersion { get; }
        public ReadOnlyCollection<EncounterRoom> Rooms { get; }
        public EncounterMap(string mapId, int mapVersion, IEnumerable<EncounterRoom> rooms)
        {
            MapId = ContractGuard.Id(mapId, nameof(mapId));
            if (mapVersion < 1) throw new ArgumentException("Invalid mapVersion.");
            MapVersion = mapVersion; Rooms = ContractGuard.Copy(rooms, r => r.RoomId, nameof(rooms));
            if (Rooms.Count == 0) throw new ArgumentException("Map requires rooms.");
        }
    }
    public sealed class EncounterRequest
    {
        public int Seed { get; }
        public int GeneratorVersion { get; }
        public int ContentVersion { get; }
        public EncounterMap Map { get; }
        public EncounterRequest(int seed, int generatorVersion, int contentVersion, EncounterMap map)
        {
            if (generatorVersion < 1 || contentVersion < 1) throw new ArgumentException("Invalid generator/content version.");
            Seed = seed; GeneratorVersion = generatorVersion; ContentVersion = contentVersion; Map = map ?? throw new ArgumentNullException(nameof(map));
        }
    }
    public sealed class SpawnPlan
    {
        public string SpawnId { get; }
        public string EnemyTypeId { get; }
        public string PointId { get; }
        public int ReservedSummons { get; }
        public SpawnPlan(string spawnId, string enemyTypeId, string pointId, int reservedSummons = 0)
        {
            SpawnId = ContractGuard.Id(spawnId, nameof(spawnId)); EnemyTypeId = ContractGuard.Id(enemyTypeId, nameof(enemyTypeId)); PointId = ContractGuard.Id(pointId, nameof(pointId));
            if (SpawnId.Contains("/summon/") || reservedSummons < 0 || reservedSummons > 64) throw new ArgumentException("Invalid reserved summon namespace/count.");
            ReservedSummons = reservedSummons;
        }
        public string SummonId(int index)
        {
            if (index < 0 || index >= ReservedSummons) throw new ArgumentOutOfRangeException(nameof(index));
            return SpawnId + "/summon/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
    public sealed class WavePlan
    {
        public string WaveId { get; }
        public float DelaySeconds { get; }
        public ReadOnlyCollection<SpawnPlan> Spawns { get; }
        public WavePlan(string waveId, float delaySeconds, IEnumerable<SpawnPlan> spawns)
        {
            WaveId = ContractGuard.Id(waveId, nameof(waveId)); DelaySeconds = ContractGuard.Number(delaySeconds, nameof(delaySeconds));
            if (delaySeconds > 60) throw new ArgumentException("Wave delay exceeds 60 scaled seconds.");
            Spawns = ContractGuard.Copy(spawns, s => s.SpawnId, nameof(spawns));
            if (Spawns.Count == 0) throw new ArgumentException("Use an empty RoomPlan for a safe room, not an empty wave.");
        }
    }
    public sealed class RoomPlan
    {
        public string RoomId { get; }
        public ReadOnlyCollection<WavePlan> Waves { get; }
        public RoomPlan(string roomId, IEnumerable<WavePlan> waves)
        { RoomId = ContractGuard.Id(roomId, nameof(roomId)); Waves = ContractGuard.Copy(waves, w => w.WaveId, nameof(waves)); }
    }
    public sealed class EncounterPlan
    {
        public int Seed { get; }
        public int GeneratorVersion { get; }
        public int ContentVersion { get; }
        public string MapId { get; }
        public int MapVersion { get; }
        public ReadOnlyCollection<RoomPlan> Rooms { get; }
        public EncounterPlan(int seed, int generatorVersion, int contentVersion, string mapId, int mapVersion, IEnumerable<RoomPlan> rooms)
        {
            if (generatorVersion < 1 || contentVersion < 1 || mapVersion < 1) throw new ArgumentException("Invalid plan versions.");
            Seed = seed; GeneratorVersion = generatorVersion; ContentVersion = contentVersion;
            MapId = ContractGuard.Id(mapId, nameof(mapId)); MapVersion = mapVersion;
            Rooms = ContractGuard.Copy(rooms, r => r.RoomId, nameof(rooms));
        }
    }
    public sealed class EncounterResult
    {
        public bool IsSuccess => Plan != null;
        public EncounterPlan Plan { get; }
        public bool UsedFallback { get; }
        public string Diagnostic { get; }
        private EncounterResult(EncounterPlan plan, bool fallback, string diagnostic)
        { Plan = plan; UsedFallback = fallback; Diagnostic = ContractGuard.Id(diagnostic, nameof(diagnostic)); }
        public static EncounterResult Success(EncounterPlan plan, bool usedFallback, string diagnostic) => new EncounterResult(plan ?? throw new ArgumentNullException(nameof(plan)), usedFallback, diagnostic);
        public static EncounterResult Failure(string diagnostic) => new EncounterResult(null, false, diagnostic);
    }
    public interface IEncounterPlanner { EncounterResult Generate(EncounterRequest request); }
}
