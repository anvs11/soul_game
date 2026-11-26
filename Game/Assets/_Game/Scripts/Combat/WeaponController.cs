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
        private int citySeed, cityShots;
        private bool cityCritical, fireAmmo;
        private bool sewerWeaponLocked;
        private float torchCooldown;
        internal bool TorchReadyMode => sewerWeaponLocked && State.Selected && State.Magazine == 0 && State.ReserveAmmo == 0;
        internal void LockSewerWeapon() => sewerWeaponLocked = true;
        public event Action Changed;
        public WeaponRuntimeState State => state ?? (state = new WeaponRuntimeState(definition));
        internal void EquipCityStarter()
        {
            EquipCityWeapon("Stage5CityPistol", false);
        }
        internal void EquipCityFamily(CityFirearm family)
        {
            EquipCityWeapon(family == CityFirearm.SubmachineGun ? "CitySubmachineGun" : "CityCarbine",
                family == CityFirearm.Carbine);
        }
        private void EquipCityWeapon(string resource, bool fire)
        {
            var weapon = Resources.Load<WeaponDefinition>(resource);
            if (weapon == null) throw new InvalidOperationException("Missing firearm resource: " + resource);
            definition = weapon;
            state = new WeaponRuntimeState(weapon);
            fireAmmo = fire;
            Changed?.Invoke();
        }
        internal void EnableCityCriticals(int seed)
        {
            cityCritical = true;
            citySeed = seed;
        }
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

            var permissions = motor.Permissions;
            if (!sewerWeaponLocked && command.SwitchPressed && permissions.CanSwitchWeapon) State.SetSelected(!State.Selected);
            State.Advance(Time.deltaTime, command.AttackHeld && !sewerWeaponLocked);
            torchCooldown = Mathf.Max(0, torchCooldown - Time.deltaTime);
            var target = selector.Select(transform.position, State.IsMelee || TorchReadyMode ? MeleeAttack.Radius : definition.TargetRange, identity.ActorId);
            if (target != null)
            {
                Vector2 offset = target.transform.position - transform.position;
                if (offset.sqrMagnitude > .0001f) { aim = offset.normalized; }
            }
            else if (command.Move.sqrMagnitude > .0001f) aim = command.Move.normalized;
            if (permissions.CanAttack && command.AttackHeld && TorchReadyMode)
            {
                if (torchCooldown <= 0)
                {
                    torchCooldown = .88f;
                    melee.Swing(transform.position, aim, identity.ActorId, identity.Faction, hitMask, ++attackId, 8);
                }
            }
            else if (permissions.CanAttack && command.AttackHeld && State.Selected)
            {
                bool critical = cityCritical && Game.Progression.CityEconomy.Roll(citySeed,
                    (cityShots + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), "critical", 8) == 0;
                var request = new ProjectileSpawnRequest(transform.position, aim, definition.ProjectileSpeed,
                    definition.ProjectileLifetime, definition.ProjectileRadius, hitMask, identity.ActorId, identity.Faction,
                    new DamagePacket(identity.ActorId, definition.Damage * (critical ? 1.5f : 1f),
                        fireAmmo ? DamageType.Fire : DamageType.Normal, attackId: ++attackId));
                if (State.TryFire((IProjectileSpawner)projectileSpawner, in request)) cityShots++;
            }
            else if (permissions.CanAttack && command.AttackHeld && State.IsMelee)
            {
                melee.TryAttack(State, transform.position, aim, identity.ActorId, identity.Faction, hitMask, ++attackId);
            }
            if (beforeStamina != State.Stamina || beforeMagazine != State.Magazine || beforeReserve != State.ReserveAmmo || beforeReload != State.IsReloading || beforeSelected != State.Selected)
                Changed?.Invoke();
        }
    }
}
