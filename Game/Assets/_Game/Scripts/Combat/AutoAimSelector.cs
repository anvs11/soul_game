using System.Collections.Generic;
using Game.Actors;
using Game.AI;
using UnityEngine;
namespace Game.Combat
{
    [DisallowMultipleComponent]
    public sealed class AutoAimSelector : MonoBehaviour
    {
        [SerializeField] private TargetRegistry registry;
        [SerializeField] private LayerMask obstructionMask = ~0;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
        private SandboxEnemy current;
        public SandboxEnemy CurrentTarget => current != null && current.isActiveAndEnabled && !current.IsDead ? current : null;
        public void Configure(TargetRegistry value)
        {
            if (registry != null) registry.Removed -= OnRemoved;
            registry = value; current = null;
            if (isActiveAndEnabled && registry != null) registry.Removed += OnRemoved;
        }
        private void OnEnable() { if (registry != null) registry.Removed += OnRemoved; }
        private void OnDisable() { if (registry != null) registry.Removed -= OnRemoved; current = null; hits.Clear(); }
        private void OnRemoved(SandboxEnemy value) { if (current == value) current = null; }
        public SandboxEnemy Select(Vector2 origin, float radius, int ownerActorId)
        {
            current = null;
            if (!isActiveAndEnabled || registry == null || !registry.isActiveAndEnabled || radius <= 0) return null;
            float bestDistance = radius * radius;
            int bestId = int.MaxValue;
            foreach (var candidate in registry.Targets)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsDead || candidate.ActorId == ownerActorId) continue;
                Vector2 offset = (Vector2)candidate.transform.position - origin;
                float distance = offset.sqrMagnitude;
                if (distance > radius * radius) continue;
                if (current != null && (distance > bestDistance || (distance == bestDistance && candidate.ActorId >= bestId))) continue;
                if (!Visible(origin, offset)) continue;
                current = candidate; bestDistance = distance; bestId = candidate.ActorId;
            }
            return current;
        }
        private bool Visible(Vector2 origin, Vector2 offset)
        {
            if (offset.sqrMagnitude < .000001f) return true;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstructionMask);
            Physics2D.Raycast(origin, offset.normalized, filter, hits, offset.magnitude);
            foreach (var hit in hits)
            {
                // Actors do not occlude auto-aim; solid world geometry does.
                if (hit.collider != null && hit.collider.GetComponentInParent<ActorIdentity>() == null) return false;
            }
            return true;
        }
    }
}
