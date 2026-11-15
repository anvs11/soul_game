using System;
using Game.Actors;
using Game.Input;
using UnityEngine;
namespace Game.Combat
{
    [RequireComponent(typeof(ActorMotor2D), typeof(ActorIdentity), typeof(DamageReceiver))]
    [RequireComponent(typeof(MeleeAttack), typeof(AutoAimSelector))]
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition definition;
        [SerializeField] private MonoBehaviour projectileSpawner;
        [SerializeField] private LayerMask hitMask = ~0;
        private WeaponRuntimeState state;
        private ActorMotor2D motor;
        private ActorIdentity identity;
        private DamageReceiver health;
        private MeleeAttack melee;
        private AutoAimSelector selector;
        public Vector2 AimDirection => aim;
        private bool controlsEnabled = true;
        private Vector2 aim = Vector2.down;
        private long attackId;
        public event Action Changed;
        public WeaponRuntimeState State => state ?? (state = new WeaponRuntimeState(definition));
        public void Configure(WeaponDefinition settings, MonoBehaviour spawner)
        {
            if (isActiveAndEnabled || state != null) throw new InvalidOperationException("Configure a disabled, unused controller.");
            if (!(spawner is IProjectileSpawner)) throw new ArgumentException("Expected IProjectileSpawner.", nameof(spawner));
            definition = settings; projectileSpawner = spawner;
        }
        private void OnEnable()
        {
            motor = GetComponent<ActorMotor2D>(); identity = GetComponent<ActorIdentity>(); health = GetComponent<DamageReceiver>();
            melee = GetComponent<MeleeAttack>(); selector = GetComponent<AutoAimSelector>();
            if (definition == null || !(projectileSpawner is IProjectileSpawner))
            { Debug.LogError("WeaponController requires definition and IProjectileSpawner.", this); enabled = false; return; }
            _ = State;
            motor.CommandSampled += OnCommand;
        }
        private void OnDisable() { if (motor != null) motor.CommandSampled -= OnCommand; state?.CancelReload(); }
        public void SetControlsEnabled(bool value)
        {
            controlsEnabled = value;
            if (!value && state != null && state.IsReloading) { state.CancelReload(); }
        }
        private void OnCommand(PlayerCommand command)
        {
            if (!controlsEnabled || health.IsDead || Time.deltaTime <= 0) return;
            float beforeStamina = State.Stamina;
            int beforeMagazine = State.Magazine, beforeReserve = State.ReserveAmmo;
            bool beforeReload = State.IsReloading, beforeSelected = State.Selected;

            if (command.SwitchPressed) State.SetSelected(!State.Selected);
            State.Advance(Time.deltaTime, command.AttackHeld);
            var target = selector.Select(transform.position, State.IsMelee ? MeleeAttack.Radius : definition.TargetRange, identity.ActorId);
            if (target != null)
            {
                Vector2 offset = target.transform.position - transform.position;
                if (offset.sqrMagnitude > .0001f) { aim = offset.normalized; }
            }
            else if (command.Move.sqrMagnitude > .0001f) aim = command.Move.normalized;
            if (command.AttackHeld && State.Selected)
            {
                var request = new ProjectileSpawnRequest(transform.position, aim, definition.ProjectileSpeed,
                    definition.ProjectileLifetime, definition.ProjectileRadius, hitMask, identity.ActorId, identity.Faction,
                    new DamagePacket(identity.ActorId, definition.Damage, attackId: ++attackId));
                State.TryFire((IProjectileSpawner)projectileSpawner, in request);
            }
            else if (command.AttackHeld && State.IsMelee)
            {
                melee.TryAttack(State, transform.position, aim, identity.ActorId, identity.Faction, hitMask, ++attackId);
            }
            if (beforeStamina != State.Stamina || beforeMagazine != State.Magazine || beforeReserve != State.ReserveAmmo || beforeReload != State.IsReloading || beforeSelected != State.Selected)
                Changed?.Invoke();
        }
    }
}
