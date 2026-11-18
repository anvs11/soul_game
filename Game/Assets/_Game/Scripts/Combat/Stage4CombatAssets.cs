using System;
using Game.AI;
using Game.Encounters;
using UnityEngine;

namespace Game.Combat
{
    public sealed class Stage4CombatAssets : ScriptableObject
    {
        public const string ResourceName = "Stage4CombatAssets";
        [SerializeField] private SlimeGrenadier grenadier;
        [SerializeField] private ShieldShotgun guard;
        [SerializeField] private TeleportNurse nurse;
        [SerializeField] private MiniSlime slime;
        [SerializeField] private RoomPickup ammo, health, armor;

        public bool IsComplete => grenadier != null && guard != null && nurse != null && slime != null &&
            ammo != null && health != null && armor != null;
        public void Configure(SlimeGrenadier a, ShieldShotgun b, TeleportNurse c, MiniSlime child,
            RoomPickup ammoDrop, RoomPickup healthDrop, RoomPickup armorDrop)
        {
            grenadier = a;
            guard = b;
            nurse = c;
            slime = child;
            ammo = ammoDrop;
            health = healthDrop;
            armor = armorDrop;
        }
        public EliteEnemy CreateElite(string type, Transform inactiveParent)
        {
            switch (type)
            {
                case EnemyTypes.SlimeGrenadier: return Instantiate(grenadier, inactiveParent);
                case EnemyTypes.ShieldShotgun: return Instantiate(guard, inactiveParent);
                case EnemyTypes.TeleportNurse: return Instantiate(nurse, inactiveParent);
                default: throw new ArgumentException("Unexpected elite type: " + type, nameof(type));
            }
        }
        public MiniSlime CreateSlime(Transform parent) => Instantiate(slime, parent);
        public RoomPickup CreatePickup(PickupKind kind, Vector2 point, Transform parent)
        {
            var template = kind == PickupKind.Ammo ? ammo : kind == PickupKind.Health ? health : armor;
            return Instantiate(template, point, Quaternion.identity, parent);
        }
    }
}
