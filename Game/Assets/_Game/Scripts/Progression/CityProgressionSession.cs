using System;
using System.Collections.Generic;
using System.Linq;
using Game.Contracts;
using Game.Core;
using LootKind = Game.Contracts.ProgressionLootKind;

namespace Game.Progression
{
    // Call from the gameplay thread. The clock and combat sink are owned by the map adapter.
    public sealed partial class CityProgressionSession : ICityProgressionSession
    {
        private readonly Func<double> gameTime;
        private readonly Dictionary<(string RunId, string ActionId), CommandEntry> commands = new();
        private readonly HashSet<string> startedRuns = new(StringComparer.Ordinal);
        private IProgressionCombatSink combat;
        private RunState run;
        private ProgressionRunSummary? lastRun;
        private int confirmedCrystals;
        private bool executing;

        public CityProgressionSession(Func<double> gameTime, int confirmedCrystals = 0)
        {
            this.gameTime = gameTime ?? throw new ArgumentNullException(nameof(gameTime));
            if (confirmedCrystals < 0) throw new ArgumentOutOfRangeException(nameof(confirmedCrystals));
            this.confirmedCrystals = confirmedCrystals;
        }

        public ProgressionSnapshot Snapshot => new ProgressionSnapshot(
            new ProgressionProfileSnapshot(confirmedCrystals, ProgressionProfileState.Ready),
            run == null ? null : new ProgressionRunSnapshot(run.Id, run.Coins,
                run.Rooms.Values.Sum(room => room.PendingCrystals), run.Loot.Values, run.Npcs,
                activeBottleEffects: run.BottleEffects, stageId: run.StageId),
            lastRun);

        public void BindCombat(IProgressionCombatSink sink)
        {
            if (executing) throw new InvalidOperationException("Cannot replace the player during a progression command.");
            if (!ReferenceEquals(combat, sink)) combat?.SetProgressionModifiers(EmptyModules, EmptyEffects);
            combat = sink;
            RefreshCombat();
        }

        public ProgressionResult StartRun(ProgressionCommandKey key, int seed,
            IReadOnlyList<ProgressionTransitionPoint> transitionPoints)
        {
            var start = new StartRequest(seed,
                transitionPoints?.OrderBy(point => point.PointId, StringComparer.Ordinal).ToArray());
            return Execute(key, nameof(StartRun), start, () =>
            {
                if (run != null || startedRuns.Contains(key.RunId))
                    return Reject(ProgressionFailure.WrongPhase);
                if (!ValidPoints(start.Points)) return Reject(ProgressionFailure.InvalidRequest);
                run = new RunState(key.RunId, seed, CityEconomy.PlanNpcs(seed, start.Points));
                startedRuns.Add(key.RunId);
                return Applied;
            });
        }

        public ProgressionResult EnterStage(ProgressionCommandKey key, string stageId) =>
            Active(key, nameof(EnterStage), stageId, () =>
            {
                int next = run.Stage + 1;
                if (next >= CityEconomy.Stages.Count || CityEconomy.Stages[next] != stageId ||
                    run.Rooms.Values.Any(room => !room.Cleared))
                    return Reject(ProgressionFailure.WrongPhase);
                run.Stage = next;
                run.Loot.Clear();
                return Applied;
            });

        public ProgressionResult FinishRun(ProgressionCommandKey key, RunOutcome outcome) =>
            Active(key, nameof(FinishRun), outcome, () =>
            {
                if (!Enum.IsDefined(typeof(RunOutcome), outcome)) return Reject(ProgressionFailure.InvalidRequest);
                lastRun = new ProgressionRunSummary(run.Id, outcome, run.Coins, run.ConfirmedEarned);
                run = null;
                var previousCombat = combat;
                combat = null;
                previousCombat?.SetProgressionModifiers(EmptyModules, EmptyEffects);
                return Applied;
            });

        public ProgressionResult ConfirmEnemyDeath(ProgressionCommandKey key, ProgressionCheckpoint checkpoint,
            string spawnId, string enemyTypeId) =>
            Active(key, nameof(ConfirmEnemyDeath), (checkpoint, spawnId, enemyTypeId), () =>
            {
                if (!CityEconomy.CanIdentifyDeath(spawnId, enemyTypeId))
                    return Reject(ProgressionFailure.InvalidRequest);
                if (run.Deaths.TryGetValue(spawnId, out var prior))
                    return prior.Checkpoint.Equals(checkpoint) && prior.EnemyTypeId == enemyTypeId
                        ? Replayed
                        : Reject(ProgressionFailure.Conflict);

                var problem = CheckRoom(checkpoint, out var room);
                if (problem != ProgressionFailure.None) return Reject(problem);
                if (room?.Cleared == true) return Reject(ProgressionFailure.WrongPhase);

                var (ammo, coins, crystals) = CityEconomy.RollLoot(run.Seed, spawnId, enemyTypeId);
                room ??= AddRoom(checkpoint);
                run.Deaths.Add(spawnId, (checkpoint, enemyTypeId));
                room.Crystals += crystals;
                AddLoot(checkpoint, spawnId, LootKind.Ammo, ammo);
                AddLoot(checkpoint, spawnId, LootKind.Coins, coins);
                AddLoot(checkpoint, spawnId, LootKind.Crystals, crystals);
                return Applied;
            });

        public ProgressionResult CollectLoot(ProgressionCommandKey key, string pickupId) =>
            Active(key, nameof(CollectLoot), pickupId, () =>
            {
                if (!Id(pickupId)) return Reject(ProgressionFailure.InvalidRequest);
                if (!run.Loot.TryGetValue(pickupId, out var drop))
                    return Reject(run.CreatedPickupIds.Contains(pickupId)
                        ? ProgressionFailure.AlreadyConsumed : ProgressionFailure.UnknownTarget);

                int accepted = drop.Remaining;
                switch (drop.Kind)
                {
                    case LootKind.Ammo:
                        accepted = Math.Min(drop.Remaining, combat?.TryAddReserve(drop.Remaining) ?? 0);
                        if (accepted <= 0) return Reject(ProgressionFailure.ResourceFull);
                        break;
                    case LootKind.Coins:
                        run.Coins += accepted;
                        break;
                    case LootKind.Crystals:
                        run.Rooms[drop.Checkpoint.Index].PendingCrystals += accepted;
                        break;
                }

                if (accepted == drop.Remaining) run.Loot.Remove(pickupId);
                else run.Loot[pickupId] = new ProgressionLootSnapshot(pickupId, drop.Checkpoint, drop.SpawnId,
                    drop.Kind, drop.Remaining - accepted);
                return Applied;
            });

        public ProgressionResult CollectSupply(ProgressionCommandKey key, string supplyId, ProgressionSupplyKind kind,
            int amount) =>
            Active(key, nameof(CollectSupply), (supplyId, kind, amount), () =>
            {
                if (!Id(supplyId) || amount <= 0 || !Enum.IsDefined(typeof(ProgressionSupplyKind), kind))
                    return Reject(ProgressionFailure.InvalidRequest);
                if (run.UsedSupplies.Contains(supplyId)) return Reject(ProgressionFailure.AlreadyConsumed);

                float accepted = kind == ProgressionSupplyKind.Medkit
                    ? combat?.TryHeal(amount) ?? 0
                    : combat?.TryRepair(amount) ?? 0;
                if (!(accepted > 0)) return Reject(ProgressionFailure.ResourceFull);
                run.UsedSupplies.Add(supplyId);
                return Applied;
            });

        public ProgressionResult ClearCheckpoint(ProgressionCommandKey key, ProgressionCheckpoint checkpoint) =>
            Active(key, nameof(ClearCheckpoint), checkpoint, () =>
            {
                if (run.Rooms.TryGetValue(checkpoint.Index, out var previous) && previous.Cleared)
                    return previous.Checkpoint.Equals(checkpoint) ? Replayed : Reject(ProgressionFailure.Conflict);
                var problem = CheckRoom(checkpoint, out var room);
                if (problem != ProgressionFailure.None) return Reject(problem);

                room ??= AddRoom(checkpoint);
                confirmedCrystals = checked(confirmedCrystals + room.Crystals);
                run.ConfirmedEarned += room.Crystals;
                room.Cleared = true;
                room.PendingCrystals = 0;
                var uncollected = run.Loot.Values.Where(drop => drop.Checkpoint.Index == checkpoint.Index &&
                    drop.Kind == LootKind.Crystals).Select(drop => drop.PickupId).ToArray();
                foreach (string id in uncollected) run.Loot.Remove(id);
                if (run.BottleEffects.Count > 0)
                {
                    run.BottleEffects.Clear();
                    RefreshCombat();
                }
                return Applied;
            });

        // Profile offers and durable checkpoint writes are introduced in S5-05.
        public ProgressionResult BuyHomeUnlock(ProgressionCommandKey key, string offerId) =>
            Execute(key, nameof(BuyHomeUnlock), offerId, () => Reject(run != null
                ? ProgressionFailure.WrongPhase : ProgressionFailure.UnknownTarget), home: true);

        private ProgressionFailure CheckRoom(ProgressionCheckpoint checkpoint, out RoomState room)
        {
            run.Rooms.TryGetValue(checkpoint.Index, out room);
            if (checkpoint.Index < 0 || !Id(checkpoint.RoomId) || !Id(checkpoint.StageId))
                return ProgressionFailure.InvalidRequest;
            if (room != null && !room.Checkpoint.Equals(checkpoint))
                return ProgressionFailure.Conflict;
            if (room == null && run.Rooms.Values.Any(existing => existing.Checkpoint.StageId == checkpoint.StageId &&
                existing.Checkpoint.RoomId == checkpoint.RoomId))
                return ProgressionFailure.Conflict;
            return checkpoint.StageId == run.StageId ? ProgressionFailure.None : ProgressionFailure.WrongPhase;
        }

        private RoomState AddRoom(ProgressionCheckpoint checkpoint)
        {
            var room = new RoomState(checkpoint);
            run.Rooms.Add(checkpoint.Index, room);
            return room;
        }

        private void AddLoot(ProgressionCheckpoint checkpoint, string spawnId, LootKind kind, int amount)
        {
            if (amount == 0) return;
            string pickupId = spawnId + "/" + kind.ToString().ToLowerInvariant();
            run.Loot.Add(pickupId, new ProgressionLootSnapshot(pickupId, checkpoint, spawnId, kind, amount));
            run.CreatedPickupIds.Add(pickupId);
        }

        private static bool ValidPoints(ProgressionTransitionPoint[] points)
        {
            if (points == null) return false;
            var pointIds = new HashSet<string>(StringComparer.Ordinal);
            var entrances = new HashSet<(string Stage, string Road, string Entrance)>();
            const ProgressionNpcKind all = ProgressionNpcKind.WeaponDealer | ProgressionNpcKind.BottleDealer;
            return points.All(point => Id(point.PointId) && CityEconomy.Stages.Contains(point.StageId) &&
                Id(point.RoadId) && Id(point.EntranceId) && (point.AllowedNpcs & ~all) == 0 &&
                pointIds.Add(point.PointId) && entrances.Add((point.StageId, point.RoadId, point.EntranceId)));
        }

        private static bool Id(string value) => !string.IsNullOrWhiteSpace(value);

        private sealed class RoomState
        {
            public ProgressionCheckpoint Checkpoint { get; }
            public bool Cleared;
            public int PendingCrystals;
            public int Crystals;

            public RoomState(ProgressionCheckpoint checkpoint) => Checkpoint = checkpoint;
        }

        private sealed class RunState
        {
            public string Id { get; }
            public int Seed { get; }
            public int Stage;
            public string StageId => CityEconomy.Stages[Stage];
            public int Coins;
            public int ConfirmedEarned;
            public readonly List<ProgressionEffectSnapshot> BottleEffects = new();
            public List<ProgressionNpcSnapshot> Npcs { get; }
            public readonly Dictionary<int, RoomState> Rooms = new();
            public readonly Dictionary<string, (ProgressionCheckpoint Checkpoint, string EnemyTypeId)> Deaths =
                new(StringComparer.Ordinal);
            public readonly Dictionary<string, ProgressionLootSnapshot> Loot = new(StringComparer.Ordinal);
            public readonly HashSet<string> CreatedPickupIds = new(StringComparer.Ordinal);
            public readonly HashSet<string> UsedSupplies = new(StringComparer.Ordinal);
            public readonly Dictionary<string, ProgressionModuleKind> Modules = new(StringComparer.Ordinal);

            public RunState(string id, int seed, List<ProgressionNpcSnapshot> npcs)
            {
                Id = id;
                Seed = seed;
                Npcs = npcs;
            }
        }
    }
}
