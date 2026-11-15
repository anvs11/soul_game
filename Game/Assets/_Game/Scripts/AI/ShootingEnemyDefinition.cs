using System;
using UnityEngine;
namespace Game.AI
{
    [CreateAssetMenu(menuName="Game/Shooting enemy")]
    public sealed class ShootingEnemyDefinition : ScriptableObject
    {
        public float Health=30;
        public float Speed=2.5f;
        public float PreferredRange=6;
        public float MinimumRange=4;
        public float FireRange=8;
        public float Windup=.5f;
        public float Interval=1.2f;
        public float ProjectileSpeed=8;
        public float ProjectileLifetime=3;
        public float Damage=10;
        public void Validate()
        {
            foreach(float value in new[]{Health,Speed,PreferredRange,MinimumRange,FireRange,Windup,Interval,ProjectileSpeed,ProjectileLifetime,Damage})
                if(float.IsNaN(value)||float.IsInfinity(value)||value<=0)throw new ArgumentOutOfRangeException(nameof(value));
            if(MinimumRange>=PreferredRange||PreferredRange>FireRange)throw new ArgumentException("Expected minimum < preferred <= fire range.");
        }
    }
}
