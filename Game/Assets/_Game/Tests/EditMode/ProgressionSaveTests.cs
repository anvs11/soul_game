using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Core;
using Game.Encounters;
using Game.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ProgressionSaveTests
    {
        private const string RunId = "test-city";
        private static readonly ProgressionCheckpoint First = new("1-1", "combat_a", 0);
        private static readonly ProgressionCheckpoint Second = new("1-1", "combat_b", 1);
        private string folder;

        [SetUp]
        public void IsolateFiles()
        {
            folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "TestResults", "Saves",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TestCase(SaveWriteMode.PreferAtomicReplacement)]
        [TestCase(SaveWriteMode.DualSlot)]
        public void LatestRevisionAndValidReserveSurviveCorruption(SaveWriteMode mode)
        {
            var repository = new JsonSaveRepository(folder, mode);
            Assert.That(repository.Load().Status, Is.EqualTo(SaveLoadStatus.Missing));
            Store(repository, 0, 10);
            Store(repository, 1, 15);
            Store(repository, 2, 24);
            var latest = repository.Load();
            Assert.That(latest.Status, Is.EqualTo(SaveLoadStatus.Ready));
            Assert.That(latest.Profile.revision, Is.EqualTo(3));
            Assert.That(latest.Profile.confirmedCrystals, Is.EqualTo(24));

            var slots = new[] { Path.Combine(folder, "profile-a.json"), Path.Combine(folder, "profile-b.json") };
            string newest = slots.Single(path => JsonUtility.FromJson<ProfileData>(File.ReadAllText(path)).revision == 3);
            File.WriteAllText(newest, "{\"schemaVersion\":1,\"revision\":3}");
            var recovered = repository.Load();
            Assert.That(recovered.Status, Is.EqualTo(SaveLoadStatus.Recovered));
            Assert.That(recovered.Profile.revision, Is.EqualTo(2));
            Assert.That(recovered.Profile.confirmedCrystals, Is.EqualTo(15));
            Assert.That(new CityProgressionSession(() => 0, repository).Snapshot.Profile.State,
                Is.EqualTo(ProgressionProfileState.RecoveredFromBackup));

            Store(repository, 2, 18);
            Assert.That(repository.Load().Profile.confirmedCrystals, Is.EqualTo(18));
            foreach (string path in slots) File.WriteAllText(path, "{}");
            Assert.That(repository.Load().Status, Is.EqualTo(SaveLoadStatus.Corrupt));
            Assert.That(repository.TryCommit(0, new ProfileData { revision = 1 }),
                Is.EqualTo(SaveWriteStatus.Unavailable));
        }

        [Test]
        public void InterruptedStagingDocumentNeverBecomesVisible()
        {
            var repository = new JsonSaveRepository(folder, SaveWriteMode.DualSlot);
            Store(repository, 0, 10);

            var other = Path.Combine(folder, "prepared-fixture");
            var writer = new JsonSaveRepository(other);
            Store(writer, 0, 999);
            Store(writer, 1, 1000);
            string prepared = Directory.GetFiles(other, "profile-?.json").Single(path =>
                JsonUtility.FromJson<ProfileData>(File.ReadAllText(path)).revision == 2);
            string pending = Path.Combine(folder, "profile.pending.json");
            File.Copy(prepared, pending);
            Assert.That(repository.Load().Profile.confirmedCrystals, Is.EqualTo(10));
            File.WriteAllText(pending, "{");
            Assert.That(repository.Load().Profile.confirmedCrystals, Is.EqualTo(10));
            Store(repository, 1, 16);
            Assert.That(repository.Load().Profile.confirmedCrystals, Is.EqualTo(16));
        }

        [Test]
        public void FutureVersionBlocksWritesAndSeparateDirectoryStartsFresh()
        {
            var repository = new JsonSaveRepository(folder);
            Store(repository, 0, 12);
            string future = Path.Combine(folder, "profile-b.json");
            const string unknownVersion = "{\"schemaVersion\":99,\"revision\":2,\"newCurrency\":500}";
            File.WriteAllText(future, unknownVersion);
            Assert.That(repository.Load().Status, Is.EqualTo(SaveLoadStatus.IncompatibleSchema));
            var session = new CityProgressionSession(() => 0, repository);
            Assert.That(session.Snapshot.Profile.State, Is.EqualTo(ProgressionProfileState.IncompatibleSchema));
            Assert.That(session.StartRun(Key("start"), 42, Array.Empty<ProgressionTransitionPoint>()).Failure,
                Is.EqualTo(ProgressionFailure.ProfileIncompatible));
            Assert.That(repository.TryCommit(1, new ProfileData { revision = 2 }),
                Is.EqualTo(SaveWriteStatus.IncompatibleSchema));
            Assert.That(File.ReadAllText(future), Is.EqualTo(unknownVersion));

            var separate = new JsonSaveRepository(Path.Combine(folder, "separate-profile"));
            Store(separate, 0, 2);
            Assert.That(separate.Load().Profile.confirmedCrystals, Is.EqualTo(2));
        }

        [Test]
        public void FailedCheckpointKeepsWalletAndPendingUntilSameActionSucceeds()
        {
            var repository = new JsonSaveRepository(folder);
            var session = Start(repository);
            int crystals = GrantPendingCrystals(session, First, "first");
            string pendingPath = Path.Combine(folder, "profile.pending.json");
            Directory.CreateDirectory(pendingPath);

            var failed = session.ClearCheckpoint(Key("clear"), First);
            Assert.That(failed.Status, Is.EqualTo(ProgressionStatus.RetryableFailure));
            Assert.That(failed.Failure, Is.EqualTo(ProgressionFailure.SaveFailed));
            Assert.That(failed.Snapshot.Run.PendingCrystals, Is.EqualTo(crystals));
            Assert.That(failed.Snapshot.Profile.ConfirmedCrystals, Is.Zero);
            Assert.That(repository.Load().Profile.checkpoints, Is.Empty);

            Directory.Move(pendingPath, Path.Combine(folder, "former-write-obstruction"));
            AssertApplied(session.ClearCheckpoint(Key("clear"), First));
            var saved = repository.Load().Profile;
            Assert.That(saved.confirmedCrystals, Is.EqualTo(crystals));
            Assert.That(saved.checkpoints.Single().runId, Is.EqualTo(RunId));
            Assert.That(saved.checkpoints.Single().checkpointIndex, Is.EqualTo(First.Index));
            Assert.That(saved.checkpoints.Single().crystals, Is.EqualTo(crystals));
            Assert.That(session.Snapshot.Run.PendingCrystals, Is.Zero);
            Assert.That(session.ClearCheckpoint(Key("new-callback"), First).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(repository.Load().Profile.revision, Is.EqualTo(saved.revision));
        }

        [TestCase(RunOutcome.Defeat)]
        [TestCase(RunOutcome.Abandoned)]
        public void FinishAndRelaunchKeepOnlyConfirmedCrystals(RunOutcome outcome)
        {
            var repository = new JsonSaveRepository(folder, SaveWriteMode.DualSlot);
            var session = Start(repository);
            int confirmed = GrantPendingCrystals(session, First, "first");
            AssertApplied(session.ClearCheckpoint(Key("clear"), First));
            GrantPendingCrystals(session, Second, "second");
            AssertApplied(session.FinishRun(Key("finish"), outcome));
            var reopened = new CityProgressionSession(() => 0, repository);
            Assert.That(reopened.Snapshot.Run, Is.Null);
            Assert.That(reopened.Snapshot.LastRun.Value.Outcome, Is.EqualTo(outcome));
            Assert.That(reopened.Snapshot.LastRun.Value.FinalCoins, Is.Zero);
            Assert.That(reopened.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(confirmed));
            Assert.That(reopened.ClearCheckpoint(Key("replayed"), First).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(reopened.ClearCheckpoint(Key("wrong-room"), new ProgressionCheckpoint("1-1", "different", 0)).Failure,
                Is.EqualTo(ProgressionFailure.Conflict));
            Assert.That(reopened.ClearCheckpoint(Key("never-saved"), Second).Failure, Is.EqualTo(ProgressionFailure.WrongPhase));
        }

        [Test]
        public void InterruptedRunIsAbandonedOnceAndItsPersistentReceiptReplays()
        {
            var repository = new JsonSaveRepository(folder);
            var session = Start(repository);
            int confirmed = GrantPendingCrystals(session, First, "first");
            AssertApplied(session.ClearCheckpoint(Key("clear"), First));
            GrantPendingCrystals(session, Second, "unsettled");

            var reopened = new CityProgressionSession(() => 0, repository);
            Assert.That(reopened.Snapshot.Run, Is.Null);
            Assert.That(reopened.Snapshot.LastRun.Value.Outcome, Is.EqualTo(RunOutcome.Abandoned));
            Assert.That(reopened.Snapshot.LastRun.Value.ConfirmedCrystalsEarned, Is.EqualTo(confirmed));
            Assert.That(reopened.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(confirmed));
            long revision = reopened.Snapshot.Profile.Revision;
            Assert.That(reopened.ClearCheckpoint(Key("replay-after-restart"), First).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(repository.Load().Profile.activeRun.Exists, Is.False);
            Assert.That(new CityProgressionSession(() => 0, repository).Snapshot.Profile.Revision, Is.EqualTo(revision));
            Assert.That(reopened.StartRun(Key("reuse-run"), 42, Array.Empty<ProgressionTransitionPoint>()).Failure,
                Is.EqualTo(ProgressionFailure.WrongPhase));
        }

        [Test]
        public void HomePurchaseReplaysAcrossSessionsAndProvidesNextStartingWeapon()
        {
            var repository = new JsonSaveRepository(folder);
            Store(repository, 0, 25);
            var offer = new ProgressionHomeOffer("new-pistol", "pistol-alt", 18);
            var session = new CityProgressionSession(() => 0, repository, offer);
            var key = new ProgressionCommandKey(null, "purchase");
            Assert.That(session.Snapshot.Profile.HomeOffers.Single().PriceCrystals, Is.EqualTo(18));
            AssertApplied(session.BuyHomeUnlock(key, offer.OfferId));
            Assert.That(session.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(7));

            var reopened = new CityProgressionSession(() => 0, repository, offer);
            Assert.That(reopened.BuyHomeUnlock(key, offer.OfferId).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(reopened.BuyHomeUnlock(key, "different").Failure, Is.EqualTo(ProgressionFailure.Conflict));
            Assert.That(reopened.Snapshot.Profile.UnlockedIds, Is.EqualTo(new[] { "pistol-alt" }));
            Assert.That(reopened.Snapshot.Profile.SelectedWeaponId, Is.EqualTo("pistol-alt"));
            AssertApplied(reopened.StartRun(Key("start"), 42, Array.Empty<ProgressionTransitionPoint>()));
            Assert.That(reopened.Snapshot.Run.StartingWeaponId, Is.EqualTo("pistol-alt"));
            Assert.That(reopened.Snapshot.Run.Coins, Is.Zero);
            Assert.That(ProfileData.FutureRevivalPriceCrystals, Is.EqualTo(500));
        }

        [Test]
        public void FailedHomePurchaseAndStaleSessionRetryDoNotChargeTwice()
        {
            var repository = new JsonSaveRepository(folder);
            Store(repository, 0, 30);
            var first = new CityProgressionSession(() => 0, repository);
            var stale = new CityProgressionSession(() => 0, new JsonSaveRepository(folder));
            string offer = first.Snapshot.Profile.HomeOffers.Single().OfferId;
            var key = new ProgressionCommandKey(null, "same-purchase");
            string obstruction = Path.Combine(folder, "profile.pending.json");
            Directory.CreateDirectory(obstruction);
            var failed = first.BuyHomeUnlock(key, offer);
            Assert.That(failed.Status, Is.EqualTo(ProgressionStatus.RetryableFailure));
            Assert.That(failed.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(30));
            Assert.That(failed.Snapshot.Profile.UnlockedIds, Is.Empty);

            Directory.Move(obstruction, Path.Combine(folder, "moved-obstruction"));
            AssertApplied(first.BuyHomeUnlock(key, offer));
            Assert.That(stale.BuyHomeUnlock(key, offer).Status, Is.EqualTo(ProgressionStatus.RetryableFailure));
            Assert.That(stale.BuyHomeUnlock(key, offer).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(repository.Load().Profile.confirmedCrystals, Is.EqualTo(10));
            Assert.That(repository.Load().Profile.homePurchases.Length, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentCheckpointCommandsCreditOnce()
        {
            var repository = new JsonSaveRepository(folder, SaveWriteMode.DualSlot);
            var session = Start(repository);
            int crystals = GrantPendingCrystals(session, First, "first");
            var a = Task.Run(() => session.ClearCheckpoint(Key("clear-a"), First).Status);
            var b = Task.Run(() => session.ClearCheckpoint(Key("clear-b"), First).Status);
            Task.WaitAll(a, b);
            CollectionAssert.AreEquivalent(new[] { ProgressionStatus.Applied, ProgressionStatus.Replayed },
                new[] { a.Result, b.Result });
            Assert.That(repository.Load().Profile.confirmedCrystals, Is.EqualTo(crystals));
            Assert.That(repository.Load().Profile.checkpoints.Length, Is.EqualTo(1));
        }

        [Test]
        public void CompetingRepositoryInstancesCannotOverwriteSameRevision()
        {
            var first = new JsonSaveRepository(folder);
            var second = new JsonSaveRepository(folder);
            Store(first, 0, 20);
            var a = Task.Run(() => first.TryCommit(1, new ProfileData { revision = 2, confirmedCrystals = 29 }));
            var b = Task.Run(() => second.TryCommit(1, new ProfileData { revision = 2, confirmedCrystals = 17 }));
            Task.WaitAll(a, b);
            CollectionAssert.AreEquivalent(new[] { SaveWriteStatus.Saved, SaveWriteStatus.RevisionConflict },
                new[] { a.Result, b.Result });
            Assert.That(first.Load().Profile.confirmedCrystals, Is.EqualTo(a.Result == SaveWriteStatus.Saved ? 29 : 17));
        }

        [Test]
        public void FinalStageVictoryPersistsCityCompletion()
        {
            var repository = new JsonSaveRepository(folder);
            var session = Start(repository);
            for (int i = 2; i <= 5; i++)
                AssertApplied(session.EnterStage(Key("enter-" + i), "1-" + i));
            Assert.That(repository.Load().Profile.activeRun.stageId, Is.EqualTo("1-5"));
            AssertApplied(session.FinishRun(Key("victory"), RunOutcome.Victory));
            Assert.That(new CityProgressionSession(() => 0, repository).Snapshot.Profile.CompletedRegionIds,
                Is.EqualTo(new[] { "city" }));
        }

        private static CityProgressionSession Start(ISaveRepository repository)
        {
            var session = new CityProgressionSession(() => 0, repository);
            AssertApplied(session.StartRun(Key("start"), 42, Array.Empty<ProgressionTransitionPoint>()));
            return session;
        }

        private static int GrantPendingCrystals(CityProgressionSession session, ProgressionCheckpoint room, string spawn)
        {
            AssertApplied(session.ConfirmEnemyDeath(Key("death:" + spawn), room, spawn, EnemyTypes.Melee));
            string pickup = spawn + "/crystals";
            int amount = session.Snapshot.Run.Loot.Single(drop => drop.PickupId == pickup).Remaining;
            AssertApplied(session.CollectLoot(Key("pick:" + spawn), pickup));
            return amount;
        }

        private static ProgressionCommandKey Key(string action) => new(RunId, action);

        private static void Store(ISaveRepository repository, long previous, int crystals) =>
            Assert.That(repository.TryCommit(previous, new ProfileData
            {
                revision = previous + 1,
                confirmedCrystals = crystals
            }), Is.EqualTo(SaveWriteStatus.Saved));

        private static void AssertApplied(ProgressionResult result) =>
            Assert.That(result.Status, Is.EqualTo(ProgressionStatus.Applied), result.Failure.ToString());
    }
}
