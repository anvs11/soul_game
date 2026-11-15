using UnityEngine;
namespace Game.AI
{
    [CreateAssetMenu(menuName="Game/Police boss")]
    public sealed class PoliceBossDefinition : ScriptableObject
    {
        public float Health = 600, Windup = .7f, RollSpeed = 10, RollDuration = .65f;
        public float RollDamage = 20, ShotDamage = 15, ShotSpeed = 7, ShotLifetime = 4;
        public float PairInterval = .12f, Reload = 2, SpinDuration = 3, SpinInterval = .08f;
        public int MagazinePerGun = 8, PoolLimit = 128, SummonLimit = 6;
        public void Validate()
        {
            foreach (float value in new[] { Health, Windup, RollSpeed, RollDuration, RollDamage, ShotDamage, ShotSpeed, ShotLifetime, PairInterval, Reload, SpinDuration, SpinInterval })
                if (value <= 0 || float.IsNaN(value) || float.IsInfinity(value)) throw new System.ArgumentOutOfRangeException(nameof(value));
            if (MagazinePerGun < 1 || PoolLimit < 2 || SummonLimit < 1) throw new System.ArgumentOutOfRangeException(nameof(MagazinePerGun));
        }
    }
}
