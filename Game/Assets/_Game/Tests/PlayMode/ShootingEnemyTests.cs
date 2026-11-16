#if UNITY_EDITOR
using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests
{
    public sealed class ShootingEnemyTests
    {
        private GameObject hero,foe,root,wall;
        private ShootingEnemy enemy;
        private DamageReceiver health;
        private ProjectilePool pool;
        private Vector2 origin=new Vector2(12000,12000);
        [UnitySetUp] public IEnumerator Setup()
        {
            root=new GameObject("Shooter registry");
            hero=new GameObject("Shooter target");hero.SetActive(false);hero.transform.position=origin+Vector2.right*6;
            hero.AddComponent<ActorIdentity>();hero.AddComponent<CircleCollider2D>().radius=.4f;
            health=hero.AddComponent<DamageReceiver>();health.Configure(1000,0,0);hero.SetActive(true);
            foe=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Stage2/ShootingEnemy.prefab"));
            foe.transform.position=origin;enemy=foe.GetComponent<ShootingEnemy>();pool=foe.GetComponent<ProjectilePool>();
            enemy.Initialize(root.AddComponent<TargetRegistry>(),hero.transform);Physics2D.SyncTransforms();yield return null;
        }
        private IEnumerator WaitForShot()
        {
            float deadline=Time.time+3;
            while(enemy.ShotsFired==0){Assert.That(Time.time,Is.LessThan(deadline));yield return null;}
        }
        [UnityTest] public IEnumerator ShooterCleanupPreservesExternalPoolDisableWithMissingTarget()
        {
            pool.enabled=false;
            enemy.Initialize(root.GetComponent<TargetRegistry>(),null);
            Assert.IsFalse(pool.enabled,"Initialize must preserve the caller's pool state.");
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsFalse(pool.enabled,"Missing-target cleanup must not reopen the pool.");
            enemy.enabled=false; Assert.IsFalse(pool.enabled); Assert.That(pool.ActiveCount,Is.Zero);
        }
        [UnityTest] public IEnumerator RealProjectileDealsTenAndDeathClearsPool()
        {
            yield return WaitForShot();Assert.That(pool.ActiveCount,Is.EqualTo(1));
            yield return new WaitForSeconds(.85f);Assert.That(health.Health,Is.EqualTo(990));
            yield return new WaitForSeconds(1);Assert.That(pool.ActiveCount,Is.GreaterThan(0));
            foe.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,100));
            Assert.That(pool.ActiveCount,Is.Zero);Assert.That(root.GetComponent<TargetRegistry>().Count,Is.Zero);
        }
        [UnityTest] public IEnumerator CoverBlocksBulletAndSubsequentPreparation()
        {
            yield return WaitForShot();wall=new GameObject("Shooter cover");wall.layer=LayerMask.NameToLayer("World");wall.transform.position=origin+Vector2.right*3;
            wall.AddComponent<BoxCollider2D>().size=new Vector2(.5f,5);Physics2D.SyncTransforms();
            yield return new WaitForSeconds(1.5f);Assert.That(health.Health,Is.EqualTo(1000));Assert.That(pool.ActiveCount,Is.Zero);Assert.That(enemy.ShotsFired,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator FullPoolDoesNotCountFalseShotAndDisableRemovesBullets()
        {
            var request=new ProjectileSpawnRequest(origin+Vector2.up*20,Vector2.up,.01f,100,.12f,~0,enemy.ActorId,CombatFaction.Enemy,new DamagePacket(enemy.ActorId,10));
            for(int i=0;i<8;i++)Assert.That(pool.TrySpawn(in request),Is.True);
            yield return new WaitForSeconds(.8f);Assert.That(enemy.ShotsFired,Is.Zero);
            enemy.enabled=false;Assert.That(pool.ActiveCount,Is.Zero);enemy.enabled=true;
            Assert.That(enemy.Phase,Is.EqualTo(ShootingEnemy.AttackPhase.Waiting));
            yield return WaitForShot();hero.SetActive(false);yield return new WaitForFixedUpdate();Assert.That(pool.ActiveCount,Is.Zero);
        }
        [UnityTest] public IEnumerator TargetChangeAndDeathCancelWindupAndPauseFreezesIt()
        {
            yield return new WaitForSeconds(.1f);Assert.That(enemy.Phase,Is.EqualTo(ShootingEnemy.AttackPhase.Windup));
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);Assert.That(enemy.ShotsFired,Is.Zero);Time.timeScale=1;
            enemy.Initialize(root.GetComponent<TargetRegistry>(),null);yield return new WaitForSeconds(.6f);Assert.That(enemy.ShotsFired,Is.Zero);
            enemy.Initialize(root.GetComponent<TargetRegistry>(),hero.transform);yield return new WaitForSeconds(.1f);
            foe.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,100));yield return new WaitForSeconds(.6f);Assert.That(enemy.ShotsFired,Is.Zero);
        }
        [UnityTest] public IEnumerator DistanceControlApproachesAndRetreatsAndDisableCancelsWindup()
        {
            enemy.Initialize(root.GetComponent<TargetRegistry>(),hero.transform);
            hero.transform.position=origin+Vector2.right;Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(foe.GetComponent<Rigidbody2D>().linearVelocity.x,Is.LessThan(0));
            hero.transform.position=origin+Vector2.right*9;Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(foe.GetComponent<Rigidbody2D>().linearVelocity.x,Is.GreaterThan(0));
            hero.transform.position=origin+Vector2.right*5;enemy.Initialize(root.GetComponent<TargetRegistry>(),hero.transform);Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.1f);Assert.That(enemy.Phase,Is.EqualTo(ShootingEnemy.AttackPhase.Windup));
            enemy.enabled=false;yield return new WaitForSeconds(.6f);Assert.That(enemy.ShotsFired,Is.Zero);
        }
        [UnityTest] public IEnumerator NewTargetAndTargetDeathRemoveOwnedProjectiles()
        {
            yield return WaitForShot();
            var replacement=Object.Instantiate(hero,root.transform);replacement.transform.position=origin+Vector2.up*6;
            enemy.Initialize(root.GetComponent<TargetRegistry>(),replacement.transform);Assert.That(pool.ActiveCount,Is.Zero);
            int previous=enemy.ShotsFired;float deadline=Time.time+3;
            while(enemy.ShotsFired==previous){Assert.That(Time.time,Is.LessThan(deadline));yield return null;}
            replacement.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,10000));
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();Assert.That(pool.ActiveCount,Is.Zero);
        }
        [UnityTest] public IEnumerator SceneUnloadRemovesShooterOwnedProjectilesAndRegistrySubscription()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.CreateScene("Shooter unload "+System.Guid.NewGuid().ToString("N"));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(foe,scene);
            yield return WaitForShot();
            var bullets=new System.Collections.Generic.List<Projectile>();
            foreach(var bullet in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                if(bullet.gameObject.scene==scene)bullets.Add(bullet);
            Assert.That(bullets.Count,Is.GreaterThan(0));
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            Assert.That(foe==null,Is.True);
            foreach(var bullet in bullets)Assert.That(bullet==null,Is.True);
            Assert.That(root.GetComponent<TargetRegistry>().Count,Is.Zero);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {Time.timeScale=1;Object.Destroy(foe);Object.Destroy(hero);Object.Destroy(root);if(wall!=null)Object.Destroy(wall);yield return null;}
    }
}
#endif
