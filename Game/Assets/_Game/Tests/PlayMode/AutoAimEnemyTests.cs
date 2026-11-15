using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests
{
    public sealed class AutoAimEnemyTests
    {
        private static SandboxEnemy Enemy(Vector2 position, TargetRegistry registry, Transform player = null)
        {
            var go = new GameObject("Test enemy"); go.SetActive(false); go.transform.position = position;
            go.AddComponent<CircleCollider2D>().radius = .3f;
            go.AddComponent<DamageReceiver>().Configure(30, 0, 0);
            var enemy = go.AddComponent<SandboxEnemy>();
            var body = go.GetComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true;
            enemy.Initialize(registry, player); go.SetActive(true); return enemy;
        }
        [UnityTest] public IEnumerator HiddenNearestIsSkippedAndDeathImmediatelyClearsSelection()
        {
            var root = new GameObject("Aim tests"); var registry = root.AddComponent<TargetRegistry>();
            var selector = root.AddComponent<AutoAimSelector>(); selector.Configure(registry);
            Vector2 origin = new Vector2(3000, 3000);
            var hidden = Enemy(origin + Vector2.right * 2, registry);
            var visible = Enemy(origin + Vector2.up * 3, registry);
            var wall = new GameObject("Visibility wall"); wall.transform.position = origin + Vector2.right;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 1);
            int deaths = 0; hidden.Died += () => deaths++;
            try
            {
                Physics2D.SyncTransforms();
                Assert.That(registry.Count, Is.EqualTo(2));
                Assert.That(selector.Select(origin, 2.5f, 0), Is.Null);
                Assert.That(selector.Select(origin, 5, 0), Is.SameAs(visible));
                visible.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(0, 30));
                Assert.That(selector.CurrentTarget, Is.Null); Assert.That(registry.Count, Is.EqualTo(1));
                Assert.That(selector.Select(origin, 5, 0), Is.Null);
                Object.DestroyImmediate(wall); Physics2D.SyncTransforms();
                Assert.That(selector.Select(origin, 5, 0), Is.SameAs(hidden));
                hidden.gameObject.SetActive(false);
                Assert.That(registry.Count, Is.Zero); Assert.That(selector.CurrentTarget, Is.Null); Assert.That(deaths, Is.Zero);
                hidden.gameObject.SetActive(true); Assert.That(registry.Count, Is.EqualTo(1));
                hidden.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(0, 30));
                hidden.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(0, 30));
                Assert.That(deaths, Is.EqualTo(1)); Assert.That(registry.Count, Is.Zero);
                hidden.gameObject.SetActive(false); hidden.gameObject.SetActive(true);
                Assert.That(deaths, Is.EqualTo(1)); Assert.That(registry.Count, Is.Zero);
                yield return null;
            }
            finally
            {
                if (wall != null) Object.DestroyImmediate(wall);
                Object.DestroyImmediate(hidden.gameObject); Object.DestroyImmediate(visible.gameObject); Object.DestroyImmediate(root);
            }
        }
        [UnityTest] public IEnumerator EqualDistanceUsesActorIdAndRegistryDisableClearsTargets()
        {
            var root = new GameObject("Tie tests"); var registry = root.AddComponent<TargetRegistry>();
            var selector = root.AddComponent<AutoAimSelector>(); selector.Configure(registry);
            Vector2 origin = new Vector2(3100, 3100);
            var a = Enemy(origin + Vector2.left, registry); var b = Enemy(origin + Vector2.right, registry);
            try
            {
                Physics2D.SyncTransforms();
                Assert.That(selector.Select(origin, 1, 0), Is.SameAs(a.ActorId < b.ActorId ? a : b));
                registry.enabled = false;
                Assert.That(registry.Count, Is.Zero); Assert.That(selector.CurrentTarget, Is.Null);
                registry.enabled = true;
                Assert.That(registry.Count, Is.EqualTo(2), "Living enemies must re-register without being re-created.");
                Assert.That(selector.Select(origin, 1, 0), Is.SameAs(a.ActorId < b.ActorId ? a : b));
                yield return null;
            }
            finally { Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject); Object.DestroyImmediate(root); }
        }
        [UnityTest] public IEnumerator WindupDealsOneHitThenRecoversAndPursuitStopsOnDeath()
        {
            var root = new GameObject("Contact registry"); var registry = root.AddComponent<TargetRegistry>();
            var player = new GameObject("Contact player"); player.transform.position = new Vector3(3200, 3200, 0);
            player.AddComponent<BoxCollider2D>(); player.AddComponent<CircleCollider2D>().radius = .4f;
            var health = player.AddComponent<DamageReceiver>();
            var enemy = Enemy(new Vector2(3200.7f, 3200), registry, player.transform);
            try
            {
                Physics2D.SyncTransforms();
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.That(enemy.GetComponent<Rigidbody2D>().linearVelocity.magnitude, Is.LessThanOrEqualTo(2.51f));
                Assert.That(health.Armor, Is.EqualTo(30));
                yield return new WaitForSeconds(.08f); Assert.That(health.Armor, Is.EqualTo(30));
                yield return new WaitForSeconds(.35f); Assert.That(health.Armor, Is.EqualTo(20));
                // Respect real invulnerability without assuming the old attack cadence.
                float deadline = Time.time + 2;
                while (!health.IsDead)
                {
                    Assert.That(Time.time, Is.LessThan(deadline), "Lethal damage never passed protection.");
                    health.ApplyDamage(new DamagePacket(0, 1000));
                    yield return new WaitForFixedUpdate();
                }
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.IsTrue(health.IsDead);
                Assert.That(enemy.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero));
            }
            finally { Object.DestroyImmediate(enemy.gameObject); Object.DestroyImmediate(player); Object.DestroyImmediate(root); }
        }
        [UnityTest] public IEnumerator AimUsesMovementUntilFirstTargetThenKeepsLastTargetDirection()
        {
            var root = new GameObject("Fallback registry"); var registry = root.AddComponent<TargetRegistry>();
            var player = new GameObject("Fallback player"); player.SetActive(false);
            player.transform.position = new Vector3(3300, 3300, 0);
            var input = player.AddComponent<OneShotWeaponInput>();
            var motor = player.AddComponent<ActorMotor2D>(); motor.Configure(input);
            player.GetComponent<Rigidbody2D>().gravityScale = 0;
            var pool = player.AddComponent<ProjectilePool>();
            var controller = player.AddComponent<WeaponController>();
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>(); controller.Configure(definition, pool);
            player.GetComponent<AutoAimSelector>().Configure(registry);
            SandboxEnemy target = null;
            try
            {
                Assert.That(controller.AimDirection, Is.EqualTo(Vector2.down));
                player.SetActive(true); input.Next = new PlayerCommand(Vector2.right);
                yield return null; yield return null;
                Assert.That(controller.AimDirection, Is.EqualTo(Vector2.right));
                target = Enemy((Vector2)player.transform.position + Vector2.up * 2, registry);
                Physics2D.SyncTransforms(); yield return null; yield return null;
                Vector2 remembered = controller.AimDirection; Assert.That(remembered.y, Is.GreaterThan(.99f));
                target.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(0, 30));
                Assert.That(player.GetComponent<AutoAimSelector>().CurrentTarget, Is.Null);
                input.Next = new PlayerCommand(Vector2.left);
                yield return null; yield return null;
                Assert.That(controller.AimDirection, Is.EqualTo(remembered));
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target.gameObject);
                Object.DestroyImmediate(player); Object.DestroyImmediate(definition); Object.DestroyImmediate(root);
            }
        }
    }
}
