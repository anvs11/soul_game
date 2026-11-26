using System;
using Game.AI;
using UnityEngine;

namespace Game.StreetMap
{
    public sealed class RanchStage : ScriptableObject
    {
        [SerializeField] private RanchDeer deer;
        [SerializeField] private RanchTrap trap;
        [SerializeField, Min(1)] private int ammo = 8;

        internal RanchDeer Deer => deer;
        internal RanchTrap Trap => trap;
        internal int Ammo => ammo;

        internal static RanchStage Load()
        {
            var stage = Resources.Load<RanchStage>("Data/Stages/RanchStage");
            if (stage == null || stage.Deer == null || stage.Trap == null || stage.Ammo < 1)
                throw new InvalidOperationException("Не найдены ресурсы ранчо 5-1.");
            return stage;
        }
    }
}
