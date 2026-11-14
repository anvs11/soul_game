using System.Collections;
using Game.Actors;
using Game.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests
{
    public sealed class ProjectileTests
    {
        [UnityTest] public IEnumerator FullPoolPreservesAmmoAndDisableReturnsAllProjectiles()
        {
            var template = new GameObject("Pool template"); template.SetActive(false);
            var projectile = template.AddComponent<Projectile>();
            var host = new GameObject("Bounded pool"); var pool = host.AddComponent<ProjectilePool>();
            pool.Configure(projectile, 1);
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            var state = new WeaponRuntimeState(definition);
            var request = new ProjectileSpawnRequest(new Vector2(5000,5000), Vector2.right, 1, 10, .08f, 0, 1, CombatFaction.Player, new DamagePacket(1,10));
            try
            {
                Assert.IsTrue(state.TryFire(pool, in request)); state.Advance(.25f);
                Assert.IsFalse(state.TryFire(pool, in request)); Assert.That(state.Magazine, Is.EqualTo(7));
                pool.enabled = false; Assert.That(pool.ActiveCount, Is.Zero);
                pool.enabled = true;
                Assert.IsTrue(state.TryFire(pool, in request)); Assert.That(state.Magazine, Is.EqualTo(6));
                Assert.That(pool.TotalCount, Is.EqualTo(1));
                pool.enabled = false; pool.enabled = true;
                Assert.That(pool.ActiveCount, Is.Zero);
                yield return null;
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(template); Object.DestroyImmediate(definition); }
        }
        [UnityTest] public IEnumerator FastProjectileStopsAtWallThenReusedProjectileHitsTargetOnce()
        {
            var template = new GameObject("Projectile template"); template.SetActive(false);
            var projectile = template.AddComponent<Projectile>();
            var host = new GameObject("Test pool");
            var pool = host.AddComponent<ProjectilePool>(); pool.Configure(projectile, 1);
            var wall = new GameObject("Wall"); wall.transform.position = new Vector3(1002, 1000, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.1f, 4);
            var target = new GameObject("Target"); target.SetActive(false); target.transform.position = new Vector3(1004, 1000, 0);
            target.AddComponent<BoxCollider2D>(); target.AddComponent<CircleCollider2D>();
            var receiver = target.AddComponent<DamageReceiver>();
            receiver.Configure(100, 30, 0); target.SetActive(true);
            var request = new ProjectileSpawnRequest(new Vector2(1000,1000), Vector2.right, 1000, 1, .08f, ~0, 1, CombatFaction.Player, new DamagePacket(1,10));
            try
            {
                Physics2D.SyncTransforms();
                Assert.IsTrue(pool.TrySpawn(in request)); Assert.IsFalse(pool.TrySpawn(in request));
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.That(pool.ActiveCount, Is.Zero); Assert.That(receiver.Armor, Is.EqualTo(30));
                Object.DestroyImmediate(wall); Physics2D.SyncTransforms();
                int hits = 0; receiver.DamageApplied += _ => hits++;
                Assert.IsTrue(pool.TrySpawn(in request));
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.That(pool.TotalCount, Is.EqualTo(1), "The stored projectile must be reused.");
                Assert.That(hits, Is.EqualTo(1)); Assert.That(receiver.Armor, Is.EqualTo(20));
                Assert.That(pool.ActiveCount, Is.Zero);
                var miss = new ProjectileSpawnRequest(new Vector2(1000,1000), Vector2.left, 1000, .02f, .08f, ~0, 2, CombatFaction.Enemy, new DamagePacket(2,99));
                Assert.IsTrue(pool.TrySpawn(in miss));
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.That(receiver.Armor, Is.EqualTo(20)); Assert.That(pool.ActiveCount, Is.Zero);
                var newDamage = new ProjectileSpawnRequest(new Vector2(1000,1000), Vector2.right, 1000, 1, .08f, ~0, 3, CombatFaction.Enemy, new DamagePacket(3,25));
                Assert.IsTrue(pool.TrySpawn(in newDamage));
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.That(receiver.Armor, Is.Zero); Assert.That(receiver.Health, Is.EqualTo(95));
                Assert.That(hits, Is.EqualTo(2)); Assert.That(pool.TotalCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(template);
                if (wall != null) Object.DestroyImmediate(wall); Object.DestroyImmediate(target);
            }
        }
    }
}
