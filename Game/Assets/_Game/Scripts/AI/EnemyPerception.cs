using System;
using System.Collections.Generic;
using Game.Actors;
using UnityEngine;
namespace Game.AI
{
    // Add only to a Stage 2 variant. Its absence preserves Stage 1 behavior.
    [DisallowMultipleComponent]
    public sealed class EnemyPerception : MonoBehaviour
    {
        [SerializeField, Min(.001f)] private float detectionRadius = 8f;
        [SerializeField, Min(.001f)] private float lossRadius = 11f;
        [SerializeField, Min(.001f)] private float memorySeconds = 2f;
        [SerializeField] private LayerMask obstructionMask = ~0;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
        private TargetAwareness state;
        private Transform target;
        private DamageReceiver targetHealth;
        private CoverNavigation coverRoute;
        public bool HasTarget => isActiveAndEnabled && State.HasTarget && TargetAvailable;
        public bool HasLineOfSight { get; private set; }
        public Vector2 PursuitPosition { get; private set; }
        public double SecondsWithoutSight => State.SecondsWithoutSight;
        private TargetAwareness State => state ?? (state = new TargetAwareness(detectionRadius, lossRadius, memorySeconds));
        private bool TargetAvailable => target != null && target.gameObject.activeInHierarchy && targetHealth != null
            && targetHealth.isActiveAndEnabled && !targetHealth.IsDead;
        internal void EnableCoverNavigation()
        {
            coverRoute = new CoverNavigation(GetComponent<Rigidbody2D>());
            state = new TargetAwareness(detectionRadius, lossRadius, Mathf.Max(memorySeconds, 6f));
        }

        internal Vector2 Approach(Vector2 directHeading) =>
            coverRoute == null ? directHeading : coverRoute.Heading(PursuitPosition);
        public void Configure(float detection, float loss, float memory, LayerMask mask)
        {
            if (isActiveAndEnabled) throw new InvalidOperationException("Configure perception before activation.");
            var validated = new TargetAwareness(detection, loss, memory);
            detectionRadius = detection; lossRadius = loss; memorySeconds = memory; obstructionMask = mask;
            state = validated; ResetMemory();
        }
        private void OnEnable() => ResetMemory();
        private void OnDisable() => ResetMemory();
        public void ResetMemory()
        {
            state?.Reset(); target = null; targetHealth = null;
            HasLineOfSight = false; PursuitPosition = default; hits.Clear();
        }
        public bool Tick(Transform player, DamageReceiver health, float scaledDelta)
        {
            if (!isActiveAndEnabled) { ResetMemory(); return false; }
            if (target != player || targetHealth != health) ResetMemory();
            target = player; targetHealth = health;
            if (!TargetAvailable) { ResetMemory(); return false; }
            Vector2 offset = (Vector2)target.position - (Vector2)transform.position;
            HasLineOfSight = offset.sqrMagnitude <= lossRadius * lossRadius && Visible(offset);
            bool tracked = State.Observe(offset.sqrMagnitude, HasLineOfSight, true, scaledDelta);
            if (tracked && (HasLineOfSight || coverRoute != null)) PursuitPosition = target.position;
            if (!tracked) PursuitPosition = default;
            return tracked;
        }
        private bool Visible(Vector2 offset)
        {
            if (offset.sqrMagnitude < .000001f) return true;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstructionMask);
            Physics2D.Raycast(transform.position, offset.normalized, filter, hits, offset.magnitude);
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(target)) continue;
                if (hit.collider.GetComponentInParent<ActorIdentity>() != null || hit.collider.GetComponentInParent<SandboxEnemy>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
