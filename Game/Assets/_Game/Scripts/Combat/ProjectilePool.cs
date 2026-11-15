using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
namespace Game.Combat
{
    public sealed class ProjectilePool : MonoBehaviour, IProjectileSpawner
    {
        [SerializeField] private Projectile prefab;
        [SerializeField, Min(1)] private int maxActive = 128;
        private readonly HashSet<Projectile> active = new HashSet<Projectile>();
        private ObjectPool<Projectile> pool;
        private Transform container;
        public int ActiveCount => active.Count;
        public int TotalCount => pool?.CountAll ?? 0;
        public void Configure(Projectile template, int limit)
        {
            if (pool != null) throw new System.InvalidOperationException("Pool is already initialized.");
            prefab = template; maxActive = Mathf.Max(1, limit);
        }
        public bool TrySpawn(in ProjectileSpawnRequest request)
        {
            if (!isActiveAndEnabled || prefab == null || active.Count >= maxActive ||
                !Finite(request.Position.x) || !Finite(request.Position.y) ||
                !Finite(request.Direction.x) || !Finite(request.Direction.y) || request.Direction.sqrMagnitude < .000001f ||
                !Positive(request.Speed) || !Positive(request.Lifetime) || !Positive(request.Radius)) return false;
            EnsurePool();
            var projectile = pool.Get();
            active.Add(projectile);
            projectile.Launch(in request, Release);
            return true;
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool Positive(float v) => Finite(v) && v > 0;
        private void EnsurePool()
        {
            if (pool != null) return;
            var root = new GameObject("Projectile pool");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            container = root.transform; // Not parented to moving/scaled player.
            pool = new ObjectPool<Projectile>(() => Instantiate(prefab, container),
                null, p => p.ResetState(), p => { if (p != null) Destroy(p.gameObject); }, true, 16, maxActive);
        }
        private void Release(Projectile projectile)
        {
            if (!active.Remove(projectile)) return;
            pool.Release(projectile);
        }
        private void OnDisable() => ClearActive();
        public void ClearActive()
        {
            // Despawn removes its own entry; no enumerator is kept over the changing set.
            while (active.Count > 0)
            {
                Projectile next = null;
                foreach (var item in active) { next = item; break; }
                if (next == null) { active.Remove(next); continue; }
                next.Despawn();
            }
        }
        private void OnDestroy()
        {
            pool?.Clear();
            if (container != null) Destroy(container.gameObject);
        }
    }
}
