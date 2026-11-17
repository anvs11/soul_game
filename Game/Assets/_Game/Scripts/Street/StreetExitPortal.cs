using Game.Actors;
using UnityEngine;
namespace Game.Street
{
    public sealed class StreetExitPortal : MonoBehaviour
    {
        private StreetModuleDefinition definition;
        private Material material;
        public bool IsOpen { get; private set; }
        public void Initialize(StreetModuleDefinition data)
        {
            definition = data;
            GetComponent<SpriteRenderer>().enabled = false;
        }
        public bool TryOpen(PlayerCombatFacade hero)
        {
            if (IsOpen) return true;
            if (hero == null || hero.IsDead) return false;
            Vector2 origin = hero.transform.position;
            for (int ring = 0; ring < 2; ring++)
            {
                for (int step = 0; step < 8; step++)
                {
                    float angle = step * Mathf.PI / 4;
                    Vector2 point = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (2.5f + ring);
                    if (Available(origin, point)) { Open(point); return true; }
                }
            }
            float bestDistance = float.PositiveInfinity;
            Vector2 best = default;
            for (int y = 0; y < definition.Height; y++)
            {
                for (int x = 0; x < definition.Width; x++)
                {
                    Vector2 point = definition.CellCenter(new Vector2Int(x, y));
                    float distance = (point - origin).sqrMagnitude;
                    if (distance < 4 || distance >= bestDistance || !Available(origin, point)) continue;
                    bestDistance = distance;
                    best = point;
                }
            }
            if (float.IsPositiveInfinity(bestDistance)) return false;
            Open(best);
            return true;
        }
        private bool Available(Vector2 origin, Vector2 point)
        {
            // Bound by the authored module as well as the physical walls.
            Vector2 local = point - definition.GridOrigin;
            if (local.x < .9f || local.y < .9f || local.x > definition.Width * definition.CellSize - .9f ||
                local.y > definition.Height * definition.CellSize - .9f) return false;
            Vector2 offset = point - origin;
            if (Physics2D.Raycast(origin, offset.normalized, offset.magnitude, LayerMask.GetMask("World"))) return false;
            foreach (var collider in Physics2D.OverlapCircleAll(point, .65f))
            {
                if (collider.isTrigger) continue;
                if (collider.gameObject.layer == LayerMask.NameToLayer("World")) return false;
                var receiver = collider.GetComponentInParent<DamageReceiver>();
                if (receiver != null && !receiver.IsDead) return false;
            }
            return true;
        }
        private void Open(Vector2 point)
        {
            transform.position = point;
            IsOpen = true;
            var marker = GetComponent<SpriteRenderer>();
            marker.enabled = true;
            marker.color = new Color(.2f, .8f, 1);
            var ring = gameObject.AddComponent<LineRenderer>();
            material = new Material(Shader.Find("Sprites/Default"));
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = .12f;
            ring.startColor = ring.endColor = Color.cyan;
            ring.sortingOrder = 3;
            ring.positionCount = 32;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI / 16;
                // Exit transform has an authored .8 scale.
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .8f);
            }
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
