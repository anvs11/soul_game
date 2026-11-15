using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests
{
    public sealed class ZombieMeleeTests
    {
        private GameObject hero, foe, registryObject, wall;
        private Vector2 origin = new Vector2(4500,4500);
        private ActorMotor2D motor;
        private DamageReceiver health;
        private SandboxEnemy enemy;
        private void Setup(bool enemyNeeded)
        {
            hero=new GameObject("Melee target"); hero.SetActive(false); hero.transform.position=origin;
            var body=hero.AddComponent<Rigidbody2D>(); body.gravityScale=0; body.freezeRotation=true;
            body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            hero.AddComponent<CircleCollider2D>().radius=.4f;
            hero.AddComponent<ActorIdentity>();
            health=hero.AddComponent<DamageReceiver>(); health.Configure(500,0,.5f);
            var input=hero.AddComponent<MovementTestInput>(); motor=hero.AddComponent<ActorMotor2D>(); motor.Configure(input);
            hero.SetActive(true);
            if(!enemyNeeded)return;
            registryObject=new GameObject("Registry"); var registry=registryObject.AddComponent<TargetRegistry>();
            foe=new GameObject("Attacker"); foe.SetActive(false); foe.transform.position=origin+Vector2.right*1.05f;
            foe.AddComponent<CircleCollider2D>().radius=.3f;
            foe.AddComponent<DamageReceiver>().Configure(30,0,0);
            enemy=foe.AddComponent<SandboxEnemy>(); foe.GetComponent<Rigidbody2D>().gravityScale=0;
            enemy.Initialize(registry,hero.transform); foe.SetActive(true); Physics2D.SyncTransforms();
        }
        [UnityTest] public IEnumerator WindupDoesNotPushAndAcceptedHitMovesOneCell()
        {
            Setup(true);
            yield return new WaitForSeconds(.1f);
            Assert.That(health.Health,Is.EqualTo(500));
            Assert.That(Vector2.Distance(hero.transform.position,origin),Is.LessThan(.02f));
            yield return new WaitForSeconds(.5f);
            Assert.That(health.Health,Is.EqualTo(490));
            Assert.That(hero.transform.position.x,Is.EqualTo(origin.x-1).Within(.08f));
        }
        [UnityTest] public IEnumerator LeavingTheTelegraphedRangeDodgesTheHit()
        {
            Setup(true); yield return new WaitForSeconds(.08f);
            Assert.That(enemy.Phase,Is.EqualTo(SandboxEnemy.AttackPhase.Windup));
            hero.GetComponent<Rigidbody2D>().position=origin+Vector2.up*3;
            Physics2D.SyncTransforms(); yield return new WaitForSeconds(.45f);
            Assert.That(health.Health,Is.EqualTo(500)); Assert.That(motor.IsKnockedBack,Is.False);
        }
        [UnityTest] public IEnumerator RepeatedKnockbackCannotCrossSolidWall()
        {
            Setup(false);
            wall=new GameObject("Boundary"); wall.transform.position=origin+Vector2.right*.8f;
            wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,5);
            Physics2D.SyncTransforms();
            for(int i=0;i<10;i++)
            {
                motor.BeginKnockback(Vector2.right);
                yield return new WaitForSeconds(.2f);
                Assert.That(hero.transform.position.x,Is.LessThan(origin.x+.32f));
            }
        }
        [UnityTest] public IEnumerator FasterAttackMeasuresAllThreePhasesAndPreservesTenDamage()
        {
            Setup(true);
            // Keep the target in range so timing isn't changed by chasing knockback.
            motor.SetControlsEnabled(false);
            float deadline=Time.fixedTime+1;
            while(enemy.Phase==SandboxEnemy.AttackPhase.Chase)
            {
                Assert.That(Time.fixedTime,Is.LessThan(deadline));
                yield return new WaitForFixedUpdate();
            }
            var phases=new[]{SandboxEnemy.AttackPhase.Windup,SandboxEnemy.AttackPhase.Swing,SandboxEnemy.AttackPhase.Recover};
            var durations=new[]{.2f,.06f,.3f};
            for(int index=0;index<phases.Length;index++)
            {
                Assert.That(enemy.Phase,Is.EqualTo(phases[index]));
                float began=Time.fixedTime;
                while(enemy.Phase==phases[index])
                {
                    Assert.That(Time.fixedTime-began,Is.LessThan(1));
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(Time.fixedTime-began,Is.EqualTo(durations[index]).Within(Time.fixedDeltaTime+.005f),phases[index].ToString());
            }
            Assert.That(health.Health,Is.EqualTo(490));
            Assert.That(enemy.Phase,Is.EqualTo(SandboxEnemy.AttackPhase.Chase));
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(foe!=null)Object.Destroy(foe);
            if(hero!=null)Object.Destroy(hero);
            if(registryObject!=null)Object.Destroy(registryObject);
            if(wall!=null)Object.Destroy(wall);
            yield return null;
        }
    }
}
