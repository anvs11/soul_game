using System;
using UnityEngine;
namespace Game.Combat
{
    public enum DamageType { Normal }
    public readonly struct DamagePacket
    {
        public int SourceActorId { get; }
        public float Amount { get; }
        public DamageType DamageType { get; }
        public long AttackId { get; }
        public float? ProtectionSecondsOverride { get; }
        public Vector2? ProjectileHitNormal { get; }
        public DamagePacket(int sourceActorId, float amount, DamageType damageType = DamageType.Normal,
            long attackId = 0, float? protectionSecondsOverride = null, Vector2? projectileHitNormal = null)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (damageType != DamageType.Normal) throw new ArgumentOutOfRangeException(nameof(damageType));
            if (protectionSecondsOverride.HasValue && (float.IsNaN(protectionSecondsOverride.Value) || float.IsInfinity(protectionSecondsOverride.Value) || protectionSecondsOverride.Value < 0)) throw new ArgumentOutOfRangeException(nameof(protectionSecondsOverride));
            ProtectionSecondsOverride = protectionSecondsOverride;
            ProjectileHitNormal = projectileHitNormal;
            SourceActorId = sourceActorId; Amount = amount; DamageType = damageType; AttackId = attackId;
        }
    }
}
