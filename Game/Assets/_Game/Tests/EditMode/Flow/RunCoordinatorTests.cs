using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.Flow
{
    public sealed class RunCoordinatorTests
    {
        private sealed class FakeFlow : ISceneFlow
        {
            public readonly List<string> Requests = new List<string>();
            public bool Refuse;
            private Action<bool, string> pending;
            public bool TryLoad(string scene, Action<bool, string> completed)
            {
                Requests.Add(scene);
                if (Refuse) return false;
                Assert.That(pending, Is.Null, "Concurrent scene loads were requested.");
                pending = completed;
                return true;
            }

            public void Complete(bool success = true)
            {
                var callback = pending;
                Assert.That(callback, Is.Not.Null);
                pending = null;
                callback(success, success ? null : "test load failure");
            }
        }

        private FakeFlow flow;
        private RunCoordinator coordinator;

        [SetUp]
        public void SetUp()
        {
            flow = new FakeFlow();
            coordinator = new RunCoordinator(flow);
        }

        [TearDown]
        public void TearDown() => coordinator.Dispose();

        private void Home()
        {
            coordinator.Begin();
            flow.Complete();
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Home));
        }

        private void Arena()
        {
            Home();
            coordinator.StartRun(RunRequest.Sandbox());
            flow.Complete();
        }

        [Test]
        public void StartBeforeHomeAndRepeatedBegin_DoNotCreateExtraLoad()
        {
            coordinator.StartRun(RunRequest.Sandbox());
            Assert.That(flow.Requests, Is.Empty);
            coordinator.Begin();
            coordinator.Begin();
            Assert.That(flow.Requests, Is.EqualTo(new[] { ScenePaths.Home }));
        }

        [Test]
        public void DoubleStart_WhileLoadingArena_RequestsOneTransition()
        {
            Home();
            coordinator.StartRun(RunRequest.Sandbox(17));
            string id = coordinator.RunId;
            coordinator.StartRun(RunRequest.Sandbox(99));
            Assert.That(flow.Requests, Is.EqualTo(new[] { ScenePaths.Home, ScenePaths.CombatSandbox }));
            Assert.That(coordinator.RunId, Is.EqualTo(id));
            Assert.That(coordinator.CurrentRequest.Seed, Is.EqualTo(17));
            flow.Complete();
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.InArena));
        }

        [Test]
        public void SameFrameFinishRequests_PreferDefeat_AndReturnOnce()
        {
            Arena();
            coordinator.FinishRun(RunOutcome.Victory);
            coordinator.FinishRun(RunOutcome.Defeat);
            coordinator.FinishRun(RunOutcome.Abandoned);
            Assert.That(flow.Requests.Count, Is.EqualTo(2), "Finish must wait for end of frame.");
            coordinator.ResolvePendingFinish();
            coordinator.ResolvePendingFinish();
            coordinator.FinishRun(RunOutcome.Victory);
            Assert.That(coordinator.LastOutcome, Is.EqualTo(RunOutcome.Defeat));
            Assert.That(flow.Requests.Count, Is.EqualTo(3));
            flow.Complete();
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Home));
        }

        [Test]
        public void TenAttempts_CreateFreshIds_AndReturnHomeEveryTime()
        {
            Home();
            var ids = new HashSet<string>();
            for (int index = 0; index < 10; index++)
            {
                coordinator.StartRun(RunRequest.Sandbox(index));
                Assert.That(ids.Add(coordinator.RunId), Is.True);
                Assert.That(coordinator.LastOutcome, Is.Null);
                flow.Complete();
                coordinator.FinishRun(RunOutcome.Abandoned);
                coordinator.ResolvePendingFinish();
                flow.Complete();
                Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Home));
            }
            Assert.That(flow.Requests.Count, Is.EqualTo(21));
        }

        [Test]
        public void FinishOutsideArena_DoesNotLoadOrPublishAnOutcome()
        {
            Home();
            coordinator.FinishRun(RunOutcome.Defeat);
            coordinator.ResolvePendingFinish();
            Assert.That(flow.Requests.Count, Is.EqualTo(1));
            Assert.That(coordinator.LastOutcome, Is.Null);
        }

        [Test]
        public void FailedLoad_CannotStartAgainUntilHomeIsRecovered()
        {
            Home();
            coordinator.StartRun(RunRequest.Sandbox());
            flow.Complete(false);
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Faulted));
            Assert.That(coordinator.Error, Is.EqualTo("test load failure"));
            coordinator.StartRun(RunRequest.Sandbox());
            Assert.That(flow.Requests.Count, Is.EqualTo(2));
            coordinator.RetryHome();
            coordinator.RetryHome();
            flow.Complete();
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Home));
            Assert.That(coordinator.Error, Is.Empty);
        }

        [Test]
        public void RefusedTransition_IsFaultedInsteadOfStuckLoading()
        {
            flow.Refuse = true;
            coordinator.Begin();
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Faulted));
            Assert.That(coordinator.Error, Does.Contain("did not start"));
        }

        [Test]
        public void Dispose_IgnoresLateCompletionAndUnsubscribesObservers()
        {
            coordinator.Begin();
            int notifications = 0;
            coordinator.Changed += () => notifications++;
            coordinator.Dispose();
            flow.Complete();
            coordinator.StartRun(RunRequest.Sandbox());
            Assert.That(notifications, Is.Zero);
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.ReturningHome));
            Assert.That(flow.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnknownArena_IsRejectedBeforeChangingTheRun()
        {
            Home();
            Assert.Throws<ArgumentException>(() => coordinator.StartRun(new RunRequest("Unknown", 0)));
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Home));
            Assert.That(coordinator.RunId, Is.Null);
            Assert.That(flow.Requests.Count, Is.EqualTo(1));
        }
    }
}
