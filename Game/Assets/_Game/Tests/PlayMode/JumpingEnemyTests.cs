using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Game.Tests
{
    public sealed class JumpingEnemyTests
    {
        private GameObject hero,foe,root,wall;
        private JumpingEnemy enemy;
        private DamageReceiver health;
        private JumpingEnemyDefinition definition;
        private TargetRegistry registry;
        private readonly Vector2 origin=new Vector2(9000,9000);
        private void Setup(float protection=0)
        {
            root=new GameObject("Jump registry");registry=root.AddComponent<TargetRegistry>();
            hero=new GameObject("Jump target");hero.SetActive(false);hero.transform.position=origin+Vector2.right*3;
            hero.AddComponent<CircleCollider2D>().radius=.4f;hero.AddComponent<BoxCollider2D>().size=Vector2.one*.6f;
            hero.AddComponent<ActorIdentity>();health=hero.AddComponent<DamageReceiver>();health.Configure(1000,0,protection);hero.SetActive(true);
            foe=new GameObject("Test jumper");foe.SetActive(false);foe.transform.position=origin;
            var body=foe.AddComponent<Rigidbody2D>();body.gravityScale=0;body.freezeRotation=true;
            foe.AddComponent<CircleCollider2D>().radius=.4f;
            var identity=foe.AddComponent<ActorIdentity>();JsonUtility.FromJsonOverwrite("{\"faction\":2}",identity);
            foe.AddComponent<DamageReceiver>().Configure(30,0,0);foe.AddComponent<EnemyPerception>().Configure(8,11,2,~0);
            definition=ScriptableObject.CreateInstance<JumpingEnemyDefinition>();
            enemy=foe.AddComponent<JumpingEnemy>();enemy.Configure(definition,~0,~0);enemy.Initialize(registry,hero.transform);foe.SetActive(true);Physics2D.SyncTransforms();
        }
        private IEnumerator Until(JumpingEnemy.AttackPhase phase)
        {
            float deadline=Time.realtimeSinceStartup+4;
            while(enemy.Phase!=phase){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return new WaitForFixedUpdate();}
        }
        [UnityTest] public IEnumerator OneDashHitsMultipleCollidersOnlyOnceAndRecovers()
        {
            Setup();int hits=0;health.DamageApplied+=_=>hits++;
            yield return Until(JumpingEnemy.AttackPhase.Recovery);
            Assert.That(health.Health,Is.EqualTo(990));Assert.That(hits,Is.EqualTo(1));
            Assert.That(Vector2.Distance(foe.transform.position,origin),Is.LessThanOrEqualTo(3.85f));
            yield return new WaitForSeconds(.5f);Assert.That(enemy.Phase,Is.EqualTo(JumpingEnemy.AttackPhase.Recovery));Assert.That(hits,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator LockedDirectionAllowsDodgeAndPauseFreezesWindup()
        {
            Setup();yield return Until(JumpingEnemy.AttackPhase.Windup);var direction=enemy.DashDirection;
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.15f);Assert.That(enemy.Phase,Is.EqualTo(JumpingEnemy.AttackPhase.Windup));Time.timeScale=1;
            hero.transform.position=origin+new Vector2(3,3);Physics2D.SyncTransforms();
            yield return Until(JumpingEnemy.AttackPhase.Recovery);
            Assert.That(enemy.DashDirection,Is.EqualTo(direction));Assert.That(health.Health,Is.EqualTo(1000));
            Assert.That(foe.transform.position.y,Is.EqualTo(origin.y).Within(.05f));
        }
        [UnityTest] public IEnumerator WallAddedAfterLockStopsBodyAndDamageBehindWall()
        {
            Setup();yield return Until(JumpingEnemy.AttackPhase.Windup);
            wall=new GameObject("Jump wall");wall.transform.position=origin+Vector2.right*1.5f;wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,5);Physics2D.SyncTransforms();
            yield return Until(JumpingEnemy.AttackPhase.Recovery);
            Assert.That(foe.transform.position.x,Is.LessThan(origin.x+1.05f));Assert.That(health.Health,Is.EqualTo(1000));
        }
        [UnityTest] public IEnumerator DisableReinitializeAndDeathClearAttackAndRegistry()
        {
            Setup();yield return Until(JumpingEnemy.AttackPhase.Dash);int deaths=0;enemy.Died+=()=>deaths++;
            foe.SetActive(false);Assert.That(registry.Count,Is.Zero);Assert.That(deaths,Is.Zero);
            foe.SetActive(true);Assert.That(enemy.Phase,Is.EqualTo(JumpingEnemy.AttackPhase.Idle));Assert.That(registry.Count,Is.EqualTo(1));
            enemy.Initialize(registry,hero.transform);Assert.That(enemy.DashDirection,Is.EqualTo(Vector2.zero));
            yield return Until(JumpingEnemy.AttackPhase.Windup);
            foe.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,100));
            Assert.That(deaths,Is.EqualTo(1));Assert.That(registry.Count,Is.Zero);Assert.That(enemy.Phase,Is.EqualTo(JumpingEnemy.AttackPhase.Dead));
            foe.SetActive(false);foe.SetActive(true);yield return new WaitForSeconds(.7f);
            Assert.That(deaths,Is.EqualTo(1));Assert.That(health.Health,Is.EqualTo(1000));Assert.That(registry.Count,Is.Zero);
        }
        [UnityTest] public IEnumerator SharedRegistrySelectsJumperAndPurgesDestroyedUnityTarget()
        {
            Setup();var selector=root.AddComponent<AutoAimSelector>();selector.Configure(registry);
            Assert.That(selector.Select(origin,10,-1),Is.SameAs(enemy));
            registry.enabled=false;Assert.That(selector.CurrentTarget,Is.Null);registry.enabled=true;
            Assert.That(registry.Count,Is.EqualTo(1));Assert.That(selector.Select(origin,10,-1),Is.SameAs(enemy));
            Object.DestroyImmediate(foe);Assert.That(registry.Count,Is.Zero);Assert.That(selector.CurrentTarget,Is.Null);
            yield return null;
        }
        [UnityTest] public IEnumerator TargetDisableCancelsPreparation()
        {
            Setup();health.ApplyDamage(new DamagePacket(-1,1)); // no immunity in this fixture; count is measured directly.
            yield return Until(JumpingEnemy.AttackPhase.Windup);hero.SetActive(false);
            yield return new WaitForFixedUpdate();Assert.That(enemy.Phase,Is.EqualTo(JumpingEnemy.AttackPhase.Idle));
            hero.SetActive(true);yield return Until(JumpingEnemy.AttackPhase.Windup);Assert.That(health.Health,Is.EqualTo(999));
        }
        [UnityTest] public IEnumerator ProtectedFirstContactDoesNotRetryAfterInvulnerabilityExpires()
        {
            Setup(.7f);hero.transform.position=origin+Vector2.right;Physics2D.SyncTransforms();health.ApplyDamage(new DamagePacket(-1,1));
            yield return Until(JumpingEnemy.AttackPhase.Recovery);Assert.That(health.Health,Is.EqualTo(999));
        }
        [UnityTest] public IEnumerator DeathDuringDashStopsMotionAndEmitsOnce()
        {
            Setup();yield return Until(JumpingEnemy.AttackPhase.Dash);int deaths=0;enemy.Died+=()=>deaths++;
            foe.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,100));var position=foe.transform.position;
            yield return new WaitForSeconds(.5f);Assert.That(foe.transform.position,Is.EqualTo(position));Assert.That(deaths,Is.EqualTo(1));Assert.That(registry.Count,Is.Zero);
        }
        [UnityTest] public IEnumerator LandingCoversAllNineCellCentersWithOneDamageEach()
        {
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
            {
                Setup();yield return Until(JumpingEnemy.AttackPhase.Windup);
                hero.transform.position=origin+new Vector2(3.6f+x,y);Physics2D.SyncTransforms();
                yield return Until(JumpingEnemy.AttackPhase.Recovery);
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                Assert.That(health.Health,Is.EqualTo(990),"Cell "+x+","+y);
                yield return Cleanup();
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;if(foe!=null)Object.Destroy(foe);if(hero!=null)Object.Destroy(hero);if(root!=null)Object.Destroy(root);if(wall!=null)Object.Destroy(wall);if(definition!=null)Object.Destroy(definition);yield return null;
        }
    }
}
