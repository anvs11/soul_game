using System;
using Game.Combat;
namespace Game.Actors
{
    public sealed class HealthState
    {
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; }
        public float CurrentArmor { get; private set; }
        public float MaxArmor { get; }
        public bool IsDead => CurrentHealth <= 0;
        public HealthState(float maxHealth = 100f, float maxArmor = 30f)
        {
            if (float.IsNaN(maxHealth) || float.IsInfinity(maxHealth) || maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (float.IsNaN(maxArmor) || float.IsInfinity(maxArmor) || maxArmor < 0) throw new ArgumentOutOfRangeException(nameof(maxArmor));
            MaxHealth = CurrentHealth = maxHealth; MaxArmor = CurrentArmor = maxArmor;
        }
        public DamageResult ApplyDamage(in DamagePacket packet)
        {
            if (IsDead || packet.Amount <= 0) return default;
            float armorLost = Math.Min(CurrentArmor, packet.Amount);
            float healthLost = Math.Min(CurrentHealth, packet.Amount - armorLost);
            CurrentArmor -= armorLost; CurrentHealth -= healthLost;
            return new DamageResult(armorLost, healthLost, IsDead);
        }
    }
}
