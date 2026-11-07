using System;

namespace Game.Core
{
    public static class ScenePaths
    {
        public const string Bootstrap = "Bootstrap";
        public const string Home = "Home";
        public const string CombatSandbox = "CombatSandbox";
        public const string Folder = "Assets/_Game/Scenes/";
        public static string Asset(string name) => Folder + name + ".unity";
    }

    public enum RunOutcome { Victory, Defeat, Abandoned }
    public enum RunPhase { Booting, Home, LoadingArena, InArena, ReturningHome, Faulted }

    public readonly struct RunRequest
    {
        public string ArenaId { get; }
        public int Seed { get; }

        public RunRequest(string arenaId, int seed)
        {
            ArenaId = arenaId;
            Seed = seed;
        }

        public static RunRequest Sandbox(int seed = 0) =>
            new RunRequest(ScenePaths.CombatSandbox, seed);
    }

    public interface ISceneFlow
    {
        // false: no load started and no callback; true: callback exactly once.
        bool TryLoad(string sceneName, Action<bool, string> completed);
    }
}
