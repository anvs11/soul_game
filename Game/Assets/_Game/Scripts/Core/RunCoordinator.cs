using System;
using Game.Contracts;
using Game.Street;

namespace Game.Core
{
    public sealed class RunCoordinator : IDisposable
    {
        private readonly ISceneFlow sceneFlow;
        private RunOutcome? pendingOutcome;
        private bool disposed;
        private Func<CombatSnapshot> pendingAdvance;
        public StreetRoute StreetRoute { get; private set; }
        public int SegmentIndex { get; private set; }
        private StreetRoute requestedStreetRoute;
        public string ActiveArena { get; private set; }
        public CombatSnapshot? PlayerTransfer { get; private set; }

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

        public void StartStreetRun(int seed, StreetRoute route)
        {
            if (disposed || Phase != RunPhase.Home) return;
            requestedStreetRoute = route ?? throw new ArgumentNullException(nameof(route));
            try { StartRun(RunRequest.StreetRun(seed)); }
            finally { requestedStreetRoute = null; }
        }
        public void StartRun(RunRequest request)
        {
            if (disposed || Phase != RunPhase.Home) return;
            if (request.ArenaId != ScenePaths.CombatSandbox && request.ArenaId != ScenePaths.Stage2CombatSandbox && !request.IsStreetRun)
                throw new ArgumentException("Unknown arena profile.", nameof(request));

            if (request.IsStage2Route && request.ArenaId != ScenePaths.Stage2CombatSandbox)
                throw new ArgumentException("Stage 2 route must start in its ordinary arena.", nameof(request));
            StreetRoute = request.IsStreetRun ? requestedStreetRoute ?? Game.Street.StreetRoute.Fixed() : null;
            SegmentIndex = request.IsStreetRun ? 1 : 0;
            CurrentRequest = request;
            ActiveArena = request.ArenaId;
            PlayerTransfer = null;
            pendingAdvance = null;
            RunId = Guid.NewGuid().ToString("N");
            LastOutcome = null;
            pendingOutcome = null;
            Load(request.ArenaId, RunPhase.LoadingArena, RunPhase.InArena);
        }

        public void FinishRun(RunOutcome outcome)
        {
            if (disposed || Phase != RunPhase.InArena) return;
            if (!Enum.IsDefined(typeof(RunOutcome), outcome))
                throw new ArgumentOutOfRangeException(nameof(outcome));
            if (!pendingOutcome.HasValue || Priority(outcome) > Priority(pendingOutcome.Value))
                pendingOutcome = outcome;
        }

        public void CompleteArena(string arena, Func<CombatSnapshot> readState, int segmentIndex = 0)
        {
            if (disposed || Phase != RunPhase.InArena || ActiveArena != arena || readState == null) return;
            bool streetAdvance = CurrentRequest.IsStreetRun && arena == ScenePaths.Stage3StreetSandbox &&
                segmentIndex == SegmentIndex && SegmentIndex < 5;
            bool stage2Advance = CurrentRequest.IsStage2Route && arena == ScenePaths.Stage2CombatSandbox;
            if (!streetAdvance && !stage2Advance) return;
            if (pendingAdvance == null) pendingAdvance = readState;
        }

        // The Unity owner calls this once in LateUpdate, after gameplay requests.
        public void ResolvePendingFinish()
        {
            if (disposed || Phase != RunPhase.InArena) return;
            if (pendingOutcome.HasValue)
            {
                LastOutcome = pendingOutcome;
                pendingOutcome = null;
                pendingAdvance = null;
                PlayerTransfer = null;
                Load(ScenePaths.Home, RunPhase.ReturningHome, RunPhase.Home);
            }
            else if (pendingAdvance != null)
            {
                var state = pendingAdvance();
                pendingAdvance = null;
                if (state.IsDead || state.Health <= 0)
                {
                    FinishRun(RunOutcome.Defeat);
                    ResolvePendingFinish();
                    return;
                }
                PlayerTransfer = state;
                if (CurrentRequest.IsStreetRun)
                {
                    SegmentIndex++;
                    ActiveArena = ScenePaths.Stage3StreetSandbox;
                }
                else ActiveArena = ScenePaths.Stage2BossSandbox;
                Load(ActiveArena, RunPhase.LoadingArena, RunPhase.InArena);
            }
        }

        public void RetryHome()
        {
            if (disposed || Phase != RunPhase.Faulted) return;
            pendingOutcome = null;
            pendingAdvance = null;
            PlayerTransfer = null;
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
            pendingAdvance = null;
            PlayerTransfer = null;
            pendingOutcome = null;
            Changed = null;
        }
    }
}
