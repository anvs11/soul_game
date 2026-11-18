using Game.Combat;
using Game.Contracts;
using UnityEngine;

namespace Game.Actors
{
    // Opt-in component. S4-04C owns input/UI/prefab attachment.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorMotor2D))]
    public sealed class PlayerDodgeController : MonoBehaviour, IDodgeCommandSink
    {
        // Explicit provisional S4-05A0 tuning (see S4_COMBAT_PLAN); not final balance.
        public const float Speed = 12f;
        public const float Duration = .20f;
        public const float Cooldown = .8f;
        public const float ResourceCost = 0f;
        public const float InvulnerabilitySeconds = 0f;
        private ActorMotor2D motor;
        private WeaponController weapon;
        private float remaining;
        private Vector2 lastMove;
        public bool IsDodging => remaining > 0;
        public Vector2 Direction { get; private set; }
        public float CooldownRemaining { get; private set; }
        private void EnsureReferences()
        {
            if (motor == null) motor = GetComponent<ActorMotor2D>();
            if (weapon == null) weapon = GetComponent<WeaponController>();
        }
        internal void ObserveMovement(Vector2 move)
        {
            if (ValidDirection(move) && move.sqrMagnitude > .0001f) lastMove = move.normalized;
        }
        public bool TryRequestDodge(Vector2 direction)
        {
            EnsureReferences();
            if (!isActiveAndEnabled || !ValidDirection(direction) || !motor.Permissions.CanDodge || CooldownRemaining > .000001f) return false;
            if (direction.sqrMagnitude < .0001f)
                direction = lastMove.sqrMagnitude > .0001f ? lastMove : weapon != null ? weapon.AimDirection : Vector2.down;
            if (!ValidDirection(direction) || direction.sqrMagnitude < .0001f) direction = Vector2.down;
            direction.Normalize();
            // A fully blocked request is rejected before changing any state/cooldown.
            if (motor.AvailableDistance(direction, Speed * Time.fixedDeltaTime) <= .0001f) return false;
            Direction = direction; remaining = Duration; CooldownRemaining = Cooldown;
            return true;
        }
        internal float Step(float deltaTime)
        {
            CooldownRemaining = Mathf.Max(0, CooldownRemaining - deltaTime);
            float distance = Speed * Mathf.Min(deltaTime, remaining);
            remaining = Mathf.Max(0, remaining - deltaTime);
            return distance;
        }
        internal void CancelMotion() { remaining = 0; }
        public void ResetForEncounter() { remaining = CooldownRemaining = 0; Direction = lastMove = Vector2.zero; }
        private void OnDisable()
        {
            ResetForEncounter();
            if (motor != null) motor.StopMotion();
        }
        private static bool ValidDirection(Vector2 direction) => ActorStatusEffect.Finite(direction.x) && ActorStatusEffect.Finite(direction.y) && ActorStatusEffect.Finite(direction.sqrMagnitude);
    }
}
