using System;
using Game.Contracts;
using Game.Street;
using Game.Generation;
using Game.StreetMap;
using Game.Combat;

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
        private GenerationResult requestedGeneration;
        public GenerationResult StreetGeneration { get; private set; }
        public string ActiveArena { get; private set; }
        public CombatSnapshot? PlayerTransfer { get; private set; }
        public CityRegionRoute FullCityRoute { get; private set; }
        public int CitySegmentIndex { get; private set; }
        public string CitySegmentId => CityRegionRoute.IdAt(CitySegmentIndex);
        public bool IsFinalCitySegment => CitySegmentIndex == CityRegionRoute.SegmentCount - 1;
        public bool IsLastCitySegment => CitySegmentIndex == CurrentRequest.LastPreviewSegment;
        public int CityMedkits { get; private set; }
        internal CityAmmoBackpack CityAmmo { get; } = new();
        public CityFirearm CityWeapon { get; internal set; }

        public RunPhase Phase { get; private set; } = RunPhase.Booting;
        public string RunId { get; private set; }
        public RunRequest CurrentRequest { get; private set; }
        public RunOutcome? LastOutcome { get; private set; }
        public string Error { get; private set; } = "";
        public Stage5Runtime Stage5 { get; private set; }
        private Stage5Runtime visualRuntime;
        internal Stage5Runtime CityRuntime => visualRuntime ?? Stage5;
        public bool Stage5FinishBlocked { get; private set; }
        public event Action Changed;

        public RunCoordinator(ISceneFlow sceneFlow, Stage5Runtime stage5 = null)
        {
            this.sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            Stage5 = stage5;
        }

        public void SetStage5Runtime(Stage5Runtime stage5)
        {
            if (Phase != RunPhase.Home) throw new InvalidOperationException("Select the profile from Home.");
            Stage5 = stage5 ?? throw new ArgumentNullException(nameof(stage5));
        }

        public void Begin()
        {
            if (disposed || Phase != RunPhase.Booting) return;
            Load(ScenePaths.Home, RunPhase.ReturningHome, RunPhase.Home);
        }

        public void StartGeneratedStreetRun(GenerationResult result, StageCatalog catalog)
        {
            if (disposed || Phase != RunPhase.Home) return;
            if (result == null || !result.IsSuccess) throw new ArgumentException("A successful generation result is required.");
            var validation = new LayoutValidator().Validate(result.Layout, catalog);
            if (!validation.IsValid) throw new ArgumentException(string.Join(" | ", validation.Reasons));
            requestedGeneration = result;
            try { StartStreetRun(result.Layout.Seed, StreetGenerationAdapter.ToRoute(result.Layout)); }
            finally { requestedGeneration = null; }
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
            if (request.ArenaId != ScenePaths.CombatSandbox && request.ArenaId != ScenePaths.Stage2CombatSandbox && !request.IsStreetRun && !request.IsFixedStreet1_1)
                throw new ArgumentException("Unknown arena profile.", nameof(request));

            if (request.IsStage2Route && request.ArenaId != ScenePaths.Stage2CombatSandbox)
                throw new ArgumentException("Stage 2 route must start in its ordinary arena.", nameof(request));
            string id = null;
            CityRegionRoute cityRoute = null;
            Stage5Runtime previewRuntime = null;
            if (request.IsStage5City)
            {
                if (Stage5 == null || request.FixedEncounter == null) throw new InvalidOperationException("City requires a profile and encounter plan.");
                if (request.IsFullCity) cityRoute = new CityRegionRoute(request.FixedEncounter);
                if (request.VisualPreview != CityVisualPreview.None) previewRuntime = Stage5Runtime.ForVisualPreview();
                string previewStage = request.FirstCitySegment > 0 ? CityRegionRoute.IdAt(request.FirstCitySegment) : null;
                var runtime = previewRuntime ?? Stage5;
                var result = runtime.Start(request.Seed, request.IsFullCity, out id, previewStage);
                for (int index = 1; result.Failure == ProgressionFailure.None && index <= request.FirstCitySegment; ++index)
                    result = runtime.Enter(id, CityRegionRoute.IdAt(index));
                if (result.Failure != ProgressionFailure.None)
                {
                    Error = Stage5Runtime.Describe(result.Failure);
                    Changed?.Invoke();
                    return;
                }
            }
            StreetRoute = request.IsStreetRun ? requestedStreetRoute ?? Game.Street.StreetRoute.Fixed() : null;
            StreetGeneration = request.IsStreetRun ? requestedGeneration : null;
            SegmentIndex = request.IsStreetRun ? 1 : 0;
            CurrentRequest = request;
            visualRuntime = previewRuntime;
            FullCityRoute = cityRoute;
            CitySegmentIndex = request.FirstCitySegment;
            CityMedkits = 0;
            CityAmmo.Clear();
            CityWeapon = CityFirearm.Pistol;
            ActiveArena = request.ArenaId;
            PlayerTransfer = null;
            pendingAdvance = null;
            RunId = id ?? Guid.NewGuid().ToString("N");
            LastOutcome = null;
            pendingOutcome = null;
            Stage5FinishBlocked = false;
            Load(request.ArenaId, RunPhase.LoadingArena, RunPhase.InArena);
        }

        public void FinishRun(RunOutcome outcome)
        {
            if (disposed || Phase != RunPhase.InArena || Stage5FinishBlocked) return;
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

        internal void AdvanceCity(Func<CombatSnapshot> readState, int medkits)
        {
            if (Phase != RunPhase.InArena || FullCityRoute == null || IsLastCitySegment || pendingAdvance != null) return;
            CityMedkits = medkits;
            pendingAdvance = readState;
        }

        // The Unity owner calls this once in LateUpdate, after gameplay requests.
        public void ResolvePendingFinish()
        {
            if (disposed || Phase != RunPhase.InArena) return;
            if (pendingOutcome.HasValue && !Stage5FinishBlocked)
            {
                if (CurrentRequest.IsStage5City && !SaveStage5Finish(pendingOutcome.Value)) return;
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
                if (CurrentRequest.IsFullCity)
                {
                    var entered = CityRuntime.Enter(RunId, CityRegionRoute.IdAt(CitySegmentIndex + 1));
                    if (entered.Failure != ProgressionFailure.None)
                    {
                        Error = Stage5Runtime.Describe(entered.Failure);
                        Changed?.Invoke();
                        return;
                    }
                    CitySegmentIndex++;
                    PlayerTransfer = state;
                    Load(ScenePaths.Stage4StreetMap, RunPhase.LoadingArena, RunPhase.InArena);
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
            if (CurrentRequest.IsStage5City && CityRuntime.Session.Snapshot.Run != null &&
                !SaveStage5Finish(RunOutcome.Abandoned)) return;
            pendingOutcome = null;
            pendingAdvance = null;
            PlayerTransfer = null;
            Load(ScenePaths.Home, RunPhase.ReturningHome, RunPhase.Home);
        }

        public void RetryStage5Finish()
        {
            if (Stage5FinishBlocked && Phase == RunPhase.InArena)
            {
                Stage5FinishBlocked = false;
                ResolvePendingFinish();
            }
        }

        private bool SaveStage5Finish(RunOutcome outcome)
        {
            var result = CityRuntime.Finish(RunId, outcome);
            if (result.Failure == ProgressionFailure.None)
            {
                Stage5FinishBlocked = false;
                return true;
            }
            Error = Stage5Runtime.Describe(result.Failure);
            Stage5FinishBlocked = true;
            Changed?.Invoke();
            return false;
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
