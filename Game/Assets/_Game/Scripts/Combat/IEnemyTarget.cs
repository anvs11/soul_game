using Game.Contracts;
using UnityEngine;
namespace Game.Combat
{
    // Local combat abstraction; CombatContracts v0.1 is unchanged.
    public interface IEnemyTarget : IDefeatable
    {
        int ActorId { get; }
        Transform transform { get; }
        bool isActiveAndEnabled { get; }
    }
    public static class EnemyTargets
    {
        public static bool Active(IEnemyTarget target) => target is Component component && component != null
            && target.isActiveAndEnabled && !target.IsDead;
    }
}
