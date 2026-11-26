using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.StreetMap;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class RanchMapPlayTests
    {
        private GameBootstrap bootstrap;
        private RunCoordinator run;
        private StreetMapScreen screen;
        private int world;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (var old in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            Time.timeScale = 1;
            yield return null;
            yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap);
            yield return Until(() =>
            {
                bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
                return bootstrap != null && bootstrap.Coordinator.Phase == RunPhase.Home;
            }, "Bootstrap → Home");
            run = bootstrap.Coordinator;
            world = LayerMask.GetMask("World");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (bootstrap != null) Object.Destroy(bootstrap.gameObject);
            yield return null;
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("RanchMapTestCleanup"));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
        }

        [UnityTest]
        public IEnumerator RanchPreviewUsesPhysicalGatesAndClearancesAndReturnsAfterRealDeer()
        {
            string profile = Path.Combine(Application.dataPath, "..", "TestResults", "Saves", "ranch-map-" + Guid.NewGuid().ToString("N"));
            run.SetStage5Runtime(new Stage5Runtime(profile));
            var home = Object.FindFirstObjectByType<HomeScreen>();
            home.FixedSeedInput.text = "41";
            ButtonWith("Карта города").onClick.Invoke();
            ButtonWith("Проверка ранчо · 5-1").onClick.Invoke();
            yield return Until(() => run.Phase == RunPhase.InArena, "Home → Ranch");
            screen = Object.FindFirstObjectByType<StreetMapScreen>();
            if (screen.IsPaused) screen.Resume();
            Assert.That(run.CurrentRequest.IsRanchPreview, Is.True);
            Assert.That(run.CurrentRequest.Seed, Is.EqualTo(41));
            Assert.That(screen.Rooms.Select(room => room.RoomId), Is.EqualTo(new[] { "yard", "pen", "barn" }));
            Assert.That(screen.CurrentRoomId, Is.EqualTo("entry"));
            Assert.That(screen.Rooms.Select(room => room.State.Phase), Is.All.EqualTo(RoomPhase.Dormant));
            Assert.That(screen.Portal.IsOpen, Is.False);
            Assert.That(Object.FindFirstObjectByType<TargetRegistry>().Count, Is.Zero);

            foreach (var room in RanchLayout.CreateMap().Rooms)
                foreach (var point in room.Points.Where(candidate => !candidate.IsHeroEntry))
                    Assert.That(Physics2D.OverlapCircle(new Vector2(room.CenterX + point.X, room.CenterY + point.Y),
                        point.ClearanceRadius, world), Is.Null, room.RoomId + "/" + point.PointId);
            foreach (var point in new[] { RanchLayout.AmmoPosition, RanchLayout.ExitPosition })
                Assert.That(Physics2D.OverlapCircle(point, .7f, world), Is.Null);

            var minimap = screen.MiniMap.SafeRoot.Find("FullStreetMap");
            Assert.That(new[] { "entry", "yard", "pen", "barn" }.All(id => minimap.Find(id) != null), Is.True);
            Assert.That(minimap.Cast<Transform>().Count(item => item.name == "Road"), Is.EqualTo(3));
            Assert.That(minimap.Cast<Transform>().Any(item => item.name.Contains("Trap") || item.name.Contains("Deer")), Is.False);
            Vector2 entryMarker = screen.MiniMap.HeroMapPosition;
            Color unvisited = minimap.Find("yard").GetComponent<Image>().color;

            var yard = screen.Rooms[0];
            Assert.That(yard.Doors[0].IsClosed, Is.False);
            Assert.That(yard.Doors[1].IsClosed, Is.True);
            Assert.That(IsBlocked(Vector2.zero, new Vector2(17, 0)), Is.False, "Entry road");
            Move(new Vector2(13.15f, 0));
            yield return new WaitForFixedUpdate();
            Assert.That(yard.State.Phase, Is.EqualTo(RoomPhase.Dormant), "Do not close on the threshold");
            Move(new Vector2(17, 0));
            yield return Until(() => yard.State.AliveCount >= 4, "yard spawns");
            yield return null;
            Assert.That(yard.Doors.All(door => door.IsClosed), Is.True);
            Assert.That(IsBlocked(new Vector2(29, 0), new Vector2(36, 0)), Is.True);
            Assert.That(minimap.Find("yard").GetComponent<Image>().color, Is.Not.EqualTo(unvisited));
            Assert.That(screen.MiniMap.HeroMapPosition.x, Is.GreaterThan(entryMarker.x));
            Vector2 projected = (new Vector2(17, 0) - RanchLayout.CameraBounds.center) * 2.4f;
            Assert.That(Vector2.Distance(screen.MiniMap.HeroMapPosition, projected), Is.LessThan(.1f));
            KillRequired(yard);
            Assert.That(yard.Doors.All(door => !door.IsClosed), Is.True);
            Assert.That(IsBlocked(new Vector2(29, 0), new Vector2(43, 0)), Is.False);
            Assert.That(screen.Portal.IsOpen, Is.False);

            Move(new Vector2(43, 0));
            var pen = screen.Rooms[1];
            yield return Until(() => pen.State.AliveCount >= 4, "pen spawns");
            var trap = Object.FindFirstObjectByType<RanchTrap>();
            Assert.That((Vector2)trap.transform.position, Is.EqualTo(RanchLayout.TrapPosition));
            Assert.That(IsBlocked(new Vector2(43, 0), new Vector2(59, 0)), Is.False, "Safe route past trap");
            Assert.That(trap.IsSpent, Is.False);
            Assert.That(pen.Doors.All(door => door.IsClosed), Is.True);
            KillRequired(pen);
            Assert.That(IsBlocked(new Vector2(59, 0), new Vector2(70, 0)), Is.False, "Road to the barn");
            var ammo = Object.FindObjectsByType<RoomPickup>(FindObjectsSortMode.None).Single();
            Assert.That(ammo.Kind, Is.EqualTo(PickupKind.Ammo));
            Assert.That(ammo.Remaining, Is.EqualTo(8));
            Assert.That((Vector2)ammo.transform.position, Is.EqualTo(RanchLayout.AmmoPosition));
            Move(RanchLayout.AmmoPosition);
            int reserve = screen.Player.ReadSnapshot().ReserveAmmo;
            screen.TryExit();
            Assert.That(screen.Player.ReadSnapshot().ReserveAmmo, Is.EqualTo(reserve + 8));
            Assert.That(ammo.IsConsumed, Is.True);
            Assert.That(screen.Portal.IsOpen, Is.False);

            Move(new Vector2(72, 0));
            var barn = screen.Rooms[2];
            yield return Until(() => barn.State.AliveCount == 1, "barn spawns deer");
            var deer = Object.FindFirstObjectByType<RanchDeer>();
            Assert.That(deer.name, Is.EqualTo("ranch/5-1/barn/deer"));
            Assert.That(IsBlocked(new Vector2(84, 0), new Vector2(76, 0), .8f), Is.False, "8-unit charge lane");
            Assert.That(IsBlocked(new Vector2(78, 0), new Vector2(78, 1.5f)), Is.False);
            Assert.That(IsBlocked(new Vector2(78, 0), new Vector2(78, -1.5f)), Is.False);
            deer.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(900, 1_000));
            Assert.That(screen.ClearedCount, Is.EqualTo(3));
            Assert.That(screen.Portal.IsOpen, Is.True);
            Assert.That((Vector2)screen.Portal.transform.position, Is.EqualTo(RanchLayout.ExitPosition));
            screen.TryExit();
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InArena), "Exit requires proximity");
            Move(RanchLayout.ExitPosition);
            screen.TryExit();
            screen.TryExit();
            yield return Until(() => run.Phase == RunPhase.Home, "Ranch → Home");
            Assert.That(run.LastOutcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(run.Stage5.Session.Snapshot.Profile.CompletedRegionIds, Is.Empty);
            Assert.That(!Directory.Exists(profile) || Directory.GetFiles(profile, "*.json").Length == 0, Is.True);
        }

        private void Move(Vector2 location)
        {
            var body = screen.Player.GetComponent<Rigidbody2D>();
            body.transform.position = location;
            body.position = location;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private bool IsBlocked(Vector2 from, Vector2 to, float radius = .45f)
        {
            var difference = to - from;
            return Physics2D.CircleCast(from, radius, difference.normalized, difference.magnitude, world).collider != null;
        }

        private static void KillRequired(RoomController room)
        {
            foreach (var actor in room.GetComponentsInChildren<MonoBehaviour>())
                if (actor is IEnemyTarget enemy && !enemy.IsDead)
                    actor.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(900, 1_000));
            Assert.That(room.State.Phase, Is.EqualTo(RoomPhase.Cleared));
        }

        private static Button ButtonWith(string label) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(button => button.GetComponentInChildren<Text>()?.text == label);

        private static IEnumerator Until(Func<bool> condition, string step)
        {
            double limit = Time.realtimeSinceStartupAsDouble + 8;
            while (!condition() && Time.realtimeSinceStartupAsDouble < limit) yield return null;
            Assert.That(condition(), Is.True, step);
        }
    }
}
