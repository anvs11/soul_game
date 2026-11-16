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
    public sealed class GeneratedStreetRunTests
    {
        private GameBootstrap boot;
        [UnitySetUp] public IEnumerator Setup()
        {
            foreach(var b in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(b.gameObject);
            Time.timeScale=1;yield return null;yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap);
            yield return Until(()=> (boot=Object.FindFirstObjectByType<GameBootstrap>())!=null && boot.Coordinator.Phase==RunPhase.Home);
        }
        [UnityTest] public IEnumerator HomeRejectsInvalidSeedAndBrokenCatalogThenLoadsRepeatableGeneratedFive()
        {
            var home=Object.FindFirstObjectByType<HomeScreen>();home.SeedInput.text="bad";home.StreetButton.onClick.Invoke();
            Assert.That(boot.Coordinator.Phase,Is.EqualTo(RunPhase.Home));Assert.That(home.StreetError,Does.Contain("Seed"));
            var data=Resources.Load<StreetCatalogAsset>("StreetCatalog");var boss=data.Modules[5];var original=boss.Walkable;
            try
            {
                boss.Walkable=new bool[original.Length];home.SeedInput.text="123";home.StreetButton.onClick.Invoke();
                Assert.That(boot.Coordinator.Phase,Is.EqualTo(RunPhase.Home));Assert.That(home.StreetError,Does.Contain("street_boss"));
                Assert.That(boot.Coordinator.RunId,Is.Null);
            }
            finally{boss.Walkable=original;}
            home.SeedInput.text="123";home.StreetButton.onClick.Invoke();home.StreetButton.onClick.Invoke();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            var ids=new string[5];string id=boot.Coordinator.RunId;
            for(int index=1;index<=5;index++)
            {
                var arena=Object.FindFirstObjectByType<ArenaController>();
                ids[index-1]=Object.FindFirstObjectByType<StreetSceneLoader>().CurrentModuleId;
                Assert.That(ids[index-1],Is.EqualTo(boot.Coordinator.StreetGeneration.Layout.Segments[index-1].ModuleId));
                Assert.That(boot.Coordinator.StreetGeneration.Layout.Seed,Is.EqualTo(123));Assert.That(boot.Coordinator.RunId,Is.EqualTo(id));
                foreach(var enemy in arena.Combatants)((MonoBehaviour)enemy).GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
                if(index<5)
                {
                    arena.Player.transform.position=arena.ExitPosition;arena.TryExit();
                    yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena&&boot.Coordinator.SegmentIndex==index+1);
                    Assert.That(boot.Coordinator.LastOutcome,Is.Null);
                }
                else yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            }
            Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Victory));
            home=Object.FindFirstObjectByType<HomeScreen>();Assert.That(home.SeedInput.gameObject.activeSelf,Is.False);
            home.PrimaryButton.onClick.Invoke();home.SeedInput.text="123";home.StreetButton.onClick.Invoke();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            Assert.That(boot.Coordinator.StreetRoute.ModuleIds,Is.EqualTo(ids));
            Assert.That(boot.Coordinator.RunId,Is.Not.EqualTo(id));Object.FindFirstObjectByType<ArenaController>().Leave();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
        }
        [UnityTest] public IEnumerator EmptySeedLaunchesWithExplicitSeedAndVersionMetadata()
        {
            var home=Object.FindFirstObjectByType<HomeScreen>();home.SeedInput.text="";home.StreetButton.onClick.Invoke();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            var generated=boot.Coordinator.StreetGeneration;
            Assert.That(generated,Is.Not.Null);Assert.That(generated.Layout.Seed,Is.EqualTo(boot.Coordinator.CurrentRequest.Seed));
            Assert.That(generated.Layout.GeneratorVersion,Is.EqualTo(1));Assert.That(generated.Layout.ContentVersion,Is.EqualTo(1));
            Object.FindFirstObjectByType<ArenaController>().Leave();yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
        }
        [UnityTest] public IEnumerator DifferentSeedSelectsAnotherRouteAndFallbackShowsItsCauseInHud()
        {
            var home=Object.FindFirstObjectByType<HomeScreen>();home.SeedInput.text="0";home.StreetButton.onClick.Invoke();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            Assert.That(boot.Coordinator.StreetRoute.ModuleIds,Is.Not.EqualTo(StreetRoute.Fixed().ModuleIds));
            Object.FindFirstObjectByType<ArenaController>().Leave();yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            var catalog=StreetGenerationAdapter.BuildCatalog(Resources.Load<StreetCatalogAsset>("StreetCatalog").Modules);
            var modules=new System.Collections.Generic.List<Game.Generation.ModuleDescriptor>(catalog.Modules);
            for(int i=0;i<64;i++)modules.Add(new Game.Generation.ModuleDescriptor("bad_"+i.ToString("D2"),Game.Generation.ModuleKind.Combat,
                1,1,new[]{false},new Game.Generation.Cell(0,0),new Game.Generation.Cell(0,0),new[]{new Game.Generation.Cell(0,0)}));
            var bank=new Game.Generation.StageCatalog(modules);
            Assert.That(StreetGenerationAdapter.TryGenerate("0",bank,out var fallback,out var error),Is.True,error);
            Assert.That(fallback.UsedFallback,Is.True);boot.Coordinator.StartGeneratedStreetRun(fallback,bank);
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            string labels="";
            foreach(var label in Object.FindFirstObjectByType<HudPresenter>().GetComponentsInChildren<UnityEngine.UI.Text>())labels+=label.text;
            Assert.That(labels,Does.Contain("резерв:").And.Contain("bad_").And.Contain("seed 0").And.Contain("G1/C1"));
            Assert.That(boot.Coordinator.StreetGeneration.Diagnostic,Does.Contain("validated fallback"));
            Object.FindFirstObjectByType<ArenaController>().Leave();yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;foreach(var b in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(b.gameObject);yield return null;
            var clean=SceneManager.CreateScene("GeneratedStreetCleanup");SceneManager.SetActiveScene(clean);
            foreach(var name in new[]{ScenePaths.Home,ScenePaths.Stage3StreetSandbox})
            {var scene=SceneManager.GetSceneByName(name);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        }
        private static IEnumerator Until(Func<bool> predicate)
        {float limit=Time.realtimeSinceStartup+15;while(!predicate()){Assert.That(Time.realtimeSinceStartup,Is.LessThan(limit));yield return null;}}
    }
}
