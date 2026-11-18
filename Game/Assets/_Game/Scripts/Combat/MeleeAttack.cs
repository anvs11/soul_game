using System.Collections.Generic;
using Game.Actors;
using UnityEngine;
namespace Game.Combat
{
    [DisallowMultipleComponent]
    public sealed class MeleeAttack : MonoBehaviour
    {
        public const float Damage = 15f;
        public const float Radius = 1.2f;
        public const float SectorDegrees = 100f;
        private static readonly float MinimumDot = Mathf.Cos(SectorDegrees * .5f * Mathf.Deg2Rad);
        private readonly List<Collider2D> candidates = new List<Collider2D>(16);
        private readonly List<RaycastHit2D> obstacles = new List<RaycastHit2D>(16);
        public int LastSwingTargetCount { get; private set; }
        private readonly HashSet<int> hitActors = new HashSet<int>();
        private readonly HashSet<DamageReceiver> hitReceivers = new HashSet<DamageReceiver>();
        public bool TryAttack(WeaponRuntimeState state, Vector2 origin, Vector2 direction,
            int ownerActorId, CombatFaction faction, LayerMask mask, long attackId)
        {
            if (!isActiveAndEnabled || state == null || direction.sqrMagnitude < .000001f) return false;
            if (!state.TryMeleeAttack()) return false;
            LastSwingTargetCount = 0;
            direction.Normalize();
            hitActors.Clear(); hitReceivers.Clear();
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(mask);
            Physics2D.OverlapCircle(origin, Radius, filter, candidates);
            var packet = new DamagePacket(ownerActorId, Damage, attackId: attackId);
            foreach (var collider in candidates)
            {
                if (collider == null) continue;
                var receiver = collider.GetComponentInParent<DamageReceiver>();
                if (receiver == null || receiver.IsDead || !receiver.CanReceiveHits || hitReceivers.Contains(receiver)) continue;
                var actor = collider.GetComponentInParent<ActorIdentity>();
                if (actor != null && (actor.ActorId == ownerActorId || hitActors.Contains(actor.ActorId) ||
                    (faction != CombatFaction.Neutral && actor.Faction == faction))) continue;
                Vector2 offset = collider.ClosestPoint(origin) - origin;
                float distance = offset.magnitude;
                if (distance > Radius || (distance > .00001f && Vector2.Dot(direction, offset / distance) < MinimumDot)) continue;
                if (Blocked(origin, offset, distance, receiver, ownerActorId, faction, filter)) continue;
                hitReceivers.Add(receiver);
                if (actor != null) hitActors.Add(actor.ActorId);
                LastSwingTargetCount++;
                receiver.ApplyDamage(in packet);
            }
            candidates.Clear(); obstacles.Clear(); hitActors.Clear(); hitReceivers.Clear();
            return true;
        }
        private bool Blocked(Vector2 origin, Vector2 offset, float distance, DamageReceiver target,
            int owner, CombatFaction faction, ContactFilter2D filter)
        {
            if (distance <= .00001f) return false;
            Physics2D.Raycast(origin, offset / distance, filter, obstacles, distance);
            foreach (var hit in obstacles)
            {
                if (hit.collider == null) continue;
                var receiver = hit.collider.GetComponentInParent<DamageReceiver>();
                if (receiver == target || (receiver != null && !receiver.CanReceiveHits)) continue;
                var actor = hit.collider.GetComponentInParent<ActorIdentity>();
                if (actor != null && (actor.ActorId == owner ||
                    (faction != CombatFaction.Neutral && actor.Faction == faction))) continue;
                // Only an earlier obstacle blocks the segment to the chosen target point.
                if (hit.distance < distance - .0001f) return true;
            }
            return false;
        }
    }
}
