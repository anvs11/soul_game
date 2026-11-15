using System;
using System.Collections.Generic;
using Game.AI;
using UnityEngine;
namespace Game.Combat
{
    public sealed class TargetRegistry : MonoBehaviour
    {
        internal readonly HashSet<IEnemyTarget> Targets = new HashSet<IEnemyTarget>();
        public int Count => Targets.Count;
        public event Action<IEnemyTarget> Removed;
        public event Action Reactivated;
        private void OnEnable() => Reactivated?.Invoke();
        public void Register(IEnemyTarget enemy)
        {
            if (isActiveAndEnabled && EnemyTargets.Active(enemy)) Targets.Add(enemy);
        }
        public void Unregister(IEnemyTarget enemy)
        {
            if (Targets.Remove(enemy)) Removed?.Invoke(enemy);
        }
        private void OnDisable()
        {
            var previous = new List<IEnemyTarget>(Targets);
            Targets.Clear();
            foreach (var enemy in previous) Removed?.Invoke(enemy);
        }
    }
}
