using System;
using System.Collections;
using Game.Actors;
using Game.Combat;
using Game.Core;
using Game.Sandbox;
using Game.Street;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Game.Tests
{
    public sealed class StreetRunTests
    {
        private GameBootstrap boot;
        [UnitySetUp] public IEnumerator Setup()
        {
            foreach(var old in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(old.gameObject);
            Time.timeScale=1;yield return null;yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap);
            yield return Until(()=> (boot=Object.FindFirstObjectByType<GameBootstrap>())!=null && boot.Coordinator.Phase==RunPhase.Home);
        }
        private static void Kill(ArenaController arena)
        {
            foreach(var target in arena.Combatants)((MonoBehaviour)target).GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
        }
        private IEnumerator StartStreet()
        {
            var home=Object.FindFirstObjectByType<HomeScreen>();if(home.IsShowingResult)home.PrimaryButton.onClick.Invoke();
            Assert.That(home.StreetButton,Is.Not.Null);home.StreetButton.onClick.Invoke();home.StreetButton.onClick.Invoke();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
        }
        [UnityTest] public IEnumerator FixedFiveModulesKeepResourcesAndPublishOnlyFinalOutcome()
        {
            yield return StartStreet();string runId=boot.Coordinator.RunId;PlayerCombatFacade oldHero=null;
            for(int index=1;index<=5;index++)
            {
                var arena=Object.FindFirstObjectByType<ArenaController>();var hero=arena.Player;
                Assert.That(boot.Coordinator.SegmentIndex,Is.EqualTo(index));Assert.That(boot.Coordinator.RunId,Is.EqualTo(runId));
                Assert.That(Object.FindFirstObjectByType<StreetSceneLoader>().CurrentModuleId,Is.EqualTo(StreetRoute.Fixed().ModuleIds[index-1]));
                Assert.That(Object.FindObjectsByType<TargetRegistry>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
                if(oldHero!=null)Assert.Fail("Previous hero survived unload");
                if(index==1)
                {
                    hero.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,50));
                    hero.GetComponent<WeaponController>().State.RestoreForEncounter(3,7,100,false);
                }
                Assert.That(hero.ReadSnapshot().Health,Is.EqualTo(80));Assert.That(hero.ReadSnapshot().Armor,Is.Zero);
                Assert.That(hero.ReadSnapshot().Magazine,Is.EqualTo(3));Assert.That(hero.ReadSnapshot().ReserveAmmo,Is.EqualTo(7));
                arena.Pause();yield return new WaitForSecondsRealtime(.03f);arena.Resume();Assert.That(Time.timeScale,Is.EqualTo(1));
                Kill(arena);Assert.That(arena.RemainingEnemies,Is.Zero);oldHero=hero;
                if(index<5)
                {
                    arena.TryExit();Assert.That(boot.Coordinator.LastOutcome,Is.Null);
                    hero.transform.position=arena.ExitPosition;Physics2D.SyncTransforms();arena.TryExit();arena.TryExit();
                    yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena && boot.Coordinator.SegmentIndex==index+1);
                    Assert.That(oldHero==null,Is.True);Assert.That(boot.Coordinator.LastOutcome,Is.Null);
                    Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None),Is.Empty);
                }
                else yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            }
            Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Victory));
            yield return StartStreet();Assert.That(boot.Coordinator.SegmentIndex,Is.EqualTo(1));
            var fresh=Object.FindFirstObjectByType<ArenaController>();Assert.That(fresh.Player.ReadSnapshot().Health,Is.EqualTo(100));
            fresh.Leave();yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
        }
        [UnityTest] public IEnumerator DeathAtFirstExitOverridesStreetAdvance()
        {
            yield return StartStreet();var arena=Object.FindFirstObjectByType<ArenaController>();Kill(arena);
            arena.Player.transform.position=arena.ExitPosition;arena.TryExit();
            arena.Player.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));arena.Resume();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Defeat));Assert.That(boot.Coordinator.SegmentIndex,Is.EqualTo(1));Assert.That(Time.timeScale,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator PortalAppearsOnceNearbyAndDetectionSeesTenUnitsButNotThroughWorld()
        {
            yield return StartStreet();
            var arena=Object.FindFirstObjectByType<ArenaController>();
            var portal=Object.FindFirstObjectByType<StreetExitPortal>();
            Assert.That(portal.IsOpen,Is.False);Assert.That(portal.GetComponent<SpriteRenderer>().enabled,Is.False);
            foreach(var enemy in arena.Combatants)((MonoBehaviour)enemy).enabled=false;
            var target=(MonoBehaviour)arena.Combatants[0];
            target.transform.position=new Vector2(-10,0);target.GetComponent<Rigidbody2D>().position=target.transform.position;
            arena.Player.transform.position=Vector2.zero;arena.Player.GetComponent<Rigidbody2D>().position=Vector2.zero;Physics2D.SyncTransforms();
            var perception=target.GetComponent<Game.AI.EnemyPerception>();
            Assert.That(perception.Tick(arena.Player.transform,arena.Player.GetComponent<DamageReceiver>(),0),Is.True,"Street radius must acquire at ten units");
            target.transform.position=new Vector2(-10,8);target.GetComponent<Rigidbody2D>().position=target.transform.position;
            arena.Player.transform.position=new Vector2(-10,4);arena.Player.GetComponent<Rigidbody2D>().position=arena.Player.transform.position;
            Physics2D.SyncTransforms();perception.ResetMemory();
            Assert.That(perception.Tick(arena.Player.transform,arena.Player.GetComponent<DamageReceiver>(),0),Is.False,"Concrete barrier must still hide hero");
            arena.Player.transform.position=new Vector2(17,-8);arena.Player.GetComponent<Rigidbody2D>().position=arena.Player.transform.position;Physics2D.SyncTransforms();
            foreach(var enemy in arena.Combatants)((MonoBehaviour)enemy).enabled=true;
            Kill(arena);Assert.That(portal.IsOpen,Is.True);
            Vector2 position=portal.transform.position;
            Assert.That(Vector2.Distance(position,arena.Player.transform.position),Is.InRange(2.49f,3.51f));
            Assert.That(Physics2D.OverlapCircle(position,.65f,LayerMask.GetMask("World")),Is.Null);
            Assert.That(Mathf.Abs(position.x),Is.LessThan(17.1f));Assert.That(Mathf.Abs(position.y),Is.LessThan(9.1f));
            Assert.That(portal.TryOpen(arena.Player),Is.True);Assert.That((Vector2)portal.transform.position,Is.EqualTo(position));
            arena.TryExit();Assert.That(boot.Coordinator.SegmentIndex,Is.EqualTo(1));
            arena.Leave();yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
        }
        [UnityTest] public IEnumerator FocusPauseAndTransientStateTransferPreserveResourcesAndMaxima()
        {
            yield return StartStreet();var arena=Object.FindFirstObjectByType<ArenaController>();
            var hero=arena.Player;var weapon=hero.GetComponent<WeaponController>();var motor=hero.GetComponent<ActorMotor2D>();
            hero.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,50));
            weapon.State.RestoreForEncounter(0,7,60,false);weapon.State.Advance(.01f);
            Assert.That(weapon.State.IsReloading,Is.True);weapon.State.SetSelected(false);
            motor.Stun(2);arena.SendMessage("OnApplicationFocus",false);
            yield return null; // Observe after the frame that requested the timeScale change.
            float remaining=motor.StunRemaining;yield return new WaitForSecondsRealtime(.1f);
            Assert.That(arena.IsPaused,Is.True);Assert.That(motor.StunRemaining,Is.EqualTo(remaining));
            arena.SendMessage("OnApplicationFocus",true);Assert.That(arena.IsPaused,Is.True);arena.Resume();
            Kill(arena);hero.transform.position=arena.ExitPosition;arena.TryExit();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena&&boot.Coordinator.SegmentIndex==2);
            arena=Object.FindFirstObjectByType<ArenaController>();var state=arena.Player.ReadSnapshot();
            Assert.That(state.Health,Is.EqualTo(80));Assert.That(state.Armor,Is.Zero);Assert.That(state.MaxHealth,Is.EqualTo(100));Assert.That(state.MaxArmor,Is.EqualTo(30));
            Assert.That(state.Magazine,Is.Zero);Assert.That(state.ReserveAmmo,Is.EqualTo(7));Assert.That(state.IsMelee,Is.True);Assert.That(state.IsReloading,Is.False);
            Assert.That(state.Stamina,Is.GreaterThanOrEqualTo(60));Assert.That(state.MaxStamina,Is.EqualTo(100));
            Assert.That(arena.Player.GetComponent<ActorMotor2D>().StunRemaining,Is.Zero);
            Assert.That(arena.Player.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1)).Applied,Is.True,"Old hit protection must end at scene boundary");
            arena.Pause();arena.Leave();yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            Assert.That(Time.timeScale,Is.EqualTo(1));yield return StartStreet();
            Assert.That(Object.FindFirstObjectByType<ArenaController>().Player.ReadSnapshot().Health,Is.EqualTo(100));
        }
        [UnityTest] public IEnumerator TenAttemptsCleanActivePoolsAndPreferDeathOnBothKindsOfExit()
        {
            var ids=new System.Collections.Generic.HashSet<string>();
            for(int attempt=0;attempt<10;attempt++)
            {
                yield return StartStreet();Assert.That(ids.Add(boot.Coordinator.RunId),Is.True);
                int last=attempt%3==0?2:5;
                for(int index=1;index<=last;index++)
                {
                    var arena=Object.FindFirstObjectByType<ArenaController>();var oldHero=arena.Player;
                    var pools=Object.FindObjectsByType<ProjectilePool>(FindObjectsSortMode.None);
                    foreach(var pool in pools)
                    {
                        var request=new ProjectileSpawnRequest(new Vector2(0,-8),Vector2.up,.01f,100,.1f,~0,-9,CombatFaction.Player,new DamagePacket(-9,1));
                        Assert.That(pool.TrySpawn(in request),Is.True);
                    }
                    if(index==last&&attempt%3==0)
                    {
                        arena.Pause();arena.Leave();arena.Leave();
                    }
                    else
                    {
                        Kill(arena);
                        if(index<5){oldHero.transform.position=arena.ExitPosition;arena.TryExit();arena.TryExit();}
                        if(index==last&&attempt%3==1)oldHero.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
                    }
                    if(index==last)yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
                    else yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena&&boot.Coordinator.SegmentIndex==index+1);
                    Assert.That(oldHero==null,Is.True);Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None),Is.Empty);
                    Assert.That(Object.FindObjectsByType<Game.AI.ShieldMinion>(FindObjectsSortMode.None),Is.Empty);Assert.That(Time.timeScale,Is.EqualTo(1));
                }
                Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(attempt%3==0?RunOutcome.Abandoned:attempt%3==1?RunOutcome.Defeat:RunOutcome.Victory));
                Assert.That(Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;foreach(var old in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(old.gameObject);yield return null;
            var clean=SceneManager.CreateScene("StreetCleanup");SceneManager.SetActiveScene(clean);
            foreach(string name in new[]{ScenePaths.Home,ScenePaths.Stage3StreetSandbox})
            {var scene=SceneManager.GetSceneByName(name);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        }
        private static IEnumerator Until(Func<bool> predicate)
        {float limit=Time.realtimeSinceStartup+15;while(!predicate()){Assert.That(Time.realtimeSinceStartup,Is.LessThan(limit));yield return null;}}
    }
}
