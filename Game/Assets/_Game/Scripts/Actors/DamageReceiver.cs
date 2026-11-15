using System;
using Game.Combat;
using UnityEngine;
namespace Game.Actors
{
    [DisallowMultipleComponent]
    public sealed class DamageReceiver : MonoBehaviour
    {
        [SerializeField, Min(.001f)] private float initialHealth = 100f;
        [SerializeField, Min(0)] private float initialArmor = 30f;
        [SerializeField, Min(0)] private float protectionSeconds = ProtectionSeconds;
        private HealthState state;
        private double protectedUntil;
        public const float ProtectionSeconds = .5f;
        private HealthState State => state ?? (state = new HealthState(initialHealth, initialArmor));
        public float Health => State.CurrentHealth;
        public float MaxHealth => State.MaxHealth;
        public float Armor => State.CurrentArmor;
        public float MaxArmor => State.MaxArmor;
        public bool IsDead => State.IsDead;
        public event Action<DamageResult> DamageApplied;
        public void Configure(float health, float armor, float protection)
        {
            if (state != null || isActiveAndEnabled) throw new InvalidOperationException("Configure an unused disabled receiver.");
            // HealthState validates health and armor before any state is changed.
            var validated = new HealthState(health, armor);
            if (float.IsNaN(protection) || float.IsInfinity(protection) || protection < 0)
                throw new ArgumentOutOfRangeException(nameof(protection));
            initialHealth = validated.MaxHealth; initialArmor = validated.MaxArmor; protectionSeconds = protection;
        }
        public void RestoreForEncounter(float health, float armor)
        {
            if (State.IsDead || State.CurrentHealth != State.MaxHealth || State.CurrentArmor != State.MaxArmor)
                throw new InvalidOperationException("Restore only a fresh encounter receiver.");
            var restored = new HealthState(initialHealth, initialArmor);
            restored.RestoreForEncounter(health, armor);
            state = restored;
            protectedUntil = 0;
        }
        public DamageResult ApplyDamage(in DamagePacket packet)
        {
            if (!isActiveAndEnabled || Time.timeAsDouble < protectedUntil) return default;
            var result = State.ApplyDamage(in packet);
            if (!result.Applied) return result;
            protectedUntil = Time.timeAsDouble + (packet.ProtectionSecondsOverride ?? protectionSeconds);
            DamageApplied?.Invoke(result);
            return result;
        }
    }
}
