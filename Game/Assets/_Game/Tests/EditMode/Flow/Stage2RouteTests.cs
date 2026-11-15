using System;
using System.Collections.Generic;
using Game.Contracts;
using Game.Core;
using NUnit.Framework;
namespace Game.Tests.Flow
{
    public sealed class Stage2RouteTests
    {
        private sealed class Flow : ISceneFlow
        {
            public readonly List<string> Scenes = new List<string>();
            public bool FailBoss;
            public bool TryLoad(string name, Action<bool,string> done)
            { Scenes.Add(name); done(!(FailBoss && name==ScenePaths.Stage2BossSandbox), "test boss load failure"); return true; }
        }
        private static CombatSnapshot State(float hp=80) => new CombatSnapshot(hp,100,0,30,60,100,3,8,7,true,false,hp<=0);
        [Test] public void RouteKeepsIdAndResourcesAndCompletesOnlyAfterBoss()
        {
            var flow=new Flow(); using(var run=new RunCoordinator(flow))
            {
                run.Begin();run.StartRun(RunRequest.Stage2(42)); string id=run.RunId;
                float health=80; run.CompleteArena(ScenePaths.Stage2CombatSandbox,()=>State(health));
                run.CompleteArena(ScenePaths.Stage2CombatSandbox,()=>State(100)); health=70;
                Assert.That(flow.Scenes.Count,Is.EqualTo(2)); run.ResolvePendingFinish();
                Assert.That(run.ActiveArena,Is.EqualTo(ScenePaths.Stage2BossSandbox));
                Assert.That(run.PlayerTransfer.Value.Health,Is.EqualTo(70),"Capture after same-frame damage, not at exit request");
                Assert.That(run.RunId,Is.EqualTo(id));Assert.That(run.LastOutcome,Is.Null);
                run.CompleteArena(ScenePaths.Stage2CombatSandbox,()=>State());run.ResolvePendingFinish();
                Assert.That(flow.Scenes.Count,Is.EqualTo(3));
                run.FinishRun(RunOutcome.Victory);run.ResolvePendingFinish();run.ResolvePendingFinish();
                Assert.That(flow.Scenes,Is.EqualTo(new[]{ScenePaths.Home,ScenePaths.Stage2CombatSandbox,ScenePaths.Stage2BossSandbox,ScenePaths.Home}));
                Assert.That(run.LastOutcome,Is.EqualTo(RunOutcome.Victory));Assert.That(run.PlayerTransfer,Is.Null);
            }
        }
        [TestCase(RunOutcome.Defeat)] [TestCase(RunOutcome.Abandoned)]
        public void TerminalRequestCancelsSameFrameIntermediateTransition(RunOutcome outcome)
        {
            var flow=new Flow();using(var run=new RunCoordinator(flow))
            {
                run.Begin();run.StartRun(RunRequest.Stage2());run.CompleteArena(ScenePaths.Stage2CombatSandbox,()=>State());
                run.FinishRun(outcome);run.ResolvePendingFinish();Assert.That(run.LastOutcome,Is.EqualTo(outcome));
                Assert.That(flow.Scenes,Does.Not.Contain(ScenePaths.Stage2BossSandbox));
            }
        }
        [Test] public void DeadSnapshotCannotStartBossEvenWithoutDeathNotification()
        {
            var flow=new Flow();using(var run=new RunCoordinator(flow))
            {run.Begin();run.StartRun(RunRequest.Stage2());run.CompleteArena(ScenePaths.Stage2CombatSandbox,()=>State(0));run.ResolvePendingFinish();Assert.That(run.LastOutcome,Is.EqualTo(RunOutcome.Defeat));Assert.That(flow.Scenes.Count,Is.EqualTo(3));}
        }
        [Test] public void FailedIntermediateLoadRecoversHomeAndNewAttemptIsFresh()
        {
            var flow=new Flow{FailBoss=true};using(var run=new RunCoordinator(flow))
            {
                run.Begin();run.StartRun(RunRequest.Stage2());run.CompleteArena(ScenePaths.Stage2CombatSandbox,()=>State());run.ResolvePendingFinish();
                Assert.That(run.Phase,Is.EqualTo(RunPhase.Faulted));Assert.That(run.LastOutcome,Is.Null);
                run.RetryHome();run.StartRun(RunRequest.Stage2());Assert.That(run.PlayerTransfer,Is.Null);Assert.That(run.ActiveArena,Is.EqualTo(ScenePaths.Stage2CombatSandbox));
            }
        }
        [Test] public void DiagnosticDamageIsOneThirdWithExactRealPhaseThresholds()
        {
            var hp=new Game.Actors.HealthState(600,0);
            float step=Game.Sandbox.HazardBossPreview.BossDiagnosticDamage(hp.MaxHealth);
            Assert.That(step,Is.EqualTo(200));
            hp.ApplyDamage(new Game.Combat.DamagePacket(-1,step));Assert.That(hp.CurrentHealth,Is.EqualTo(400));
            hp.ApplyDamage(new Game.Combat.DamagePacket(-1,step));Assert.That(hp.CurrentHealth,Is.EqualTo(200));
            hp.ApplyDamage(new Game.Combat.DamagePacket(-1,step));Assert.That(hp.IsDead,Is.True);
        }
    }
}
