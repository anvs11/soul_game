using System;

namespace Game.Contracts
{
    // Proposed v0.1 boundary from .agent/STAGE_1_PLAN.md.
    // Values describe actual runtime state; this type does not implement combat.
    public readonly struct CombatSnapshot
    {
        public float Health { get; }
        public float MaxHealth { get; }
        public float Armor { get; }
        public float MaxArmor { get; }
        public float Stamina { get; }
        public float MaxStamina { get; }
        public int Magazine { get; }
        public int MagazineCapacity { get; }
        public int ReserveAmmo { get; }
        public bool IsMelee { get; }
        public bool IsReloading { get; }
        public bool IsDead { get; }

        public CombatSnapshot(
            float health, float maxHealth, float armor, float maxArmor,
            float stamina, float maxStamina, int magazine, int magazineCapacity,
            int reserveAmmo, bool isMelee, bool isReloading, bool isDead)
        {
            Health = health;
            MaxHealth = maxHealth;
            Armor = armor;
            MaxArmor = maxArmor;
            Stamina = stamina;
            MaxStamina = maxStamina;
            Magazine = magazine;
            MagazineCapacity = magazineCapacity;
            ReserveAmmo = reserveAmmo;
            IsMelee = isMelee;
            IsReloading = isReloading;
            IsDead = isDead;
        }
    }

    public interface IDefeatable
    {
        bool IsDead { get; }
        event Action Died;
    }

    public interface IPlayerCombatSource : IDefeatable
    {
        CombatSnapshot ReadSnapshot();
        event Action<CombatSnapshot> Changed;
        event Action InteractRequested;
        event Action PauseRequested;

        // Disables movement/attacks. Pause requests must remain available.
        // Death prevents combat even if enabled is subsequently true.
        void SetControlsEnabled(bool enabled);
    }
}
