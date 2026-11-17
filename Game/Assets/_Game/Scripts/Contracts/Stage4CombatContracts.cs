using UnityEngine;

namespace Game.Contracts
{
    // Additive S4-01 boundaries. No resource, status or dodge mechanic is implemented here.
    public interface IRunResourceSink
    {
        int TryAddReserve(int amount);
        float TryHeal(float amount);
        float TryRepair(float amount);
    }
    public readonly struct ActorActionPermissions
    {
        public bool CanMove { get; }
        public bool CanAttack { get; }
        public bool CanDodge { get; }
        public bool CanSwitchWeapon { get; }
        public ActorActionPermissions(bool canMove, bool canAttack, bool canDodge, bool canSwitchWeapon)
        { CanMove=canMove; CanAttack=canAttack; CanDodge=canDodge; CanSwitchWeapon=canSwitchWeapon; }
    }
    public interface IActorActionGate { ActorActionPermissions Permissions { get; } }
    public interface IDodgeCommandSink { bool TryRequestDodge(Vector2 direction); }
}
