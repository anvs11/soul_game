using System;
using System.Collections;
using System.Collections.Generic;
using Game.Actors;
using Game.Combat;
using Game.Core;
using Game.Sandbox;
using Game.Street;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace Game.Tests
{
    public sealed class GeneratedStreetLifecycleTests
    {
        private GameBootstrap boot;
        private GameObject blocker;
        private bool previousQueries;
        [UnitySetUp] public IEnumerator Setup()
        {
            previousQueries = Physics2D.queriesStartInColliders;
            foreach (var old in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            Time.timeScale = 1; yield return null;
            yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap);
            yield return Until(() => (boot = Object.FindFirstObjectByType<GameBootstrap>()) != null && boot.Coordinator.Phase == RunPhase.Home);
        }
        [UnityTest] public IEnumerator TenGeneratedAttemptsCleanObjectsPreserveResourcesAndResolveRaces()
        {
            int[] seeds = { 0, 123, -1, int.MinValue, int.MaxValue, 42, 7, -123, 99, 123 };
            int[] ends = { 5, 1, 2, 3, 4, 5, 2, 3, 4, 5 };
            RunOutcome[] outcomes = { RunOutcome.Victory, RunOutcome.Defeat, RunOutcome.Abandoned,
                RunOutcome.Defeat, RunOutcome.Abandoned, RunOutcome.Defeat, RunOutcome.Abandoned,
                RunOutcome.Defeat, RunOutcome.Abandoned, RunOutcome.Victory };
            var runIds = new HashSet<string>();
            var catalog = StreetGenerationAdapter.BuildCatalog(Resources.Load<StreetCatalogAsset>("StreetCatalog").Modules);
            for (int attempt = 0; attempt < seeds.Length; attempt++)
            {
                var home = Object.FindFirstObjectByType<HomeScreen>();
                if (home.IsShowingResult) home.PrimaryButton.onClick.Invoke();
                home.SeedInput.text = seeds[attempt].ToString(System.Globalization.CultureInfo.InvariantCulture);
                home.StreetButton.onClick.Invoke();
                yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
                Assert.That(runIds.Add(boot.Coordinator.RunId), Is.True);
                Assert.That(StreetGenerationAdapter.TryGenerate(seeds[attempt].ToString(System.Globalization.CultureInfo.InvariantCulture), catalog, out var expected, out var error), Is.True, error);
                for (int segment = 1; segment <= ends[attempt]; segment++)
                {
                    var arena = Object.FindFirstObjectByType<ArenaController>();
                    var hero = arena.Player;
                    Assert.That(boot.Coordinator.CurrentRequest.Seed, Is.EqualTo(seeds[attempt]));
                    Assert.That(boot.Coordinator.StreetGeneration.Layout.GeneratorVersion, Is.EqualTo(1));
                    Assert.That(boot.Coordinator.StreetGeneration.Layout.ContentVersion, Is.EqualTo(1));
                    Assert.That(Object.FindFirstObjectByType<StreetSceneLoader>().CurrentModuleId,
                        Is.EqualTo(expected.Layout.Segments[segment - 1].ModuleId));
                    Assert.That(boot.Coordinator.SegmentIndex, Is.EqualTo(segment));
                    Assert.That(boot.Coordinator.LastOutcome, Is.Null);
                    Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                    Assert.That(Object.FindObjectsByType<TargetRegistry>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                    Assert.That(Object.FindObjectsByType<PlayerCombatFacade>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                    var hud = Object.FindFirstObjectByType<HudPresenter>();
                    string labels = "";
                    foreach (var label in hud.GetComponentsInChildren<UnityEngine.UI.Text>()) labels += label.text;
                    Assert.That(labels, Does.Contain("1." + segment + "/5").And.Contain("seed " + seeds[attempt]).And.Contain("G1/C1"));
                    Assert.That(labels, Does.Contain(segment == 5 ? "Полицейский" : "Зачистите"));
                    AssertSafe(arena.Controls); AssertSafe(hud);
                    if (segment == 1)
                    {
                        Assert.That(hero.ReadSnapshot().Health, Is.EqualTo(100));
                        Assert.That(hero.ReadSnapshot().Armor, Is.EqualTo(30));
                        Assert.That(hero.ReadSnapshot().Magazine, Is.EqualTo(8));
                        Assert.That(hero.ReadSnapshot().ReserveAmmo, Is.EqualTo(16));
                        hero.GetComponent<DamageReceiver>().RestoreForEncounter(80, 0);
                        hero.GetComponent<WeaponController>().State.RestoreForEncounter(3, 7, 100, true);
                    }
                    Assert.That(hero.ReadSnapshot().Health, Is.EqualTo(80));
                    Assert.That(hero.ReadSnapshot().Armor, Is.Zero);
                    Assert.That(hero.ReadSnapshot().Magazine, Is.EqualTo(3));
                    Assert.That(hero.ReadSnapshot().ReserveAmmo, Is.EqualTo(7));
                    Assert.That(hero.ReadSnapshot().IsMelee, Is.True);
                    Assert.That(hero.ReadSnapshot().Stamina, Is.EqualTo(100));
                    var pools = Object.FindObjectsByType<ProjectilePool>(FindObjectsSortMode.None);
                    Assert.That(pools.Length, Is.GreaterThan(0));
                    foreach (var pool in pools)
                    {
                        var shot = new ProjectileSpawnRequest(new Vector2(0,-8), Vector2.up, .01f, 100, .1f, ~0, -9, CombatFaction.Player, new DamagePacket(-9,1));
                        Assert.That(pool.TrySpawn(in shot), Is.True);
                        Assert.That(pool.ActiveCount, Is.GreaterThan(0));
                    }
                    TestContext.WriteLine("S306B loaded attempt=" + attempt + " segment=" + segment + " module=" + expected.Layout.Segments[segment - 1].ModuleId);
                    bool last = segment == ends[attempt];
                    if (last && outcomes[attempt] == RunOutcome.Abandoned) { arena.Pause(); arena.Leave(); arena.Leave(); }
                    else
                    {
                        Kill(arena);
                        if (segment < 5)
                        {
                            hero.transform.position = arena.ExitPosition; Physics2D.SyncTransforms();
                            arena.TryExit(); arena.TryExit();
                        }
                        if (last && outcomes[attempt] == RunOutcome.Defeat)
                            hero.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
                    }
                    if (last) yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
                    else yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena && boot.Coordinator.SegmentIndex == segment + 1);
                    Assert.That(hero == null && arena == null && hud == null, Is.True, "Old actors/views must be unloaded");
                    foreach (var pool in pools) Assert.That(pool == null, Is.True);
                    Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None), Is.Empty);
                    Assert.That(Object.FindObjectsByType<Game.AI.ShieldMinion>(FindObjectsSortMode.None), Is.Empty);
                    Assert.That(Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                    Assert.That(Time.timeScale, Is.EqualTo(1));
                }
                Assert.That(boot.Coordinator.LastOutcome, Is.EqualTo(outcomes[attempt]));
                TestContext.WriteLine("S306B attempt=" + attempt + " seed=" + seeds[attempt] + " segment=" + ends[attempt] + " outcome=" + outcomes[attempt]);
            }
        }
        [UnityTest] public IEnumerator ClearedPortalRecoversOnInteractAfterTemporaryObstruction()
        {
            var home = Object.FindFirstObjectByType<HomeScreen>(); home.SeedInput.text = "0"; home.StreetButton.onClick.Invoke();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
            var arena = Object.FindFirstObjectByType<ArenaController>(); var portal = Object.FindFirstObjectByType<StreetExitPortal>();
            var body = arena.Player.GetComponent<Rigidbody2D>(); body.simulated = false;
            Physics2D.queriesStartInColliders = true;
            blocker = new GameObject("Temporary World obstruction"); blocker.layer = LayerMask.NameToLayer("World");
            blocker.transform.position = arena.Player.transform.position; blocker.AddComponent<BoxCollider2D>().size = new Vector2(4,4);
            Physics2D.SyncTransforms(); Kill(arena); Assert.That(portal.IsOpen, Is.False);
            arena.Pause(); arena.TryExit(); Assert.That(portal.IsOpen, Is.False); arena.Resume();
            Object.Destroy(blocker); yield return null; Physics2D.SyncTransforms(); body.simulated = true;
            arena.TryExit(); Assert.That(portal.IsOpen, Is.True);
            Assert.That(boot.Coordinator.SegmentIndex, Is.EqualTo(1), "Placement alone must not skip proximity");
            arena.Player.transform.position = arena.ExitPosition; arena.TryExit(); arena.TryExit();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena && boot.Coordinator.SegmentIndex == 2);
            Object.FindFirstObjectByType<ArenaController>().Leave(); yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
        }
        private static void AssertSafe(Component owner)
        {
            int count = 0;
            foreach (var rect in owner.GetComponentsInChildren<RectTransform>())
                if (rect.name == "SafeArea")
                {
                    count++;
                    Assert.That(rect.anchorMin.x, Is.EqualTo(Screen.safeArea.xMin / Screen.width).Within(.0001));
                    Assert.That(rect.anchorMin.y, Is.EqualTo(Screen.safeArea.yMin / Screen.height).Within(.0001));
                    Assert.That(rect.anchorMax.x, Is.EqualTo(Screen.safeArea.xMax / Screen.width).Within(.0001));
                    Assert.That(rect.anchorMax.y, Is.EqualTo(Screen.safeArea.yMax / Screen.height).Within(.0001));
                }
            Assert.That(count, Is.EqualTo(1));
        }
        private static void Kill(ArenaController arena)
        { foreach (var target in arena.Combatants) ((MonoBehaviour)target).GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000)); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Physics2D.queriesStartInColliders = previousQueries; Time.timeScale = 1;
            if (blocker != null) Object.Destroy(blocker);
            foreach (var old in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null; var clean = SceneManager.CreateScene("GeneratedLifecycleCleanup"); SceneManager.SetActiveScene(clean);
            foreach (var name in new[] { ScenePaths.Home, ScenePaths.Stage3StreetSandbox })
            { var scene = SceneManager.GetSceneByName(name); if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene); }
        }
        private static IEnumerator Until(Func<bool> predicate)
        { float limit = Time.realtimeSinceStartup + 15; while (!predicate()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(limit)); yield return null; } }
    }
}
