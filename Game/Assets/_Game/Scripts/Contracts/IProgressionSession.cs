using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Contracts
{
    public interface IProgressionSession
    {
        ProgressionSnapshot Snapshot { get; }

        ProgressionResult StartRun(ProgressionCommandKey key, int seed,
            IReadOnlyList<ProgressionTransitionPoint> transitionPoints);
        ProgressionResult FinishRun(ProgressionCommandKey key, RunOutcome outcome);
        ProgressionResult ConfirmEnemyDeath(ProgressionCommandKey key, ProgressionCheckpoint checkpoint,
            string spawnId, string enemyTypeId);
        ProgressionResult CollectLoot(ProgressionCommandKey key, string pickupId);
        ProgressionResult ClearCheckpoint(ProgressionCommandKey key, ProgressionCheckpoint checkpoint);

        ProgressionResult BuyNpcOffer(ProgressionCommandKey key, string pointId, string npcId, string offerId,
            string weaponInstanceId = null);
        ProgressionResult CompleteNpcService(ProgressionCommandKey key, string pointId, string npcId, string offerId);
        ProgressionResult ApplyWeaponModule(ProgressionCommandKey key, string itemId, string weaponInstanceId);
        ProgressionResult DrinkBottle(ProgressionCommandKey key, string itemId);
        ProgressionResult BuyHomeUnlock(ProgressionCommandKey key, string offerId);
    }

    public readonly struct ProgressionCommandKey
    {
        // Null RunId scopes an action to the current Home profile.
        public string RunId { get; }
        public string ActionId { get; }

        public ProgressionCommandKey(string runId, string actionId)
        {
            RunId = runId;
            ActionId = actionId;
        }
    }

    public readonly struct ProgressionCheckpoint
    {
        public string StageId { get; }
        public string RoomId { get; }
        public int Index { get; }

        public ProgressionCheckpoint(string stageId, string roomId, int index)
        {
            StageId = stageId;
            RoomId = roomId;
            Index = index;
        }
    }

    [Flags]
    public enum ProgressionNpcKind { None = 0, WeaponDealer = 1, BottleDealer = 2 }

    public readonly struct ProgressionTransitionPoint
    {
        public string PointId { get; }
        public string StageId { get; }
        public string RoadId { get; }
        public string EntranceId { get; }
        public ProgressionNpcKind AllowedNpcs { get; }

        public ProgressionTransitionPoint(string pointId, string stageId, string roadId, string entranceId,
            ProgressionNpcKind allowedNpcs)
        {
            PointId = pointId;
            StageId = stageId;
            RoadId = roadId;
            EntranceId = entranceId;
            AllowedNpcs = allowedNpcs;
        }
    }

    public enum ProgressionStatus { Applied, Rejected, RetryableFailure, Replayed }

    public enum ProgressionFailure
    {
        None,
        InvalidRequest,
        Conflict,
        WrongPhase,
        UnknownTarget,
        InsufficientCoins,
        InsufficientCrystals,
        ResourceFull,
        AlreadyConsumed,
        IncompatibleWeapon,
        ServiceNotReady,
        SaveFailed,
        ProfileIncompatible,
        AlreadyPurchased
    }

    public sealed class ProgressionResult
    {
        public ProgressionStatus Status { get; }
        public ProgressionFailure Failure { get; }
        public ProgressionSnapshot Snapshot { get; }

        private ProgressionResult(ProgressionStatus status, ProgressionFailure failure, ProgressionSnapshot snapshot)
        {
            Status = status;
            Failure = failure;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public static ProgressionResult Applied(ProgressionSnapshot snapshot) =>
            new ProgressionResult(ProgressionStatus.Applied, ProgressionFailure.None, snapshot);

        public static ProgressionResult Rejected(ProgressionFailure failure, ProgressionSnapshot snapshot) =>
            new ProgressionResult(ProgressionStatus.Rejected, RequireFailure(failure), snapshot);

        public static ProgressionResult RetryableFailure(ProgressionFailure failure, ProgressionSnapshot snapshot) =>
            new ProgressionResult(ProgressionStatus.RetryableFailure, RequireFailure(failure), snapshot);

        // Carries the original outcome with a fresh snapshot. A retryable failure must be executed again.
        public ProgressionResult AsReplay(ProgressionSnapshot current)
        {
            if (Status == ProgressionStatus.RetryableFailure || Failure == ProgressionFailure.Conflict)
                throw new InvalidOperationException("This result cannot be replayed.");
            return new ProgressionResult(ProgressionStatus.Replayed, Failure, current);
        }

        private static ProgressionFailure RequireFailure(ProgressionFailure failure)
        {
            if (failure == ProgressionFailure.None)
                throw new ArgumentException("A failed command requires a reason.", nameof(failure));
            return failure;
        }
    }
}
