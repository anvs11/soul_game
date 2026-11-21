using System;
using System.Collections.Generic;
using Game.Contracts;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ProgressionContractTests
    {
        private const string RunId = "city-test-42";
        private static readonly ProgressionCheckpoint Room = new ProgressionCheckpoint("1-1", "combat_a", 0);
        private static readonly ProgressionTransitionPoint Alley = new ProgressionTransitionPoint(
            "1-1/a-to-b", "1-1", "a-to-b", "alley", ProgressionNpcKind.WeaponDealer);
        private static readonly ProgressionTransitionPoint Door = new ProgressionTransitionPoint(
            "1-1/b-to-c", "1-1", "b-to-c", "house-door", ProgressionNpcKind.BottleDealer);

        [Test]
        public void NewOperationAndSuccessfulReplayUseCurrentSnapshot()
        {
            var first = ProgressionResult.Applied(State(coins: 4));
            var replay = first.AsReplay(State(coins: 7));

            Assert.That(first.Status, Is.EqualTo(ProgressionStatus.Applied));
            Assert.That(first.Failure, Is.EqualTo(ProgressionFailure.None));
            Assert.That(first.Snapshot.Run.Coins, Is.EqualTo(4));
            Assert.That(replay.Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(replay.Failure, Is.EqualTo(ProgressionFailure.None));
            Assert.That(replay.Snapshot.Run.Coins, Is.EqualTo(7));
        }

        [Test]
        public void InsufficientCoinsReplayKeepsReasonAndShowsNewBalance()
        {
            var offer = new ProgressionNpcSnapshot(Alley, "broker", ProgressionNpcKind.WeaponDealer,
                "upgrade", 3, ProgressionNpcState.Offered);
            var refusal = ProgressionResult.Rejected(ProgressionFailure.InsufficientCoins,
                State(coins: 2, npcs: new[] { offer }));
            var replay = refusal.AsReplay(State(coins: 5, npcs: new[] { offer }));

            Assert.That(refusal.Status, Is.EqualTo(ProgressionStatus.Rejected));
            Assert.That(refusal.Snapshot.Run.Npcs[0].PriceCoins - refusal.Snapshot.Run.Coins, Is.EqualTo(1));
            Assert.That(replay.Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(replay.Failure, Is.EqualTo(ProgressionFailure.InsufficientCoins));
            Assert.That(replay.Snapshot.Run.Coins, Is.EqualTo(5));
        }

        [TestCase(ProgressionFailure.SaveFailed)]
        [TestCase(ProgressionFailure.ServiceNotReady)]
        public void RetryableFailureMustBeProcessedAgain(ProgressionFailure reason)
        {
            var failure = ProgressionResult.RetryableFailure(reason, State(coins: 2, pending: 6));
            Assert.That(failure.Status, Is.EqualTo(ProgressionStatus.RetryableFailure));
            Assert.That(failure.Failure, Is.EqualTo(reason));
            Assert.Throws<InvalidOperationException>(() => failure.AsReplay(State()));
        }

        [Test]
        public void SaveErrorAndSuccessfulRetryDistinguishBothCrystalBalances()
        {
            var failure = ProgressionResult.RetryableFailure(ProgressionFailure.SaveFailed,
                State(coins: 2, pending: 6, confirmed: 120));
            var retry = ProgressionResult.Applied(State(coins: 2, pending: 0, confirmed: 129));

            Assert.That(failure.Snapshot.Run.Coins, Is.EqualTo(2));
            Assert.That(failure.Snapshot.Run.PendingCrystals, Is.EqualTo(6));
            Assert.That(failure.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(120));
            Assert.That(retry.Snapshot.Run.PendingCrystals, Is.Zero);
            Assert.That(retry.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(129));
        }

        [Test]
        public void LootSnapshotsRetainStableIdAndPreviousRemainder()
        {
            const string pickupId = "combat_a/shooter-2/ammo";
            const string spawnId = "combat_a/shooter-2";
            var drops = new List<ProgressionLootSnapshot>
            {
                new ProgressionLootSnapshot(pickupId, Room, spawnId, ProgressionLootKind.Ammo, 8)
            };
            var before = new ProgressionRunSnapshot(RunId, 0, 0, drops);
            drops[0] = new ProgressionLootSnapshot(pickupId, Room, spawnId, ProgressionLootKind.Ammo, 5);
            var after = new ProgressionRunSnapshot(RunId, 0, 0, drops);
            drops.Clear();

            Assert.That(before.Loot[0].Remaining, Is.EqualTo(8));
            Assert.That(after.Loot[0].Remaining, Is.EqualTo(5));
            Assert.That(after.Loot[0].PickupId, Is.EqualTo(before.Loot[0].PickupId));
            Assert.That(after.Loot[0].Checkpoint.Index, Is.EqualTo(0));
            Assert.That(after.Loot[0].SpawnId, Is.EqualTo(spawnId));
        }

        [Test]
        public void WorkingBrokerAndTableBottleHaveDistinctState()
        {
            var broker = new ProgressionNpcSnapshot(Alley, "broker", ProgressionNpcKind.WeaponDealer,
                "upgrade", 3, ProgressionNpcState.Working, readyAtGameTime: 14, weaponInstanceId: "pistol-01");
            var bottle = new ProgressionNpcSnapshot(Door, "dealer", ProgressionNpcKind.BottleDealer,
                "buy-bottle", 4, ProgressionNpcState.ItemAvailable, itemId: "bottle-01",
                itemDefinitionId: "dealer-bottle");
            var state = State(npcs: new[] { broker, bottle });

            Assert.That(state.Run.Npcs[0].Point.PointId, Is.EqualTo("1-1/a-to-b"));
            Assert.That(state.Run.Npcs[0].ReadyAtGameTime, Is.EqualTo(14d));
            Assert.That(state.Run.Npcs[0].ItemId, Is.Null);
            Assert.That(state.Run.Npcs[0].WeaponInstanceId, Is.EqualTo("pistol-01"));
            Assert.That(state.Run.Npcs[1].Point.EntranceId, Is.EqualTo("house-door"));
            Assert.That(state.Run.Npcs[1].ItemId, Is.EqualTo("bottle-01"));
            Assert.That(state.Run.ActiveBottleEffect, Is.Null);
        }

        [Test]
        public void HomeSnapshotKeepsSummaryWithoutAnActiveRun()
        {
            var profile = new ProgressionProfileSnapshot(129, ProgressionProfileState.RecoveredFromBackup);
            var snapshot = new ProgressionSnapshot(profile,
                lastRun: new ProgressionRunSummary(RunId, RunOutcome.Defeat, 2, 9));

            Assert.That(snapshot.Run, Is.Null);
            Assert.That(snapshot.LastRun.Value.FinalCoins, Is.EqualTo(2));
            Assert.That(snapshot.LastRun.Value.ConfirmedCrystalsEarned, Is.EqualTo(9));
            Assert.That(snapshot.Profile.ConfirmedCrystals, Is.EqualTo(129));
            Assert.That(snapshot.Profile.State, Is.EqualTo(ProgressionProfileState.RecoveredFromBackup));
        }

        private static ProgressionSnapshot State(int coins = 0, int pending = 0, int confirmed = 120,
            IEnumerable<ProgressionNpcSnapshot> npcs = null) =>
            new ProgressionSnapshot(new ProgressionProfileSnapshot(confirmed, ProgressionProfileState.Ready),
                new ProgressionRunSnapshot(RunId, coins, pending, npcs: npcs));
    }
}
