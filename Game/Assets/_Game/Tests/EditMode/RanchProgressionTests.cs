using System;
using System.IO;
using System.Linq;
using Game.Contracts;
using Game.Core;
using Game.Encounters;
using Game.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class RanchProgressionTests
    {
        private static readonly ProgressionCheckpoint Yard = new(RanchProgression.SegmentId, "yard", 0);
        private static readonly ProgressionCheckpoint Pen = new(RanchProgression.SegmentId, "pen", 1);
        private static readonly ProgressionCheckpoint Barn = new(RanchProgression.SegmentId, "barn", 2);
        private string folder;
        private JsonSaveRepository repository;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Application.dataPath, "..", "TestResults", "Saves", "ranch-" + Guid.NewGuid().ToString("N"));
            repository = new JsonSaveRepository(folder);
        }

        [Test]
        public void LegacyCityProfileOpensRanchWithoutSewerAndVictoryIsRecordedOnce()
        {
            var city = new CityProgressionSession(() => 0, repository);
            const string cityId = "old-city";
            Applied(city.StartRun(new ProgressionCommandKey(cityId, "start"), 1, Array.Empty<ProgressionTransitionPoint>()));
            for (int index = 2; index <= 5; index++)
                Applied(city.EnterStage(new ProgressionCommandKey(cityId, "enter-" + index), "1-" + index));
            Applied(city.FinishRun(new ProgressionCommandKey(cityId, "finish"), RunOutcome.Victory));
            Assert.That(repository.Load().Profile.schemaVersion, Is.EqualTo(1));
            Assert.That(repository.Load().Profile.unlockedIds, Is.Empty);

            var ranch = Open("ranch-first");
            Assert.That(ranch.Snapshot.Run.StageId, Is.EqualTo("ranch/5-1"));
            Assert.That(ranch.EnterStage(Key("city-stage"), "1-2").Failure, Is.EqualTo(ProgressionFailure.WrongPhase));
            Assert.That(ranch.ClearCheckpoint(Key("wrong-order"), Barn).Failure, Is.EqualTo(ProgressionFailure.WrongPhase));
            Assert.That(ranch.FinishRun(Key("early"), RunOutcome.Victory).Failure, Is.EqualTo(ProgressionFailure.WrongPhase));
            Applied(ranch.ClearCheckpoint(Key("yard"), Yard));
            Applied(ranch.ClearCheckpoint(Key("pen"), Pen));
            int crystals = Death(ranch, Barn, "deer", EnemyTypes.RanchDeer);
            Applied(ranch.ClearCheckpoint(Key("barn"), Barn));
            Applied(ranch.FinishRun(Key("finish"), RunOutcome.Victory));
            var saved = repository.Load().Profile;
            Assert.That(saved.completedRegionIds, Is.EqualTo(new[] { "city", "ranch" }));
            Assert.That(saved.confirmedCrystals, Is.EqualTo(crystals));
            Assert.That(saved.finishedRuns.Count(entry => entry.runId == "ranch-first"), Is.EqualTo(1));

            var restart = new CityProgressionSession(() => 0, repository);
            Assert.That(restart.FinishRun(Key("replayed-finish"), RunOutcome.Victory).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(restart.ClearCheckpoint(Key("replayed-barn"), Barn).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(repository.Load().Profile.revision, Is.EqualTo(saved.revision));
        }

        [Test]
        public void DeathBeforeAndAfterCheckpointPreservesOnlyConfirmedCrystals()
        {
            SeedProfile("city");
            var session = Open("ranch-first");
            int first = Death(session, Yard, "first", EnemyTypes.Melee);
            Applied(session.CollectLoot(Key("pending"), "first/crystals"));
            Assert.That(session.Snapshot.Run.PendingCrystals, Is.EqualTo(first));
            Applied(session.FinishRun(Key("finish"), RunOutcome.Defeat));
            Assert.That(repository.Load().Profile.confirmedCrystals, Is.EqualTo(10));

            session = Open("ranch-second");
            int settled = Death(session, Yard, "settled", EnemyTypes.Melee, "ranch-second");
            Applied(session.ClearCheckpoint(Key("clear", "ranch-second"), Yard));
            Death(session, Pen, "unsettled", EnemyTypes.Melee, "ranch-second");
            Applied(session.FinishRun(Key("finish", "ranch-second"), RunOutcome.Defeat));
            var after = repository.Load().Profile;
            Assert.That(after.confirmedCrystals, Is.EqualTo(10 + settled));
            Assert.That(after.completedRegionIds, Is.EqualTo(new[] { "city" }));
        }

        [Test]
        public void SaveFailureRetriesSameRanchCheckpointAndVictory()
        {
            SeedProfile("city");
            var session = Open("ranch-first");
            int crystals = Death(session, Yard, "first", EnemyTypes.Melee);
            string obstacle = Path.Combine(folder, "profile.pending.json");
            Block(obstacle, "before-checkpoint");
            var clear = Key("clear");
            Assert.That(session.ClearCheckpoint(clear, Yard).Status, Is.EqualTo(ProgressionStatus.RetryableFailure));
            Assert.That(repository.Load().Profile.confirmedCrystals, Is.EqualTo(10));
            Directory.Move(obstacle, obstacle + "-former-checkpoint");
            Applied(session.ClearCheckpoint(clear, Yard));
            Assert.That(repository.Load().Profile.confirmedCrystals, Is.EqualTo(10 + crystals));
            Applied(session.ClearCheckpoint(Key("pen"), Pen));
            Applied(session.ClearCheckpoint(Key("barn"), Barn));

            Block(obstacle, "before-finish");
            var finish = Key("finish");
            Assert.That(session.FinishRun(finish, RunOutcome.Victory).Status, Is.EqualTo(ProgressionStatus.RetryableFailure));
            Assert.That(repository.Load().Profile.completedRegionIds, Is.EqualTo(new[] { "city" }));
            Directory.Move(obstacle, obstacle + "-former-finish");
            Applied(session.FinishRun(finish, RunOutcome.Victory));
            Assert.That(session.FinishRun(finish, RunOutcome.Victory).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(repository.Load().Profile.completedRegionIds, Is.EqualTo(new[] { "city", "ranch" }));
        }

        [Test]
        public void NoCityMeansLockedAndInterruptedRanchReturnsWithoutVictory()
        {
            var session = new CityProgressionSession(() => 0, repository);
            Assert.That(session.StartRanch(Key("locked"), 42).Failure, Is.EqualTo(ProgressionFailure.RegionLocked));
            SeedProfile("city");
            session = Open("ranch-first");
            int crystals = Death(session, Yard, "first", EnemyTypes.Melee);
            Applied(session.ClearCheckpoint(Key("clear"), Yard));
            Assert.That(repository.Load().Profile.activeRun.stageId, Is.EqualTo("ranch/5-1"));
            var restarted = new CityProgressionSession(() => 0, repository);
            Assert.That(restarted.Snapshot.LastRun.Value.Outcome, Is.EqualTo(RunOutcome.Abandoned));
            Assert.That(restarted.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(10 + crystals));
            Assert.That(restarted.Snapshot.Profile.CompletedRegionIds, Is.EqualTo(new[] { "city" }));
        }

        private void SeedProfile(params string[] regions) => Assert.That(repository.TryCommit(0,
            new ProfileData { revision = 1, confirmedCrystals = 10, completedRegionIds = regions }), Is.EqualTo(SaveWriteStatus.Saved));

        private CityProgressionSession Open(string runId)
        {
            var session = new CityProgressionSession(() => 0, repository);
            Applied(session.StartRanch(Key("start", runId), 42));
            return session;
        }

        private static int Death(CityProgressionSession session, ProgressionCheckpoint checkpoint, string spawn,
            string type, string id = "ranch-first")
        {
            Applied(session.ConfirmEnemyDeath(Key("death/" + spawn, id), checkpoint, spawn, type));
            return session.Snapshot.Run.Loot.Single(drop => drop.PickupId == spawn + "/crystals").Remaining;
        }

        private static void Block(string path, string hint)
        {
            // A directory at the staging path injects an I/O failure even with elevated test permissions.
            if (File.Exists(path)) File.Move(path, path + "-staged-" + hint);
            Directory.CreateDirectory(path);
        }

        private static ProgressionCommandKey Key(string operation, string runId = "ranch-first") => new(runId, operation);

        private static void Applied(ProgressionResult result) =>
            Assert.That(result.Status, Is.EqualTo(ProgressionStatus.Applied), result.Failure.ToString());
    }
}
