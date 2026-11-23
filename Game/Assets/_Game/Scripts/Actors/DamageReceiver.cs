using System;
using Game.Combat;
using UnityEngine;
namespace Game.Actors
{
    public interface IDamageAdjustment
    {
        bool CanReceiveHits { get; }
        float AdjustDamage(in DamagePacket packet);
    }
    [DisallowMultipleComponent]
    public sealed class DamageReceiver : MonoBehaviour
    {
        [SerializeField, Min(.001f)] private float initialHealth = 100f;
        [SerializeField, Min(0)] private float initialArmor = 30f;
        [SerializeField, Min(0)] private float protectionSeconds = ProtectionSeconds;
        private HealthState state;
        private double protectedUntil;
        private IDamageAdjustment adjustment;
        public const float ProtectionSeconds = .5f;
        private HealthState State => state ?? (state = new HealthState(initialHealth, initialArmor));
        public float Health => State.CurrentHealth;
        public float MaxHealth => State.MaxHealth;
        public float Armor => State.CurrentArmor;
        public float MaxArmor => State.MaxArmor;
        public bool IsDead => State.IsDead;
        public bool CanReceiveHits => adjustment == null || adjustment.CanReceiveHits;
        public event Action<DamageResult> DamageApplied;
        internal static event Action<DamageReceiver> Hit;
        private void Awake() => adjustment = GetComponent<IDamageAdjustment>();
        internal float TryHeal(float amount) => State.TryHeal(amount);
        internal float TryRepair(float amount) => State.TryRepair(amount);
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
            if (!isActiveAndEnabled || !CanReceiveHits || Time.timeAsDouble < protectedUntil) return default;
            var adjusted = adjustment == null ? packet : new DamagePacket(packet.SourceActorId,
                adjustment.AdjustDamage(in packet), packet.DamageType, packet.AttackId, packet.ProtectionSecondsOverride,
                packet.ProjectileHitNormal);
            var result = State.ApplyDamage(in adjusted);
            if (!result.Applied) return result;
            protectedUntil = Time.timeAsDouble + (packet.ProtectionSecondsOverride ?? protectionSeconds);
            if (packet.DamageType == DamageType.Fire && !result.Killed)
                (GetComponent<CityBurn>() ?? gameObject.AddComponent<CityBurn>()).Ignite(packet.SourceActorId);
            DamageApplied?.Invoke(result);
            Hit?.Invoke(this);
            return result;
        }
    }
}
