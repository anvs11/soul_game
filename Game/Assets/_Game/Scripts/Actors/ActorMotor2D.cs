using System;
using Game.Input;
using Game.Contracts;
using UnityEngine;

namespace Game.Actors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class ActorMotor2D : MonoBehaviour, IActorActionGate
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField, Min(0f)] private float speed = 5.5f;
        private Rigidbody2D body;
        private IPlayerInputSource source;
        private bool controlsEnabled = true;
        private ActorStatusController statuses;
        private PlayerDodgeController dodge;
        private DamageReceiver health;
        public bool CombatActive => controlsEnabled && isActiveAndEnabled && (health == null || !health.IsDead);
        public ActorActionPermissions Permissions
        {
            get
            {
                if (!CombatActive || Time.timeScale <= 0) return default;
                var status = statuses != null ? statuses.Permissions : new ActorActionPermissions(true, true, true, true);
                bool moving = IsKnockedBack || (dodge != null && dodge.isActiveAndEnabled && dodge.IsDodging);
                return new ActorActionPermissions(status.CanMove && !moving, status.CanAttack,
                    status.CanDodge && !moving && (dodge == null || dodge.CooldownRemaining <= .000001f), status.CanSwitchWeapon);
            }
        }
        public PlayerCommand CurrentCommand { get; private set; }
        public event Action<PlayerCommand> CommandSampled;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<DamageReceiver>();
            statuses = GetComponent<ActorStatusController>();
            if (statuses == null) statuses = gameObject.AddComponent<ActorStatusController>();
        }
        private void OnEnable()
        {
            source = inputSource as IPlayerInputSource;
            if (source == null)
            {
                Debug.LogError("ActorMotor2D requires an IPlayerInputSource component.", this);
                enabled = false;
            }
        }

        public void Configure(MonoBehaviour provider)
        {
            if (isActiveAndEnabled) throw new InvalidOperationException("Disable motor before configuring it.");
            if (!(provider is IPlayerInputSource)) throw new ArgumentException("Expected IPlayerInputSource.", nameof(provider));
            inputSource = provider;
        }

        public float StunRemaining => statuses != null ? statuses.StunRemaining : 0;
        public void Stun(float seconds)
        {
            if (!ActorStatusEffect.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!CombatActive || seconds == 0) return;
            // Legacy callers represent discrete hits; new repeating hazards must reuse their own effect key.
            statuses.ApplyLegacyStun(seconds);
        }
        private void Update()
        {
            if (dodge == null) dodge = GetComponent<PlayerDodgeController>();
            if (CombatActive) statuses.Advance(Time.deltaTime);
            if (!CombatActive) ResetForEncounter();
            if (Time.timeScale <= 0) StopMotion();
            var command = source.ReadCommand();
            var beforeDodge = Permissions;
            if (CombatActive && Time.timeScale > 0 && dodge != null && dodge.isActiveAndEnabled)
            {
                if (Permissions.CanMove) dodge.ObserveMovement(command.Move);
                if (command.DodgePressed) dodge.TryRequestDodge(command.Move);
            }
            var permissions = Permissions;
            CurrentCommand = new PlayerCommand(permissions.CanMove ? command.Move : Vector2.zero,
                permissions.CanAttack && command.AttackHeld, permissions.CanSwitchWeapon && command.SwitchPressed,
                CombatActive && Time.timeScale > 0 && command.InteractPressed, command.PausePressed,
                beforeDodge.CanDodge && command.DodgePressed,
                CombatActive && Time.timeScale > 0 && command.NpcPressed);
            CommandSampled?.Invoke(CurrentCommand);
        }

        private float knockRemaining, knockSpeed;
        private Vector2 knockDirection;
        private readonly System.Collections.Generic.List<RaycastHit2D> knockHits = new System.Collections.Generic.List<RaycastHit2D>();
        public bool IsKnockedBack => knockRemaining > 0;
        public void BeginKnockback(Vector2 direction, float distance = 1f, float duration = .15f)
        {
            if (!CombatActive || Time.timeScale <= 0 || direction.sqrMagnitude < .0001f) return;
            if (!ActorStatusEffect.Finite(direction.x) || !ActorStatusEffect.Finite(direction.y) || !ActorStatusEffect.Finite(direction.sqrMagnitude)) throw new ArgumentOutOfRangeException(nameof(direction));
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0 || float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (!ActorStatusEffect.Finite(distance / duration)) throw new ArgumentOutOfRangeException(nameof(distance));
            if (dodge == null) dodge = GetComponent<PlayerDodgeController>();
            dodge?.CancelMotion();
            knockDirection = direction.normalized; knockSpeed = distance/duration; knockRemaining=duration;
        }
        private void FixedUpdate()
        {
            if (!CombatActive || Time.timeScale <= 0) { StopMotion(); return; }
            if (dodge == null) dodge = GetComponent<PlayerDodgeController>();
            RefreshRestrictions();
            bool wasDodging = dodge != null && dodge.isActiveAndEnabled && dodge.IsDodging;
            float dodgeStep = dodge != null && dodge.isActiveAndEnabled ? dodge.Step(Time.fixedDeltaTime) : 0;
            if (knockRemaining > 0)
            {
                float step = AvailableDistance(knockDirection, knockSpeed * Mathf.Min(Time.fixedDeltaTime, knockRemaining));
                body.linearVelocity = knockDirection * (step / Time.fixedDeltaTime);
                knockRemaining = Mathf.Max(0, knockRemaining - Time.fixedDeltaTime);
                return;
            }
            if (wasDodging)
            {
                float step = AvailableDistance(dodge.Direction, dodgeStep);
                body.linearVelocity = dodge.Direction * (step / Time.fixedDeltaTime);
                if (step + .0001f < dodgeStep) dodge.CancelMotion();
                return;
            }
            body.linearVelocity = Permissions.CanMove ? Vector2.ClampMagnitude(CurrentCommand.Move, 1f) * speed * statuses.MovementMultiplier : Vector2.zero;
        }

        // Cast the actual attached body shape; stop short of walls/doors, never teleport.
        internal float AvailableDistance(Vector2 direction, float distance)
        {
            var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(~0);
            body.Cast(direction, filter, knockHits, distance + .02f);
            foreach (var hit in knockHits)
                if (hit.collider != null && hit.collider.GetComponentInParent<ActorIdentity>() == null)
                    distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - .02f));
            return distance;
        }
        internal void StopMotion() { if (body != null) body.linearVelocity = Vector2.zero; }
        internal void RefreshRestrictions()
        {
            if (dodge == null) dodge = GetComponent<PlayerDodgeController>();
            if (statuses != null && !statuses.Permissions.CanDodge) dodge?.CancelMotion();
            if (statuses != null && !statuses.Permissions.CanMove && !IsKnockedBack) StopMotion();
        }
        public void ResetForEncounter()
        {
            CurrentCommand = default;
            knockRemaining = 0;
            statuses?.ResetForEncounter();
            if (dodge == null) dodge = GetComponent<PlayerDodgeController>();
            dodge?.ResetForEncounter();
            StopMotion();
        }
        public void SetControlsEnabled(bool value)
        {
            controlsEnabled = value;
            // Repeated enable is not a cleanse or cooldown reset.
            if (!value) ResetForEncounter();
        }
        private void OnDisable() => ResetForEncounter();
    }
}
