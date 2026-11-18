using Game.Combat;
using Game.Contracts;
using UnityEngine;

namespace Game.Actors
{
    [DisallowMultipleComponent]
    public sealed class ActorStatusController : MonoBehaviour
    {
        private readonly ActorStatusState state = new ActorStatusState();
        private ActorMotor2D motor;
        private DamageReceiver receiver;
        private long poisonAttackId;
        public ActorActionPermissions Permissions => state.Permissions;
        public int ActiveCount => state.Count;
        public float MovementMultiplier => state.MovementMultiplier;
        public float StunRemaining => state.Remaining(ActorStatusKind.Stun);
        private void EnsureReferences()
        {
            if (motor == null) motor = GetComponent<ActorMotor2D>();
            if (receiver == null) receiver = GetComponent<DamageReceiver>();
        }
        public bool TryApply(ActorStatusEffect effect)
        {
            EnsureReferences();
            if (!isActiveAndEnabled || motor == null || !motor.CombatActive || Time.timeScale <= 0) return false;
            if (!state.TryApply(effect)) return false;
            if (effect.Kind == ActorStatusKind.Stun && GetComponent<StunStars>() == null) gameObject.AddComponent<StunStars>();
            motor.RefreshRestrictions();
            return true;
        }
        internal void ApplyLegacyStun(float seconds)
        {
            EnsureReferences();
            if (!isActiveAndEnabled || !motor.CombatActive || Time.timeScale <= 0) return;
            state.RefreshLegacyStun(seconds);
            if (GetComponent<StunStars>() == null) gameObject.AddComponent<StunStars>();
            motor.RefreshRestrictions();
        }
        public int ReleaseSource(int sourceId) => state.ReleaseSource(sourceId);
        public bool Contains(int sourceId, string effectId) => state.Contains(sourceId, effectId);
        internal void Advance(float deltaTime)
        {
            EnsureReferences();
            state.Advance(deltaTime, ApplyPoison);
        }
        private void ApplyPoison(int sourceId, float amount)
        {
            if (receiver == null || receiver.IsDead || amount <= 0) return;
            // Normal damage/armor/hit protection semantics stay in the single existing receiver.
            var packet = new DamagePacket(sourceId, amount, attackId: ++poisonAttackId);
            receiver.ApplyDamage(in packet);
        }
        public void ResetForEncounter() { state.Reset(); poisonAttackId = 0; }
        private void OnDisable() => ResetForEncounter();
    }
}
