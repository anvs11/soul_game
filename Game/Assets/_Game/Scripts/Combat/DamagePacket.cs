using System;
namespace Game.Combat
{
    public enum DamageType { Normal }
    public readonly struct DamagePacket
    {
        public int SourceActorId { get; }
        public float Amount { get; }
        public DamageType DamageType { get; }
        public long AttackId { get; }
        public DamagePacket(int sourceActorId, float amount, DamageType damageType = DamageType.Normal, long attackId = 0)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (damageType != DamageType.Normal) throw new ArgumentOutOfRangeException(nameof(damageType));
            SourceActorId = sourceActorId; Amount = amount; DamageType = damageType; AttackId = attackId;
        }
    }
}
