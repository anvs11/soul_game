using System.Collections.Generic;
using UnityEngine;
namespace Game.Actors
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyKnockback : MonoBehaviour
    {
        private float remaining, speed;
        private bool wasActive;
        private Vector2 direction;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        public void Begin(Vector2 value, float distance, float duration)
        {
            if (distance < 0 || duration <= 0 || float.IsNaN(distance) || float.IsInfinity(distance))
                throw new System.ArgumentOutOfRangeException(nameof(distance));
            direction = value.normalized;
            speed = distance / duration;
            remaining = duration; wasActive = true;
        }
        private void FixedUpdate()
        {
            var body = GetComponent<Rigidbody2D>();
            var health = GetComponent<DamageReceiver>();
            if (remaining <= 0 || health == null || health.IsDead)
            { if (wasActive) { body.linearVelocity = Vector2.zero; wasActive = false; } return; }
            float distance = speed * Mathf.Min(remaining, Time.fixedDeltaTime);
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(~0);
            body.Cast(direction, filter, hits, distance + .02f);
            foreach (var hit in hits)
                if (hit.collider != null && hit.collider.GetComponentInParent<ActorIdentity>() == null)
                    distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - .02f));
            body.linearVelocity = direction * (distance / Time.fixedDeltaTime);
            remaining -= Time.fixedDeltaTime;
        }
        private void OnDisable() { remaining = 0; }
    }
}
