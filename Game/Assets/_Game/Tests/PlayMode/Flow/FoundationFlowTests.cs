using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Sandbox;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Flow
{
    public sealed class FoundationFlowTests
    {
        private GameBootstrap bootstrap;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.CanStreamedLevelBeLoaded(ScenePaths.Bootstrap), Is.True,
                "First run Game/Setup/Create foundation scenes outside Play.");
            foreach (var existing in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(existing.gameObject);
            yield return null;
            yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap, LoadSceneMode.Single);
            yield return Until(() =>
            {
                bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
                return bootstrap != null && bootstrap.Coordinator != null &&
                    bootstrap.Coordinator.Phase == RunPhase.Home;
            }, "Bootstrap did not reach Home.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var existing in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(existing.gameObject);
            Time.timeScale = 1;
            yield return null;
            // Runtime-only cleanup; no scene assets or user files are deleted.
            var empty = SceneManager.CreateScene("FoundationTestCleanup");
            SceneManager.SetActiveScene(empty);
            foreach (var name in new[] { ScenePaths.Bootstrap, ScenePaths.Home, ScenePaths.CombatSandbox })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator Buttons_TenRoundTrips_KeepOneServiceRootAndFreshScreens()
        {
            var ids = new HashSet<string>();
            for (int index = 0; index < 10; index++)
            {
                var home = Object.FindFirstObjectByType<HomeScreen>();
                Assert.That(home, Is.Not.Null);
                if (home.IsShowingResult) home.PrimaryButton.onClick.Invoke();
                home.PrimaryButton.onClick.Invoke();
                bootstrap.Coordinator.StartRun(RunRequest.Sandbox(999)); // Duplicate during load.
                yield return Until(() => bootstrap.Coordinator.Phase == RunPhase.InArena, "Arena not loaded.");
                Assert.That(home == null, Is.True, "Old Home screen survived scene unload.");
                Assert.That(ids.Add(bootstrap.Coordinator.RunId), Is.True);
                AssertOneSceneContext();
                var arena = Object.FindFirstObjectByType<ArenaController>();
                Assert.That(arena, Is.Not.Null);
                arena.TryExit();
                Assert.That(bootstrap.Coordinator.LastOutcome, Is.Null);
                foreach (var enemy in Object.FindObjectsByType<Game.AI.SandboxEnemy>(FindObjectsSortMode.None))
                {
                    var hit = new Game.Combat.DamagePacket(-1, 100);
                    enemy.GetComponent<Game.Actors.DamageReceiver>().ApplyDamage(in hit);
                }
                Assert.That(arena.RemainingEnemies, Is.Zero);
                arena.TryExit();
                Assert.That(bootstrap.Coordinator.LastOutcome, Is.Null, "Exit from far away must fail.");
                arena.Player.transform.position = arena.ExitPosition;
                arena.TryExit(); arena.TryExit();
                yield return Until(() => bootstrap.Coordinator.Phase == RunPhase.Home, "Home not loaded.");
                Assert.That(arena == null, Is.True, "Old arena screen survived scene unload.");
                Assert.That(bootstrap.Coordinator.LastOutcome, Is.EqualTo(RunOutcome.Victory));
                AssertOneSceneContext();
            }
        }

        [UnityTest]
        public IEnumerator SameFrameDeathAndVictory_ReturnHomeWithDefeat()
        {
            bootstrap.Coordinator.StartRun(RunRequest.Sandbox());
            yield return Until(() => bootstrap.Coordinator.Phase == RunPhase.InArena, "Arena not loaded.");
            bootstrap.Coordinator.FinishRun(RunOutcome.Victory);
            var death = new Game.Combat.DamagePacket(-1, 999);
            Object.FindFirstObjectByType<ArenaController>().Player.GetComponent<Game.Actors.DamageReceiver>().ApplyDamage(in death);
            yield return Until(() => bootstrap.Coordinator.Phase == RunPhase.Home, "Home not loaded.");
            Assert.That(bootstrap.Coordinator.LastOutcome, Is.EqualTo(RunOutcome.Defeat));
            AssertOneSceneContext();
        }

        [UnityTest]
        public IEnumerator DuplicateBootstrap_DoesNotCreateAnotherServiceOrReloadHome()
        {
            var home = Object.FindFirstObjectByType<HomeScreen>();
            var duplicate = new GameObject("DuplicateBootstrap");
            duplicate.AddComponent<GameBootstrap>();
            yield return null;
            Assert.That(duplicate == null, Is.True);
            Assert.That(Object.FindFirstObjectByType<HomeScreen>(), Is.SameAs(home));
            Assert.That(bootstrap.Coordinator.Phase, Is.EqualTo(RunPhase.Home));
            AssertOneSceneContext();
        }

        private static void AssertOneSceneContext()
        {
            Assert.That(Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<SceneFlow>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        private static IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!condition())
            {
                if (Time.realtimeSinceStartup >= deadline) Assert.Fail(message);
                yield return null;
            }
        }
    }
}
