using System;
using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Contracts;
using Game.Core;
using Game.Input;
using Game.Sandbox;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Game.Tests
{
    public sealed class Stage2RouteIntegrationTests
    {
        private GameBootstrap boot;
        private ArenaController arena;
        [UnitySetUp] public IEnumerator Setup()
        {
            foreach(var b in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(b.gameObject);
            Time.timeScale=1; yield return null;
            yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap);
            yield return Until(()=> (boot=Object.FindFirstObjectByType<GameBootstrap>())!=null && boot.Coordinator.Phase==RunPhase.Home);
            yield return StartRoute();
        }
        private IEnumerator StartRoute()
        {
            var home=Object.FindFirstObjectByType<HomeScreen>();
            if(home.IsShowingResult)home.PrimaryButton.onClick.Invoke();
            home.SecondaryButton.onClick.Invoke();home.SecondaryButton.onClick.Invoke();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            arena=Object.FindFirstObjectByType<ArenaController>(); Idle();
            Assert.That(boot.Coordinator.CurrentRequest.IsStage2Route,Is.True);
        }
        private void Idle()
        {
            var motor=arena.Player.GetComponent<ActorMotor2D>();motor.enabled=false;
            var input=arena.Player.gameObject.AddComponent<MovementTestInput>();motor.Configure(input);motor.enabled=true;
        }
        private void ClearArena()
        {
            foreach(var target in arena.Combatants)
            {
                var component=(MonoBehaviour)target;
                component.enabled=true; // Died is published only by an enabled behaviour.
                component.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
            }
            Assert.That(arena.RemainingEnemies,Is.Zero);
        }
        private void Exit()
        {arena.Player.transform.position=arena.ExitPosition;Physics2D.SyncTransforms();arena.TryExit();arena.TryExit();}
        private IEnumerator EnterBoss()
        {
            ClearArena(); Exit();
            yield return Until(()=>SceneManager.GetActiveScene().name==ScenePaths.Stage2BossSandbox && boot.Coordinator.Phase==RunPhase.InArena);
            arena=Object.FindFirstObjectByType<ArenaController>();Idle();
        }
        private static void SpawnBullet(ProjectilePool pool,Vector2 position)
        {
            var request=new ProjectileSpawnRequest(position,Vector2.up,.01f,100,.1f,~0,-4,CombatFaction.Player,new DamagePacket(-4,10));
            Assert.That(pool.TrySpawn(in request),Is.True);
        }
        [UnityTest] public IEnumerator RealHomeRouteTransfersResourcesAndClearsBothPoolsBeforeLoad()
        {
            foreach(var target in arena.Combatants)((MonoBehaviour)target).enabled=false;
            var oldHero=arena.Player; string id=boot.Coordinator.RunId;
            oldHero.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,50));
            oldHero.GetComponent<WeaponController>().State.RestoreForEncounter(3,7,60,true);
            var expected=oldHero.ReadSnapshot();
            var oldPools=Object.FindObjectsByType<ProjectilePool>(FindObjectsSortMode.None);
            foreach(var pool in oldPools)SpawnBullet(pool,new Vector2(0,-8));
            bool observed=false;
            Action inspect=()=>{if(boot.Coordinator.Phase==RunPhase.LoadingArena){observed=true;foreach(var pool in oldPools)Assert.That(pool.ActiveCount,Is.Zero);}};
            boot.Coordinator.Changed+=inspect;
            ClearArena();arena.TryExit();Assert.That(boot.Coordinator.LastOutcome,Is.Null);
            Assert.That(boot.Coordinator.ActiveArena,Is.EqualTo(ScenePaths.Stage2CombatSandbox),"Far exit rejected");
            Exit();yield return Until(()=>SceneManager.GetActiveScene().name==ScenePaths.Stage2BossSandbox && boot.Coordinator.Phase==RunPhase.InArena);
            boot.Coordinator.Changed-=inspect;arena=Object.FindFirstObjectByType<ArenaController>();Idle();
            Assert.That(observed,Is.True);Assert.That(oldHero==null,Is.True);Assert.That(boot.Coordinator.RunId,Is.EqualTo(id));Assert.That(boot.Coordinator.LastOutcome,Is.Null);
            var actual=arena.Player.ReadSnapshot();
            Assert.That(actual.Health,Is.EqualTo(expected.Health));Assert.That(actual.Armor,Is.EqualTo(expected.Armor));
            Assert.That(actual.Magazine,Is.EqualTo(3));Assert.That(actual.ReserveAmmo,Is.EqualTo(7));Assert.That(actual.IsMelee,Is.True);Assert.That(actual.Stamina,Is.InRange(60,61));
            Assert.That(arena.RemainingEnemies,Is.EqualTo(1));Assert.That(Object.FindObjectsByType<HazardBossPreview>(FindObjectsSortMode.None),Is.Empty);
            AssertContext();Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None),Is.Empty);
            var boss=Object.FindFirstObjectByType<PoliceBoss>();boss.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Victory));Assert.That(Object.FindFirstObjectByType<HomeScreen>().IsShowingResult,Is.True);
        }
        [UnityTest] public IEnumerator ArenaBarrelHurtsPushesStunsButNeverCountsAsEnemy()
        {
            foreach(var target in arena.Combatants)((MonoBehaviour)target).enabled=false;
            foreach(var target in arena.Combatants)
            {
                var component=(MonoBehaviour)target;
                component.transform.position=new Vector2(12,7);
                component.enabled=true;
            }
            var barrel=Object.FindFirstObjectByType<ExplosiveBarrel>();Assert.That(barrel,Is.Not.Null);Assert.That(arena.RemainingEnemies,Is.EqualTo(3));
            var body=arena.Player.GetComponent<Rigidbody2D>();arena.Player.transform.position=(Vector2)barrel.transform.position+Vector2.left*1.2f;body.position=arena.Player.transform.position;Physics2D.SyncTransforms();
            barrel.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,10));
            Assert.That(arena.Player.ReadSnapshot().Health,Is.EqualTo(80));Assert.That(arena.Player.ReadSnapshot().Armor,Is.Zero);
            Assert.That(arena.RemainingEnemies,Is.EqualTo(3));
            var before=body.position;yield return new WaitForSeconds(.25f);
            Assert.That(body.position.x,Is.LessThan(before.x-2));Assert.That(arena.Player.GetComponent<ActorMotor2D>().StunRemaining,Is.GreaterThan(1));
            arena.Pause();float stun=arena.Player.GetComponent<ActorMotor2D>().StunRemaining;yield return new WaitForSecondsRealtime(.1f);
            Assert.That(arena.Player.GetComponent<ActorMotor2D>().StunRemaining,Is.EqualTo(stun));arena.Leave();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Abandoned));
        }
        [UnityTest] public IEnumerator ActualArenaCoverBlocksBarrelEffect()
        {
            foreach(var target in arena.Combatants)((MonoBehaviour)target).enabled=false;
            var barrel=Object.FindFirstObjectByType<ExplosiveBarrel>();barrel.transform.position=new Vector2(-10.5f,-3);
            arena.Player.transform.position=new Vector2(-7.5f,-3);arena.Player.GetComponent<Rigidbody2D>().position=arena.Player.transform.position;Physics2D.SyncTransforms();
            barrel.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,10));
            Assert.That(arena.Player.ReadSnapshot().Health,Is.EqualTo(100));Assert.That(arena.Player.ReadSnapshot().Armor,Is.EqualTo(30));
            Assert.That(arena.Player.GetComponent<ActorMotor2D>().StunRemaining,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator DeathAtIntermediateExitOverridesTransition()
        {
            ClearArena();Exit();arena.Player.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Defeat));Assert.That(boot.Coordinator.PlayerTransfer,Is.Null);
            Assert.That(Object.FindFirstObjectByType<PoliceBoss>(),Is.Null);Assert.That(Time.timeScale,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator BossThresholdsPausePreparationAndDeathAreIntegrated()
        {
            yield return EnterBoss();var boss=Object.FindFirstObjectByType<PoliceBoss>();var hp=boss.GetComponent<DamageReceiver>();
            Assert.That(boss.Level,Is.EqualTo(1));hp.ApplyDamage(new DamagePacket(-1,200));Assert.That(boss.Level,Is.EqualTo(2));
            arena.Pause();var position=boss.transform.position;yield return new WaitForSecondsRealtime(.2f);
            Assert.That(boss.Phase,Is.EqualTo(PoliceBoss.AttackPhase.Windup));Assert.That(boss.transform.position,Is.EqualTo(position));arena.Resume();
            yield return Until(()=>boss.SecondStageSummons==2);
            foreach(var minion in Object.FindObjectsByType<ShieldMinion>(FindObjectsSortMode.None))minion.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
            hp.ApplyDamage(new DamagePacket(-1,200));Assert.That(boss.Level,Is.EqualTo(3));
            // Stay away so this test observes all three attack stages, not player death.
            arena.Player.transform.position=new Vector2(14,7);arena.Player.GetComponent<Rigidbody2D>().position=arena.Player.transform.position;Physics2D.SyncTransforms();
            yield return Until(()=>boss.Phase==PoliceBoss.AttackPhase.Spin);
            Assert.That(boss.ThirdStageSummons,Is.EqualTo(4));Assert.That(arena.RemainingEnemies,Is.EqualTo(1));
            boss.enabled=false;boss.enabled=true;Assert.That(boss.Phase,Is.EqualTo(PoliceBoss.AttackPhase.Windup));
            SpawnBullet(arena.Player.GetComponent<ProjectilePool>(),Vector2.zero);
            hp.ApplyDamage(new DamagePacket(-1,1000));yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Victory));Assert.That(Object.FindObjectsByType<ShieldMinion>(FindObjectsSortMode.None),Is.Empty);Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None),Is.Empty);
        }
        [UnityTest] public IEnumerator BossWallsHoldRollAndPlayerKnockback()
        {
            yield return EnterBoss();var boss=Object.FindFirstObjectByType<PoliceBoss>();var hero=arena.Player.GetComponent<Rigidbody2D>();
            arena.Player.transform.position=new Vector2(17,0);hero.position=arena.Player.transform.position;boss.transform.position=new Vector2(13,0);boss.GetComponent<Rigidbody2D>().position=boss.transform.position;
            boss.Initialize(Object.FindFirstObjectByType<TargetRegistry>(),arena.Player.transform);Physics2D.SyncTransforms();
            arena.Player.GetComponent<ActorMotor2D>().BeginKnockback(Vector2.right,3,.15f);
            yield return new WaitForSeconds(1.4f);
            Assert.That(hero.position.x,Is.LessThan(17.6f));Assert.That(boss.transform.position.x,Is.LessThanOrEqualTo(15.8f));
            AssertContext();
        }
        [UnityTest] public IEnumerator TenRoutesHaveOneOutcomeFreshActorsAndNoOldProjectiles()
        {
            var ids=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<10;i++)
            {
                Assert.That(ids.Add(boot.Coordinator.RunId),Is.True);Assert.That(arena.Player.ReadSnapshot().Health,Is.EqualTo(100));Assert.That(arena.Player.ReadSnapshot().Magazine,Is.EqualTo(8));
                yield return EnterBoss();var old=arena.Player;var boss=Object.FindFirstObjectByType<PoliceBoss>();
                SpawnBullet(old.GetComponent<ProjectilePool>(),new Vector2(0,-8));SpawnBullet(boss.GetComponent<ProjectilePool>(),new Vector2(0,-8));
                if(i%3==0){arena.Pause();arena.Leave();arena.Leave();}
                else if(i%3==1){boss.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));old.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));arena.Resume();}
                else boss.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
                yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
                Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(i%3==0?RunOutcome.Abandoned:i%3==1?RunOutcome.Defeat:RunOutcome.Victory));
                Assert.That(old==null,Is.True);Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None),Is.Empty);Assert.That(Object.FindObjectsByType<ShieldMinion>(FindObjectsSortMode.None),Is.Empty);Assert.That(Time.timeScale,Is.EqualTo(1));AssertContext();
                if(i<9)yield return StartRoute();
            }
        }
        private static void AssertContext()
        {
            Assert.That(Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length,Is.EqualTo(1));Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;foreach(var b in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(b.gameObject);yield return null;
            var clean=SceneManager.CreateScene("Stage2RouteCleanup");SceneManager.SetActiveScene(clean);
            foreach(var name in new[]{ScenePaths.Home,ScenePaths.Stage2CombatSandbox,ScenePaths.Stage2BossSandbox})
            {var scene=SceneManager.GetSceneByName(name);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        }
        private static IEnumerator Until(Func<bool> predicate)
        {float limit=Time.realtimeSinceStartup+15;while(!predicate()){Assert.That(Time.realtimeSinceStartup,Is.LessThan(limit),"Timed out waiting for route/attack");yield return null;}}
    }
}
