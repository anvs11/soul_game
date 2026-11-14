using UnityEngine;
namespace Game.Combat
{
    public enum CombatFaction { Neutral, Player, Enemy }
    public readonly struct ProjectileSpawnRequest
    {
        public readonly Vector2 Position, Direction;
        public readonly float Speed, Lifetime, Radius;
        public readonly LayerMask HitMask;
        public readonly int OwnerActorId;
        public readonly CombatFaction Faction;
        public readonly DamagePacket Damage;
        public ProjectileSpawnRequest(Vector2 position, Vector2 direction, float speed, float lifetime,
            float radius, LayerMask hitMask, int ownerActorId, CombatFaction faction, DamagePacket damage)
        {
            Position = position; Direction = direction; Speed = speed; Lifetime = lifetime;
            Radius = radius; HitMask = hitMask; OwnerActorId = ownerActorId; Faction = faction; Damage = damage;
        }
    }
    public interface IProjectileSpawner { bool TrySpawn(in ProjectileSpawnRequest request); }
}
