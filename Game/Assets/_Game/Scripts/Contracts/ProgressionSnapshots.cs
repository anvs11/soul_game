using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Contracts
{
    public enum ProgressionLootKind { Ammo, Coins, Crystals }
    public enum ProgressionNpcState { Offered, Working, ItemAvailable, ItemUsed }
    public enum ProgressionProfileState { Ready, RecoveredFromBackup, IncompatibleSchema }
    public enum ProgressionEffectKind { Beneficial, Harmful }

    public readonly struct ProgressionLootSnapshot
    {
        public string PickupId { get; }
        public ProgressionCheckpoint Checkpoint { get; }
        public string SpawnId { get; }
        public ProgressionLootKind Kind { get; }
        public int Remaining { get; }

        public ProgressionLootSnapshot(string pickupId, ProgressionCheckpoint checkpoint, string spawnId,
            ProgressionLootKind kind, int remaining)
        {
            PickupId = pickupId;
            Checkpoint = checkpoint;
            SpawnId = spawnId;
            Kind = kind;
            Remaining = remaining;
        }
    }

    public readonly struct ProgressionNpcSnapshot
    {
        public ProgressionTransitionPoint Point { get; }
        public string NpcId { get; }
        public ProgressionNpcKind Kind { get; }
        public string OfferId { get; }
        public int PriceCoins { get; }
        public ProgressionNpcState State { get; }
        public double? ReadyAtGameTime { get; }
        public string ItemId { get; }
        public string ItemDefinitionId { get; }
        public string WeaponInstanceId { get; }

        public ProgressionNpcSnapshot(ProgressionTransitionPoint point, string npcId, ProgressionNpcKind kind,
            string offerId, int priceCoins, ProgressionNpcState state, double? readyAtGameTime = null,
            string itemId = null, string itemDefinitionId = null, string weaponInstanceId = null)
        {
            Point = point;
            NpcId = npcId;
            Kind = kind;
            OfferId = offerId;
            PriceCoins = priceCoins;
            State = state;
            ReadyAtGameTime = readyAtGameTime;
            ItemId = itemId;
            ItemDefinitionId = itemDefinitionId;
            WeaponInstanceId = weaponInstanceId;
        }
    }

    public readonly struct ProgressionEffectSnapshot
    {
        public string EffectId { get; }
        public ProgressionEffectKind Kind { get; }

        public ProgressionEffectSnapshot(string effectId, ProgressionEffectKind kind)
        {
            EffectId = effectId;
            Kind = kind;
        }
    }

    public readonly struct ProgressionHomeOffer
    {
        public string OfferId { get; }
        public string UnlockId { get; }
        public int PriceCrystals { get; }

        public ProgressionHomeOffer(string offerId, string unlockId, int priceCrystals)
        {
            OfferId = offerId;
            UnlockId = unlockId;
            PriceCrystals = priceCrystals;
        }
    }

    public readonly struct ProgressionRunSummary
    {
        public string RunId { get; }
        public RunOutcome Outcome { get; }
        public int FinalCoins { get; }
        public int ConfirmedCrystalsEarned { get; }

        public ProgressionRunSummary(string runId, RunOutcome outcome, int finalCoins, int confirmedCrystalsEarned)
        {
            RunId = runId;
            Outcome = outcome;
            FinalCoins = finalCoins;
            ConfirmedCrystalsEarned = confirmedCrystalsEarned;
        }
    }

    public sealed class ProgressionRunSnapshot
    {
        public string RunId { get; }
        public int Coins { get; }
        public int PendingCrystals { get; }
        public IReadOnlyList<ProgressionLootSnapshot> Loot { get; }
        public IReadOnlyList<ProgressionNpcSnapshot> Npcs { get; }
        public ProgressionEffectSnapshot? ActiveBottleEffect { get; }

        public ProgressionRunSnapshot(string runId, int coins, int pendingCrystals,
            IEnumerable<ProgressionLootSnapshot> loot = null, IEnumerable<ProgressionNpcSnapshot> npcs = null,
            ProgressionEffectSnapshot? activeBottleEffect = null)
        {
            RunId = runId;
            Coins = coins;
            PendingCrystals = pendingCrystals;
            Loot = ProgressionSnapshot.Copy(loot);
            Npcs = ProgressionSnapshot.Copy(npcs);
            ActiveBottleEffect = activeBottleEffect;
        }
    }

    public sealed class ProgressionProfileSnapshot
    {
        public int ConfirmedCrystals { get; }
        public ProgressionProfileState State { get; }
        public IReadOnlyList<string> UnlockedIds { get; }
        public IReadOnlyList<ProgressionHomeOffer> HomeOffers { get; }

        public ProgressionProfileSnapshot(int confirmedCrystals, ProgressionProfileState state,
            IEnumerable<string> unlockedIds = null, IEnumerable<ProgressionHomeOffer> homeOffers = null)
        {
            ConfirmedCrystals = confirmedCrystals;
            State = state;
            UnlockedIds = ProgressionSnapshot.Copy(unlockedIds);
            HomeOffers = ProgressionSnapshot.Copy(homeOffers);
        }
    }

    public sealed class ProgressionSnapshot
    {
        public ProgressionProfileSnapshot Profile { get; }
        public ProgressionRunSnapshot Run { get; }
        public ProgressionRunSummary? LastRun { get; }

        public ProgressionSnapshot(ProgressionProfileSnapshot profile, ProgressionRunSnapshot run = null,
            ProgressionRunSummary? lastRun = null)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Run = run;
            LastRun = lastRun;
        }

        internal static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) =>
            Array.AsReadOnly(values?.ToArray() ?? Array.Empty<T>());
    }
}
