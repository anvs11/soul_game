using UnityEngine;
namespace Game.Combat
{
    [CreateAssetMenu(menuName = "Game/Weapons/Pistol")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField, Min(1)] private int magazineCapacity = 8;
        [SerializeField, Min(0)] private int initialReserve = 16;
        [SerializeField, Min(.001f)] private float damage = 10f;
        [SerializeField, Min(.001f)] private float shotInterval = .25f;
        [SerializeField, Min(.001f)] private float reloadSeconds = 1f;
        [SerializeField, Min(.001f)] private float projectileSpeed = 25f;
        [SerializeField, Min(.001f)] private float projectileLifetime = 2f;
        [SerializeField, Min(.001f)] private float projectileRadius = .08f;
        public float TargetRange => projectileSpeed * projectileLifetime;
        public int MagazineCapacity => magazineCapacity;
        public int InitialReserve => initialReserve;
        public float Damage => damage;
        public float ShotInterval => shotInterval;
        public float ReloadSeconds => reloadSeconds;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileRadius => projectileRadius;
    }
}
