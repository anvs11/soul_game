using System.Collections.Generic;

namespace Game.Contracts
{
    public enum ProgressionSupplyKind { Medkit, Armor }
    public enum ProgressionWeaponKind { Firearm, Blade }
    public enum ProgressionModuleKind { FasterFire, SharpenedBlade }

    public interface ICityProgressionSession : IProgressionSession
    {
        ProgressionResult EnterStage(ProgressionCommandKey key, string stageId);
        ProgressionResult CollectSupply(ProgressionCommandKey key, string supplyId, ProgressionSupplyKind kind,
            int amount);
    }

    public interface IProgressionCombatSink : IRunResourceSink
    {
        string EquippedWeaponId { get; }
        ProgressionWeaponKind EquippedWeaponKind { get; }

        // The map binds the same logical weapon IDs when its player actor changes between stages.
        void SetProgressionModifiers(IReadOnlyDictionary<string, ProgressionModuleKind> modules,
            IReadOnlyList<ProgressionEffectSnapshot> bottleEffects);
    }
}
