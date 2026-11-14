using System;
namespace Game.Combat
{
    public sealed class WeaponRuntimeState
    {
        private readonly WeaponDefinition definition;
        private float cooldown, reloadRemaining, meleeCooldown, knifeRecoveryDelay;
        public const float MaximumStamina = 100f;
        public const float MeleeCost = 20f;
        public const float MeleeInterval = .45f;
        public const float StaminaRecoveryPerSecond = 25f;
        public const float KnifeRecoveryPerSecond = 10f;
        public const float KnifeRecoveryPause = .8f;
        public float Stamina { get; private set; } = MaximumStamina;
        public float MaxStamina => MaximumStamina;
        public bool IsMelee => !Selected;
        public int Magazine { get; private set; }
        public int ReserveAmmo { get; private set; }
        public int MagazineCapacity => definition.MagazineCapacity;
        public bool Selected { get; private set; } = true;
        public bool IsReloading => reloadRemaining > 0;
        public WeaponRuntimeState(WeaponDefinition definition)
        {
            this.definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            if (definition.MagazineCapacity < 1 || definition.InitialReserve < 0 ||
                !Positive(definition.Damage) || !Positive(definition.ShotInterval) || !Positive(definition.ReloadSeconds) ||
                !Positive(definition.ProjectileSpeed) || !Positive(definition.ProjectileLifetime) || !Positive(definition.ProjectileRadius))
                throw new ArgumentException("Invalid weapon settings.", nameof(definition));
            Magazine = definition.MagazineCapacity; ReserveAmmo = definition.InitialReserve;
        }
        private static bool Positive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
        public void SetSelected(bool selected) { Selected = selected; if (!selected) reloadRemaining = 0; }
        public void CancelReload() => reloadRemaining = 0;
        public void Advance(float deltaTime, bool attackHeld = false)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            cooldown = Math.Max(0, cooldown - deltaTime);
            meleeCooldown = Math.Max(0, meleeCooldown - deltaTime);
            float knifeRecoveryTime = Math.Max(0, deltaTime - knifeRecoveryDelay);
            knifeRecoveryDelay = Math.Max(0, knifeRecoveryDelay - deltaTime);
            if (!Selected)
            {
                if (attackHeld) knifeRecoveryDelay = KnifeRecoveryPause;
                else Stamina = Math.Min(MaximumStamina, Stamina + KnifeRecoveryPerSecond * knifeRecoveryTime);
                return;
            }
            Stamina = Math.Min(MaximumStamina, Stamina + StaminaRecoveryPerSecond * deltaTime);
            if (IsReloading)
            {
                reloadRemaining = Math.Max(0, reloadRemaining - deltaTime);
                if (!IsReloading)
                {
                    int transfer = Math.Min(MagazineCapacity - Magazine, ReserveAmmo);
                    Magazine += transfer; ReserveAmmo -= transfer;
                }
            }
            else if (Magazine == 0 && ReserveAmmo > 0) reloadRemaining = definition.ReloadSeconds;
        }
        // An accepted swing costs stamina even when it misses or a target rejects damage.
        public bool TryMeleeAttack()
        {
            if (!IsMelee || meleeCooldown > .000001f || Stamina < MeleeCost) return false;
            Stamina -= MeleeCost;
            meleeCooldown = MeleeInterval;
            knifeRecoveryDelay = KnifeRecoveryPause;
            return true;
        }
        public bool TryFire(IProjectileSpawner spawner, in ProjectileSpawnRequest request)
        {
            if (!Selected || IsReloading || cooldown > .000001f || Magazine == 0 || spawner == null) return false;
            if (!spawner.TrySpawn(in request)) return false;
            Magazine--; cooldown = definition.ShotInterval; return true;
        }
    }
}
