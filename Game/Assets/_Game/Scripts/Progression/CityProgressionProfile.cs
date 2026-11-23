using System;
using System.Linq;
using Game.Contracts;
using Game.Core;

namespace Game.Progression
{
    public sealed partial class CityProgressionSession
    {
        private static readonly ProgressionHomeOffer DefaultHomeOffer = new("unlock-city-pistol", "pistol-city", 20);
        private readonly ISaveRepository saves;
        private readonly ProgressionHomeOffer homeOffer;
        private ProfileData profile;
        private ProgressionProfileState profileState;
        private bool recoverInterruptedRun;
        private bool recoveredFromBackup;

        public CityProgressionSession(Func<double> gameTime, ISaveRepository saves, ProgressionHomeOffer? offer = null)
        {
            this.gameTime = gameTime ?? throw new ArgumentNullException(nameof(gameTime));
            this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
            homeOffer = offer ?? DefaultHomeOffer;
            if (!Id(homeOffer.OfferId) || !Id(homeOffer.UnlockId) || homeOffer.UnlockId == ProfileData.DefaultWeaponId ||
                homeOffer.PriceCrystals <= 0)
                throw new ArgumentException("Expected a priced alternate starter weapon.", nameof(offer));
            recoverInterruptedRun = true;
            LoadProfile();
            RecoverInterruptedRun();
        }

        private ProgressionProfileSnapshot ProfileSnapshot() => new(
            profile?.confirmedCrystals ?? 0, profileState, profile?.unlockedIds,
            profile != null && profile.activeRun.Exists == false && !profile.unlockedIds.Contains(homeOffer.UnlockId)
                && (profileState == ProgressionProfileState.Ready || profileState == ProgressionProfileState.RecoveredFromBackup)
                ? new[] { homeOffer } : null,
            profile?.selectedWeaponId, profile?.completedRegionIds, profile?.revision ?? 0);

        public ProgressionResult BuyHomeUnlock(ProgressionCommandKey key, string offerId) =>
            Execute(key, nameof(BuyHomeUnlock), offerId, () =>
            {
                var unavailable = EnsureWritableProfile();
                if (unavailable.HasValue) return unavailable.Value;
                foreach (var purchase in profile.homePurchases)
                    if (purchase.actionId == key.ActionId)
                        return purchase.offerId == offerId ? Replayed : Reject(ProgressionFailure.Conflict);
                if (run != null || profile.activeRun.Exists) return Reject(ProgressionFailure.WrongPhase);
                if (offerId != homeOffer.OfferId) return Reject(ProgressionFailure.UnknownTarget);
                if (profile.unlockedIds.Contains(homeOffer.UnlockId)) return Reject(ProgressionFailure.AlreadyPurchased);
                if (profile.confirmedCrystals < homeOffer.PriceCrystals)
                    return Reject(ProgressionFailure.InsufficientCrystals);

                var next = profile.Copy();
                next.confirmedCrystals -= homeOffer.PriceCrystals;
                next.unlockedIds = next.unlockedIds.Append(homeOffer.UnlockId).ToArray();
                next.selectedWeaponId = homeOffer.UnlockId;
                next.homePurchases = next.homePurchases.Append(new HomePurchaseReceipt
                {
                    actionId = key.ActionId,
                    offerId = offerId
                }).ToArray();
                return CommitProfile(next);
            }, home: true);

        public ProgressionResult SelectStarter(ProgressionCommandKey key, string weaponId) =>
            Execute(key, nameof(SelectStarter), weaponId, () =>
            {
                var unavailable = EnsureWritableProfile();
                if (unavailable.HasValue) return unavailable.Value;
                if (run != null || profile.activeRun.Exists) return Reject(ProgressionFailure.WrongPhase);
                if (weaponId != ProfileData.DefaultWeaponId &&
                    (weaponId != homeOffer.UnlockId || !profile.unlockedIds.Contains(weaponId)))
                    return Reject(ProgressionFailure.UnknownTarget);
                if (profile.selectedWeaponId == weaponId) return Replayed;

                var next = profile.Copy();
                next.selectedWeaponId = weaponId;
                return CommitProfile(next);
            }, home: true);

        private CommandOutcome? EnsureWritableProfile()
        {
            if (saves != null && (profile == null && profileState != ProgressionProfileState.IncompatibleSchema))
                LoadProfile();
            if (profileState == ProgressionProfileState.IncompatibleSchema)
                return Reject(ProgressionFailure.ProfileIncompatible);
            if (profile == null) return Retry(ProgressionFailure.SaveFailed);
            return RecoverInterruptedRun();
        }

        private void LoadProfile()
        {
            var loaded = saves.Load();
            profile = loaded.Status == SaveLoadStatus.Missing ? new ProfileData() : loaded.Profile;
            recoveredFromBackup |= loaded.Status == SaveLoadStatus.Recovered;
            profileState = loaded.Status switch
            {
                SaveLoadStatus.Ready or SaveLoadStatus.Missing => recoveredFromBackup
                    ? ProgressionProfileState.RecoveredFromBackup : ProgressionProfileState.Ready,
                SaveLoadStatus.Recovered => ProgressionProfileState.RecoveredFromBackup,
                SaveLoadStatus.IncompatibleSchema => ProgressionProfileState.IncompatibleSchema,
                _ => ProgressionProfileState.Unavailable
            };
            if (profile != null && !profile.activeRun.Exists && lastRun == null && profile.finishedRuns.Length > 0)
            {
                var last = profile.finishedRuns[profile.finishedRuns.Length - 1];
                lastRun = new ProgressionRunSummary(last.runId, last.outcome, 0, Earned(last.runId));
            }
        }

        private CommandOutcome? RecoverInterruptedRun()
        {
            if (!recoverInterruptedRun || profile == null) return null;
            if (!profile.activeRun.Exists)
            {
                recoverInterruptedRun = false;
                return null;
            }

            string id = profile.activeRun.runId;
            var next = EndProfileRun(id, RunOutcome.Abandoned, completeCity: false);
            var result = CommitProfile(next);
            if (result.Status != ProgressionStatus.Applied)
            {
                if (profile != null) profileState = ProgressionProfileState.Unavailable;
                return result;
            }
            lastRun = new ProgressionRunSummary(id, RunOutcome.Abandoned, 0, Earned(id));
            recoverInterruptedRun = false;
            return null;
        }

        private CommandOutcome CommitProfile(ProfileData next)
        {
            if (profile.revision == long.MaxValue) return Retry(ProgressionFailure.SaveFailed);
            next.revision = profile.revision + 1;
            if (saves != null)
            {
                switch (saves.TryCommit(profile.revision, next))
                {
                    case SaveWriteStatus.Saved:
                        break;
                    case SaveWriteStatus.RevisionConflict:
                        LoadProfile();
                        return Retry(ProgressionFailure.SaveFailed);
                    case SaveWriteStatus.IncompatibleSchema:
                        profile = null;
                        profileState = ProgressionProfileState.IncompatibleSchema;
                        return Reject(ProgressionFailure.ProfileIncompatible);
                    default:
                        return Retry(ProgressionFailure.SaveFailed);
                }
            }
            profile = next;
            if (profileState == ProgressionProfileState.Unavailable)
                profileState = recoveredFromBackup ? ProgressionProfileState.RecoveredFromBackup : ProgressionProfileState.Ready;
            return Applied;
        }

        private ProfileData EndProfileRun(string id, RunOutcome outcome, bool completeCity)
        {
            var next = profile.Copy();
            next.activeRun = ActiveRunData.Empty;
            next.finishedRuns = next.finishedRuns.Append(new RunReceipt { runId = id, outcome = outcome }).ToArray();
            if (completeCity && !next.completedRegionIds.Contains("city"))
                next.completedRegionIds = next.completedRegionIds.Append("city").ToArray();
            return next;
        }

        private int Earned(string runId) => profile.checkpoints.Where(receipt => receipt.runId == runId)
            .Sum(receipt => receipt.crystals);
    }
}
