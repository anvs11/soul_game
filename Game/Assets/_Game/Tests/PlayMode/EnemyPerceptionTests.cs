using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests
{
    public sealed class EnemyPerceptionTests
    {
        private GameObject observer, player, cover, registryObject;
        private EnemyPerception perception;
        private DamageReceiver playerHealth;
        private readonly Vector2 origin = new Vector2(6000, 6000);
        private void Setup()
        {
            observer = new GameObject("Perception observer"); observer.SetActive(false); observer.transform.position = origin;
            observer.AddComponent<CircleCollider2D>().radius = .3f;
            perception = observer.AddComponent<EnemyPerception>(); perception.Configure(8, 11, 2, ~0);
            player = new GameObject("Perception player"); player.SetActive(false); player.transform.position = origin + Vector2.right * 8;
            player.AddComponent<CircleCollider2D>().radius = .3f;
            playerHealth = player.AddComponent<DamageReceiver>(); playerHealth.Configure(100, 0, 0);
            player.SetActive(true); observer.SetActive(true); Physics2D.SyncTransforms();
        }
        private void Wall()
        {
            cover = new GameObject("Cover"); cover.transform.position = origin + Vector2.right * 4;
            cover.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 4); Physics2D.SyncTransforms();
        }
        [UnityTest] public IEnumerator SolidCoverBlocksButOwnActorTargetAndTriggersDoNot()
        {
            Setup();
            Assert.IsTrue(perception.Tick(player.transform, playerHealth, 0), "Own and target colliders must be ignored at exact radius.");
            Wall(); perception.ResetMemory(); Assert.IsFalse(perception.Tick(player.transform, playerHealth, 0));
            cover.GetComponent<BoxCollider2D>().isTrigger = true; Physics2D.SyncTransforms();
            Assert.IsTrue(perception.Tick(player.transform, playerHealth, 0));
            perception.ResetMemory(); cover.GetComponent<BoxCollider2D>().isTrigger = false;
            cover.AddComponent<ActorIdentity>(); Physics2D.SyncTransforms();
            Assert.IsTrue(perception.Tick(player.transform, playerHealth, 0), "Another actor must not occlude discovery.");
            yield return null;
        }
        [UnityTest] public IEnumerator MemoryUsesLastSeenPositionAndLifecycleClearsIt()
        {
            Setup(); Assert.IsTrue(perception.Tick(player.transform, playerHealth, 0));
            Vector2 remembered = perception.PursuitPosition; Wall();
            player.transform.position = origin + Vector2.right * 9; Physics2D.SyncTransforms();
            Assert.IsTrue(perception.Tick(player.transform, playerHealth, 1));
            Assert.That(perception.PursuitPosition, Is.EqualTo(remembered)); Assert.IsFalse(perception.HasLineOfSight);
            Assert.IsFalse(perception.Tick(player.transform, playerHealth, 1));
            cover.SetActive(false); player.transform.position = origin + Vector2.right * 8; Physics2D.SyncTransforms();
            Assert.IsTrue(perception.Tick(player.transform, playerHealth, 0));
            perception.enabled = false; Assert.IsFalse(perception.HasTarget);
            perception.enabled = true; Assert.IsFalse(perception.HasTarget);
            Assert.IsTrue(perception.Tick(player.transform, playerHealth, 0));
            player.SetActive(false); Assert.IsFalse(perception.Tick(player.transform, playerHealth, 0));
            player.SetActive(true); Assert.IsTrue(perception.Tick(player.transform, playerHealth, 0));
            playerHealth.ApplyDamage(new DamagePacket(0, 1000));
            Assert.IsFalse(perception.HasTarget); Assert.IsFalse(perception.Tick(player.transform, playerHealth, 0));
            yield return null;
        }
        private SandboxEnemy BindEnemy()
        {
            observer.SetActive(false);
            observer.AddComponent<DamageReceiver>().Configure(30, 0, 0);
            var enemy = observer.AddComponent<SandboxEnemy>(); observer.GetComponent<Rigidbody2D>().gravityScale = 0;
            observer.GetComponent<Rigidbody2D>().freezeRotation = true;
            registryObject = new GameObject("Perception registry");
            enemy.Initialize(registryObject.AddComponent<TargetRegistry>(), player.transform);
            observer.SetActive(true); return enemy;
        }
        [UnityTest] public IEnumerator PauseFreezesMemoryAndReinitializeAndDisableResetEnemy()
        {
            Setup(); var enemy = BindEnemy();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); Assert.IsTrue(perception.HasTarget);
            Wall();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            float previousScale = Time.timeScale;
            double before = perception.SecondsWithoutSight; Vector3 position = observer.transform.position;
            try
            {
                Time.timeScale = 0;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(perception.SecondsWithoutSight, Is.EqualTo(before)); Assert.That(observer.transform.position, Is.EqualTo(position));
            }
            finally { Time.timeScale = previousScale; }
            enemy.Initialize(registryObject.GetComponent<TargetRegistry>(), player.transform);
            Assert.IsFalse(perception.HasTarget);
            cover.SetActive(false); Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); Assert.IsTrue(perception.HasTarget);
            int deaths = 0; enemy.Died += () => deaths++;
            observer.SetActive(false); Assert.IsFalse(perception.HasTarget); Assert.That(deaths, Is.Zero);
            observer.SetActive(true); Assert.IsFalse(perception.HasTarget);
        }
        [UnityTest] public IEnumerator IdleBeforeDetectionThenPreservedWindupDealsTenAndDeathStops()
        {
            Setup(); player.transform.position = origin + Vector2.right * 9;
            var enemy = BindEnemy(); Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsFalse(perception.HasTarget); Assert.That(observer.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero));
            player.transform.position = origin + Vector2.right; Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.08f); Assert.IsTrue(perception.HasTarget);
            Assert.That(enemy.Phase, Is.EqualTo(SandboxEnemy.AttackPhase.Windup)); Assert.That(playerHealth.Health, Is.EqualTo(100));
            yield return new WaitForSeconds(.3f); Assert.That(playerHealth.Health, Is.EqualTo(90));
            observer.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(0, 30));
            Assert.IsFalse(perception.HasTarget); Assert.That(registryObject.GetComponent<TargetRegistry>().Count, Is.Zero);
            yield return new WaitForFixedUpdate(); Assert.That(observer.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero));
        }
        [UnityTest] public IEnumerator TargetDisableAndDeathCancelPendingWindupWithoutKillingEnemy()
        {
            Setup(); player.transform.position = origin + Vector2.right;
            var enemy = BindEnemy(); Physics2D.SyncTransforms();
            int enemyDeaths = 0; enemy.Died += () => enemyDeaths++;
            yield return new WaitForSeconds(.08f);
            Assert.That(enemy.Phase, Is.EqualTo(SandboxEnemy.AttackPhase.Windup));
            player.SetActive(false);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsFalse(perception.HasTarget); Assert.That(enemy.Phase, Is.EqualTo(SandboxEnemy.AttackPhase.Chase));
            player.SetActive(true); Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.08f);
            Assert.That(playerHealth.Health, Is.EqualTo(100), "Re-enabled target must get a fresh windup.");
            playerHealth.ApplyDamage(new DamagePacket(0, 1000));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsFalse(perception.HasTarget); Assert.That(enemy.Phase, Is.EqualTo(SandboxEnemy.AttackPhase.Chase));
            Assert.That(enemyDeaths, Is.Zero);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (observer != null) Object.Destroy(observer);
            if (player != null) Object.Destroy(player);
            if (cover != null) Object.Destroy(cover);
            if (registryObject != null) Object.Destroy(registryObject);
            yield return null;
        }
    }
}
