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
