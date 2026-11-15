using System;
using UnityEngine;
namespace Game.AI
{
    [CreateAssetMenu(menuName="Game/Jumping enemy")]
    public sealed class JumpingEnemyDefinition : ScriptableObject
    {
        public float Health=30, ChaseSpeed=2.5f, TriggerRange=4, Windup=.6f,
            DashSpeed=12, DashDuration=.3f, Recovery=1, Damage=10, HitRadius=.4f, Knockback=3;
        public void Validate()
        {
            foreach(float value in new[]{Health,ChaseSpeed,TriggerRange,Windup,DashSpeed,DashDuration,Recovery,Damage,HitRadius,Knockback})
                if(float.IsNaN(value)||float.IsInfinity(value)||value<=0)throw new ArgumentOutOfRangeException("Jumping enemy parameters must be finite and positive.");
        }
    }
}
