using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Progression
{
    [Serializable]
    public sealed class ProfileData
    {
        public const int CurrentSchemaVersion = 1;
        public const int FutureRevivalPriceCrystals = 500;
        public const string DefaultWeaponId = "pistol";

        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public int confirmedCrystals;
        public string[] unlockedIds = Array.Empty<string>();
        public string selectedWeaponId = DefaultWeaponId;
        public string[] completedRegionIds = Array.Empty<string>();
        public ActiveRunData activeRun = ActiveRunData.Empty;
        public CheckpointReceipt[] checkpoints = Array.Empty<CheckpointReceipt>();
        public RunReceipt[] finishedRuns = Array.Empty<RunReceipt>();
        public HomePurchaseReceipt[] homePurchases = Array.Empty<HomePurchaseReceipt>();
        public string integrity = "";

        // A transaction replaces changed arrays instead of editing entries in place.
        internal ProfileData Copy() => (ProfileData)MemberwiseClone();

        internal bool IsValid()
        {
            if (schemaVersion != CurrentSchemaVersion || revision < 1 || confirmedCrystals < 0 ||
                !UniqueIds(unlockedIds) || !UniqueIds(completedRegionIds) ||
                (selectedWeaponId != DefaultWeaponId && !unlockedIds.Contains(selectedWeaponId)) ||
                checkpoints == null || finishedRuns == null || homePurchases == null)
                return false;

            if (activeRun.Exists && (!IsId(activeRun.runId) || !CityEconomy.FullRoute.Contains(activeRun.stageId)))
                return false;
            var runIds = new HashSet<string>(StringComparer.Ordinal);
            if (activeRun.Exists) runIds.Add(activeRun.runId);
            foreach (var run in finishedRuns)
                if (!IsId(run.runId) || !Enum.IsDefined(typeof(RunOutcome), run.outcome) || !runIds.Add(run.runId))
                    return false;

            var checkpointKeys = new HashSet<(string, int)>();
            var rooms = new HashSet<(string, string, string)>();
            foreach (var entry in checkpoints)
                if (!IsId(entry.runId) || !runIds.Contains(entry.runId) || entry.checkpointIndex < 0 ||
                    !CityEconomy.FullRoute.Contains(entry.stageId) || !IsId(entry.roomId) || entry.crystals < 0 ||
                    !checkpointKeys.Add((entry.runId, entry.checkpointIndex)) ||
                    !rooms.Add((entry.runId, entry.stageId, entry.roomId)))
                    return false;

            var purchaseIds = new HashSet<string>(StringComparer.Ordinal);
            return homePurchases.All(entry => IsId(entry.actionId) && IsId(entry.offerId) &&
                purchaseIds.Add(entry.actionId));
        }

        private static bool UniqueIds(string[] values) => values != null && values.All(IsId) &&
            new HashSet<string>(values, StringComparer.Ordinal).Count == values.Length;
        private static bool IsId(string value) => !string.IsNullOrWhiteSpace(value);
    }

    [Serializable]
    public struct ActiveRunData
    {
        public static ActiveRunData Empty => new ActiveRunData { runId = "", stageId = "" };

        public string runId;
        public int seed;
        public string stageId;

        public bool Exists => !string.IsNullOrEmpty(runId);
    }

    [Serializable]
    public struct CheckpointReceipt
    {
        public string runId;
        public int checkpointIndex;
        public string stageId;
        public string roomId;
        public int crystals;
    }

    [Serializable]
    public struct RunReceipt
    {
        public string runId;
        public RunOutcome outcome;
    }

    [Serializable]
    public struct HomePurchaseReceipt
    {
        public string actionId;
        public string offerId;
    }
}
