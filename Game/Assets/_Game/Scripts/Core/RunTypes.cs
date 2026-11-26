using System;
using Game.Encounters;

namespace Game.Core
{
    public static class ScenePaths
    {
        public const string Bootstrap = "Bootstrap";
        public const string Home = "Home";
        public const string CombatSandbox = "CombatSandbox";
        public const string Stage2CombatSandbox = "Stage2CombatSandbox";
        public const string Stage2BossSandbox = "Stage2BossSandbox";
        public const string Stage3StreetSandbox = "Stage3StreetSandbox";
        public const string Stage4StreetMap = "Stage4StreetMap";
        public const string Folder = "Assets/_Game/Scenes/";
        public static string Asset(string name) => Folder + name + ".unity";
    }

    public enum RunOutcome { Victory, Defeat, Abandoned }
    public enum RunPhase { Booting, Home, LoadingArena, InArena, ReturningHome, Faulted }
    public enum CityVisualPreview { None, Street, Market, Office, Sewer }

    public readonly struct RunRequest
    {
        public string ArenaId { get; }
        public int Seed { get; }
        public EncounterPlan FixedEncounter { get; }
        public bool IsStage5Preview { get; }
        public bool IsStage5City { get; }
        public bool IsFullCity { get; }
        public bool IsRanchPreview { get; }
        public bool IsRanch { get; }
        public CityVisualPreview VisualPreview { get; }
        internal int FirstCitySegment => VisualPreview switch
        { CityVisualPreview.Market => 5, CityVisualPreview.Office => 13, CityVisualPreview.Sewer => 14, _ => 0 };
        internal int LastPreviewSegment => VisualPreview switch { CityVisualPreview.Street => 2, CityVisualPreview.Market => 5, _ => 14 };
        public bool IsStreetRun => ArenaId == ScenePaths.Stage3StreetSandbox;
        public bool IsFixedStreet1_1 => ArenaId == ScenePaths.Stage4StreetMap;
        public bool IsStage2Route { get; }
        public bool IsStage2Diagnostic => ArenaId == ScenePaths.Stage2CombatSandbox && !IsStage2Route;

        public RunRequest(string arenaId, int seed, bool isStage2Route = false, EncounterPlan fixedEncounter = null,
            bool stage5Preview = false, bool stage5City = false, bool fullCity = false,
            CityVisualPreview visualPreview = CityVisualPreview.None, bool ranch = false, bool ranchPreview = false)
        {
            ArenaId = arenaId;
            Seed = seed;
            IsStage2Route = isStage2Route;
            FixedEncounter = fixedEncounter;
            IsStage5Preview = stage5Preview;
            IsStage5City = stage5City;
            IsFullCity = fullCity;
            IsRanchPreview = ranchPreview;
            IsRanch = ranch || ranchPreview;
            VisualPreview = visualPreview;
        }

        public static RunRequest StreetRun(int seed = 0) => new RunRequest(ScenePaths.Stage3StreetSandbox, seed);

        public static RunRequest FixedStreet1_1(int seed = 0) => new RunRequest(ScenePaths.Stage4StreetMap, seed);
        public static RunRequest FixedStreet1_1(EncounterPlan plan) =>
            new RunRequest(ScenePaths.Stage4StreetMap, plan.Seed, fixedEncounter: plan);

        public static RunRequest Stage5Preview(EncounterPlan plan) =>
            new RunRequest(ScenePaths.Stage4StreetMap, plan.Seed, fixedEncounter: plan, stage5Preview: true);

        public static RunRequest RanchPreview(EncounterPlan plan) =>
            new RunRequest(ScenePaths.Stage4StreetMap, plan.Seed, fixedEncounter: plan, ranchPreview: true);

        public static RunRequest Ranch(EncounterPlan plan) =>
            new RunRequest(ScenePaths.Stage4StreetMap, plan.Seed, fixedEncounter: plan, ranch: true);

        public static RunRequest Stage5City(EncounterPlan plan, bool fullCity = false, CityVisualPreview preview = CityVisualPreview.None) =>
            new RunRequest(ScenePaths.Stage4StreetMap, plan.Seed, fixedEncounter: plan, stage5City: true,
                fullCity: fullCity || preview != CityVisualPreview.None, visualPreview: preview);

        public static RunRequest Stage2(int seed = 0) => new RunRequest(ScenePaths.Stage2CombatSandbox, seed, true);

        public static RunRequest Stage2Diagnostic(int seed = 0) => new RunRequest(ScenePaths.Stage2CombatSandbox, seed);

        public static RunRequest Sandbox(int seed = 0) =>
            new RunRequest(ScenePaths.CombatSandbox, seed);
    }

    public interface ISceneFlow
    {
        // false: no load started and no callback; true: callback exactly once.
        bool TryLoad(string sceneName, Action<bool, string> completed);
    }
}
