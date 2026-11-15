using System.Collections.Generic;
using Game.Actors;
using Game.Combat;
using UnityEngine;
namespace Game.AI
{
    [RequireComponent(typeof(DamageReceiver))]
    public sealed class ExplosiveBarrel : MonoBehaviour
    {
        [SerializeField] private float radius = 3.5f, damage = 50, knockback = 3, stun = 2;
        [SerializeField] private LayerMask worldMask;
        private DamageReceiver health;
        private bool exploded;
        private float flash;
        private LineRenderer ring;
        private Material material;
        public int ExplosionCount { get; private set; }
        public void Configure(float range, float amount, float push, float seconds, LayerMask world)
        {
            if (isActiveAndEnabled) throw new System.InvalidOperationException("Configure before activation.");
            foreach (float value in new[] { range, amount, push, seconds })
                if (value <= 0 || float.IsNaN(value) || float.IsInfinity(value)) throw new System.ArgumentOutOfRangeException(nameof(value));
            radius = range; damage = amount; knockback = push; stun = seconds; worldMask = world;
        }
        private void OnEnable() { health = GetComponent<DamageReceiver>(); health.DamageApplied += OnDamage; }
        private void OnDisable() { if (health != null) health.DamageApplied -= OnDamage; if (ring != null) ring.enabled = false; }
        private void OnDamage(DamageResult result)
        {
            if (!result.Killed || exploded) return;
            exploded = true; ExplosionCount++;
            foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            foreach (var skin in GetComponentsInChildren<SpriteRenderer>()) skin.enabled = false;
            Flash();
            var targets = new HashSet<DamageReceiver>();
            foreach (var collider in Physics2D.OverlapCircleAll(transform.position, radius))
            {
                if (collider.isTrigger) continue;
                var receiver = collider.GetComponentInParent<DamageReceiver>();
                if (receiver == null || receiver == health || !receiver.isActiveAndEnabled || receiver.IsDead || receiver.GetComponent<ExplosiveBarrel>() != null) continue;
                if (Vector2.Distance(transform.position, receiver.transform.position) > radius || !targets.Add(receiver)) continue;
                Vector2 offset = receiver.transform.position - transform.position;
                bool blocked = false;
                foreach (var hit in Physics2D.RaycastAll(transform.position, offset.normalized, offset.magnitude, worldMask))
                    if (!hit.collider.isTrigger && hit.collider.GetComponentInParent<ActorIdentity>() == null) blocked = true;
                if (blocked) continue;
                var accepted = receiver.ApplyDamage(new DamagePacket(-1, damage));
                if (!accepted.Applied || accepted.Killed) continue;
                Vector2 direction = offset.sqrMagnitude > .0001f ? offset.normalized : Vector2.up;
                var motor = receiver.GetComponent<ActorMotor2D>();
                if (motor != null)
                {
                    motor.BeginKnockback(direction, knockback, .2f);
                    motor.Stun(stun);
                }
                else if (receiver.TryGetComponent<Rigidbody2D>(out var body) && body.simulated)
                    (receiver.GetComponent<EnemyKnockback>() ?? receiver.gameObject.AddComponent<EnemyKnockback>()).Begin(direction, knockback, .2f);
            }
        }
        private void Flash()
        {
            ring = new GameObject("Explosion ring").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false); ring.useWorldSpace = false; ring.loop = true;
            material = new Material(Shader.Find("Sprites/Default")); ring.sharedMaterial = material;
            ring.startColor = ring.endColor = new Color(1, .45f, .1f); ring.widthMultiplier = .12f; ring.positionCount = 40;
            for (int point = 0; point < 40; point++)
                ring.SetPosition(point, new Vector3(Mathf.Cos(point * Mathf.PI / 20), Mathf.Sin(point * Mathf.PI / 20)) * radius);
            flash = .35f;
        }
        private void Update() { if (flash > 0) { flash -= Time.deltaTime; if (flash <= 0 && ring != null) ring.enabled = false; } }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
