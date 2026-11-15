using Game.Core;
using NUnit.Framework;
namespace Game.Tests.Flow
{
    public sealed class Stage2ProfileTests
    {
        private sealed class ImmediateFlow : ISceneFlow
        {
            public string LastScene;
            public int Loads;
            public bool TryLoad(string name,System.Action<bool,string> complete)
            { LastScene=name;Loads++;complete(true,null);return true; }
        }
        [Test] public void DiagnosticProfileUsesSameCoordinatorAndStillPrefersDefeat()
        {
            var flow=new ImmediateFlow();
            using(var run=new RunCoordinator(flow))
            {
                run.Begin();run.StartRun(RunRequest.Stage2Diagnostic(7));
                Assert.That(flow.LastScene,Is.EqualTo(ScenePaths.Stage2CombatSandbox));
                Assert.That(run.CurrentRequest.Seed,Is.EqualTo(7));
                Assert.That(run.CurrentRequest.IsStage2Diagnostic,Is.True);
                run.FinishRun(RunOutcome.Victory);run.FinishRun(RunOutcome.Defeat);run.ResolvePendingFinish();
                Assert.That(run.LastOutcome,Is.EqualTo(RunOutcome.Defeat));
                Assert.That(flow.LastScene,Is.EqualTo(ScenePaths.Home));Assert.That(flow.Loads,Is.EqualTo(3));
                run.StartRun(RunRequest.Sandbox());
                Assert.That(flow.LastScene,Is.EqualTo(ScenePaths.CombatSandbox));
                Assert.That(run.CurrentRequest.IsStage2Diagnostic,Is.False);
            }
        }
    }
}
