using System;
using Game.AI;
using Game.Combat;
using Game.Encounters;
using UnityEngine;

namespace Game.StreetMap
{
    public sealed class FixedEncounterPrefabs : ScriptableObject
    {
        public const string ResourceName = "FixedEncounterPrefabs";
        [SerializeField] private SandboxEnemy melee;
        [SerializeField] private JumpingEnemy jumper;
        [SerializeField] private ShootingEnemy shooter;
        public bool IsComplete => melee != null && jumper != null && shooter != null;
        public void Configure(SandboxEnemy close, JumpingEnemy jump, ShootingEnemy ranged)
        {
            melee = close;
            jumper = jump;
            shooter = ranged;
        }
        public MonoBehaviour InstantiateActor(string type, Transform inactiveParent)
        {
            switch (type)
            {
                case EnemyTypes.Melee: return Instantiate(melee, inactiveParent);
                case EnemyTypes.Jumper: return Instantiate(jumper, inactiveParent);
                case EnemyTypes.Shooter: return Instantiate(shooter, inactiveParent);
                default: throw new ArgumentException("Unknown runtime enemy type: " + type);
            }
        }
    }
}
