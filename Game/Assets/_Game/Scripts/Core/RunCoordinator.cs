using System;

namespace Game.Core
{
    public sealed class RunCoordinator : IDisposable
    {
        private readonly ISceneFlow sceneFlow;
        private RunOutcome? pendingOutcome;
        private bool disposed;

        public RunPhase Phase { get; private set; } = RunPhase.Booting;
        public string RunId { get; private set; }
        public RunRequest CurrentRequest { get; private set; }
        public RunOutcome? LastOutcome { get; private set; }
        public string Error { get; private set; } = "";
        public event Action Changed;

        public RunCoordinator(ISceneFlow sceneFlow)
        {
            this.sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        }

        public void Begin()
        {
            if (disposed || Phase != RunPhase.Booting) return;
            Load(ScenePaths.Home, RunPhase.ReturningHome, RunPhase.Home);
        }

        public void StartRun(RunRequest request)
        {
            if (disposed || Phase != RunPhase.Home) return;
            if (request.ArenaId != ScenePaths.CombatSandbox)
                throw new ArgumentException("Only CombatSandbox is available.", nameof(request));

            CurrentRequest = request;
            RunId = Guid.NewGuid().ToString("N");
            LastOutcome = null;
            pendingOutcome = null;
            Load(ScenePaths.CombatSandbox, RunPhase.LoadingArena, RunPhase.InArena);
        }

        public void FinishRun(RunOutcome outcome)
        {
            if (disposed || Phase != RunPhase.InArena) return;
            if (!Enum.IsDefined(typeof(RunOutcome), outcome))
                throw new ArgumentOutOfRangeException(nameof(outcome));
            if (!pendingOutcome.HasValue || Priority(outcome) > Priority(pendingOutcome.Value))
                pendingOutcome = outcome;
        }

        // The Unity owner calls this once in LateUpdate, after gameplay requests.
        public void ResolvePendingFinish()
        {
            if (disposed || Phase != RunPhase.InArena || !pendingOutcome.HasValue) return;
            LastOutcome = pendingOutcome;
            pendingOutcome = null;
            Load(ScenePaths.Home, RunPhase.ReturningHome, RunPhase.Home);
        }

        public void RetryHome()
        {
            if (disposed || Phase != RunPhase.Faulted) return;
            pendingOutcome = null;
            Load(ScenePaths.Home, RunPhase.ReturningHome, RunPhase.Home);
        }

        private void Load(string scene, RunPhase loading, RunPhase ready)
        {
            Error = "";
            SetPhase(loading);
            bool started;
            try
            {
                started = sceneFlow.TryLoad(scene, (success, error) =>
                {
                    if (disposed) return;
                    if (success) SetPhase(ready);
                    else Fail(error);
                });
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
                return;
            }

            if (!started) Fail("Scene transition did not start: " + scene);
        }

        private void Fail(string error)
        {
            Error = string.IsNullOrWhiteSpace(error) ? "Scene transition failed." : error;
            SetPhase(RunPhase.Faulted);
        }

        private void SetPhase(RunPhase next)
        {
            Phase = next;
            Changed?.Invoke();
        }

        private static int Priority(RunOutcome outcome) =>
            outcome == RunOutcome.Defeat ? 2 : outcome == RunOutcome.Victory ? 1 : 0;

        public void Dispose()
        {
            disposed = true;
            pendingOutcome = null;
            Changed = null;
        }
    }
}
