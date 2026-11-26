using System.Linq;
using Game.Contracts;
using Game.Core;

namespace Game.Progression
{
    internal static class SewerEntryService
    {
        internal const string BlueprintId = "blueprint-sealed-mechanism";
        internal const int RepeatCrystals = 150;

        internal static CombatSnapshot Equip(CombatSnapshot state, bool knife, int carried) => new(
            state.Health, state.MaxHealth, 0, state.MaxArmor, state.Stamina, state.MaxStamina,
            knife ? 0 : state.Magazine, state.MagazineCapacity, knife ? 0 : state.ReserveAmmo + carried,
            knife, false, state.IsDead);
    }

    public sealed partial class CityProgressionSession
    {
        internal ProgressionResult CompleteSewer(string cityRunId)
        {
            string receiptId = "sewer/" + cityRunId;
            return Execute(new ProgressionCommandKey(cityRunId, "sewer-finish"), nameof(CompleteSewer), cityRunId, () =>
            {
                var unavailable = EnsureWritableProfile();
                if (unavailable.HasValue) return unavailable.Value;
                if (run != null || profile.activeRun.Exists || !profile.completedRegionIds.Contains("city") ||
                    !profile.finishedRuns.Any(receipt => receipt.runId == cityRunId && receipt.outcome == RunOutcome.Victory))
                    return Reject(ProgressionFailure.WrongPhase);
                if (profile.finishedRuns.Any(receipt => receipt.runId == receiptId)) return Replayed;

                var next = profile.Copy();
                if (next.unlockedIds.Contains(SewerEntryService.BlueprintId))
                {
                    if (next.confirmedCrystals > int.MaxValue - SewerEntryService.RepeatCrystals)
                        return Reject(ProgressionFailure.InvalidRequest);
                    next.confirmedCrystals += SewerEntryService.RepeatCrystals;
                }
                else next.unlockedIds = next.unlockedIds.Append(SewerEntryService.BlueprintId).ToArray();
                next.finishedRuns = next.finishedRuns.Append(new RunReceipt { runId = receiptId, outcome = RunOutcome.Victory }).ToArray();
                return CommitProfile(next);
            });
        }
    }
}
