using System;
using System.Collections.Generic;
using Game.Actors;
using UnityEngine;
namespace Game.Combat
{
    public sealed class Projectile : MonoBehaviour
    {
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
        private ProjectileSpawnRequest request;
        private Action<Projectile> returnToPool;
        private Vector2 position;
        private float remaining;
        private bool active;
        public void Launch(in ProjectileSpawnRequest value, Action<Projectile> release)
        {
            request = value; position = value.Position; remaining = value.Lifetime;
            returnToPool = release; active = true;
            transform.position = new Vector3(position.x, position.y, 0);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * (value.Radius * 2f);
            gameObject.SetActive(true);
        }
        private void FixedUpdate()
        {
            if (!active) return;
            float step = Mathf.Min(Time.fixedDeltaTime, remaining);
            Vector2 direction = request.Direction.normalized;
            float distance = request.Speed * step;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(request.HitMask);
            Physics2D.CircleCast(position, request.Radius, direction, filter, hits, distance);
            RaycastHit2D closest = default;
            float nearest = float.PositiveInfinity;
            foreach (var hit in hits)
            {
                var identity = hit.collider.GetComponentInParent<ActorIdentity>();
                if (identity != null && (identity.ActorId == request.OwnerActorId ||
                    (request.Faction != CombatFaction.Neutral && identity.Faction == request.Faction))) continue;
                if (hit.distance >= nearest) continue;
                closest = hit; nearest = hit.distance;
            }
            if (closest.collider != null)
            {
                // Disable the hit path before damage can invoke arbitrary scene callbacks.
                active = false;
                var receiver = closest.collider.GetComponentInParent<DamageReceiver>();
                try { if (receiver != null) receiver.ApplyDamage(request.Damage); }
                finally { Despawn(); }
                return;
            }
            position += direction * distance;
            transform.position = new Vector3(position.x, position.y, 0);
            remaining -= step;
            if (remaining <= 0) Despawn();
        }
        public void Despawn()
        {
            var release = returnToPool;
            ResetState();
            release?.Invoke(this);
        }
        public void ResetState()
        {
            active = false; remaining = 0; position = default; request = default;
            returnToPool = null; hits.Clear();
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }
    }
}
