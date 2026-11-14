using System;
using System.Collections.Generic;
using Game.AI;
using UnityEngine;
namespace Game.Combat
{
    public sealed class TargetRegistry : MonoBehaviour
    {
        internal readonly HashSet<SandboxEnemy> Targets = new HashSet<SandboxEnemy>();
        public int Count => Targets.Count;
        public event Action<SandboxEnemy> Removed;
        public event Action Reactivated;
        private void OnEnable() => Reactivated?.Invoke();
        public void Register(SandboxEnemy enemy)
        {
            if (isActiveAndEnabled && enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead) Targets.Add(enemy);
        }
        public void Unregister(SandboxEnemy enemy)
        {
            if (Targets.Remove(enemy)) Removed?.Invoke(enemy);
        }
        private void OnDisable()
        {
            var previous = new List<SandboxEnemy>(Targets);
            Targets.Clear();
            foreach (var enemy in previous) Removed?.Invoke(enemy);
        }
    }
}
