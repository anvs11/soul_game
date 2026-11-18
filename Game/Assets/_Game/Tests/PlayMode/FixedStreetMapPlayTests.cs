using System;
using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Encounters;
using System.Linq;
using Game.Input;
using Game.StreetMap;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    // Test-only input provider. Production continues to use LocalInputSource/Player.inputactions.
    public sealed class MapTestCommands : MonoBehaviour, IPlayerInputSource
    {
        public Vector2 Move;
        public PlayerCommand ReadCommand() => new PlayerCommand(Move);
    }
    public sealed class FixedStreetMapPlayTests
    {
        private GameBootstrap boot;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (var old in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            Time.timeScale = 1;
            yield return null;
            yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap);
            yield return Until(() =>
            {
                boot = Object.FindFirstObjectByType<GameBootstrap>();
                return boot != null && boot.Coordinator != null && boot.Coordinator.Phase == RunPhase.Home;
            });
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (boot != null) Object.Destroy(boot.gameObject);
            yield return null;
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("FixedMapTestCleanup"));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
        }
        private IEnumerator StartMap()
        {
            var home = Object.FindFirstObjectByType<HomeScreen>();
            if (home.IsShowingResult) home.PrimaryButton.onClick.Invoke();
            Assert.That(home.PrimaryButton.IsActive(), Is.True);
            Assert.That(home.SecondaryButton.IsActive(), Is.True);
            Assert.That(home.StreetButton.IsActive(), Is.True);
            Assert.That(home.FixedStreetButton, Is.Not.Null);
            home.FixedStreetButton.onClick.Invoke();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
            Assert.That(boot.Coordinator.ActiveArena, Is.EqualTo(ScenePaths.Stage4StreetMap));
            Assert.That(Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SeededHome_RejectsInvalidInputAndReplaysTheGeneratedWaves()
        {
            var home = Object.FindFirstObjectByType<HomeScreen>();
            home.FixedSeedInput.text = "abc";
            home.SeededStreetButton.onClick.Invoke();
            Assert.That(boot.Coordinator.Phase, Is.EqualTo(RunPhase.Home));
            Assert.That(home.StreetError, Is.Not.Empty);
            string first = null;
            int? freshSeed = null;
            foreach (string input in new[] { "", null, "42" })
            {
                home = Object.FindFirstObjectByType<HomeScreen>();
                if (home.IsShowingResult) home.PrimaryButton.onClick.Invoke();
                home.FixedSeedInput.text = input ?? freshSeed.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                home.SeededStreetButton.onClick.Invoke();
                yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
                var plan = boot.Coordinator.CurrentRequest.FixedEncounter;
                Assert.That(plan, Is.Not.Null);
                var snapshot = string.Join("|", plan.Rooms.SelectMany(r => r.Waves)
                    .SelectMany(w => w.Spawns).Select(s => s.EnemyTypeId + "@" + s.PointId));
                if (input == "") { first = snapshot; freshSeed = plan.Seed; }
                else if (input == null) { Assert.That(plan.Seed, Is.EqualTo(freshSeed)); Assert.That(snapshot, Is.EqualTo(first)); }
                var screen = Object.FindFirstObjectByType<StreetMapScreen>();
                Assert.That(screen.Rooms[1].State.Plan.Waves.Count, Is.EqualTo(2));
                Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IEnemyTarget>(), Is.Empty);
                var room = screen.Rooms[0];
                var body = screen.Player.GetComponent<Rigidbody2D>();
                body.position = StreetMapLayout.Rooms[1].Center;
                yield return Until(() => room.State.AliveCount == room.State.Plan.Waves[0].Spawns.Count);
                var active = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IEnemyTarget>().ToArray();
                Assert.That(active.Length, Is.EqualTo(room.State.AliveCount));
                foreach (var enemy in active.Cast<MonoBehaviour>())
                {
                    var spawn = room.State.Plan.Waves[0].Spawns.Single(s => s.SpawnId == enemy.name);
                    string type = enemy is ShootingEnemy ? EnemyTypes.Shooter : enemy is JumpingEnemy ? EnemyTypes.Jumper : EnemyTypes.Melee;
                    Assert.That(type, Is.EqualTo(spawn.EnemyTypeId));
                }
                if (input == "42")
                {
                    foreach (var enemy in active.Cast<MonoBehaviour>()) Kill(enemy);
                    var secondRoom = screen.Rooms[1];
                    body.position = StreetMapLayout.Rooms[2].Center;
                    yield return Until(() => secondRoom.State.AliveCount == secondRoom.State.Plan.Waves[0].Spawns.Count);
                    foreach (var enemy in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                        .OfType<IEnemyTarget>().Where(e => !e.IsDead).Cast<MonoBehaviour>()) Kill(enemy);
                    Assert.That(secondRoom.State.AliveCount, Is.Zero);
                    Assert.That(secondRoom.Doors.All(d => d.IsClosed), Is.True);
                    var nextWave = secondRoom.State.Plan.Waves[1];
                    yield return Until(() => secondRoom.State.AliveCount == nextWave.Spawns.Count);
                    var secondWaveActors = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                        .OfType<IEnemyTarget>().Where(e => !e.IsDead).Cast<MonoBehaviour>().ToArray();
                    Assert.That(secondWaveActors.Select(a => a.name), Is.EquivalentTo(nextWave.Spawns.Select(s => s.SpawnId)));
                    foreach (var enemy in secondWaveActors) Kill(enemy);
                    Assert.That(secondRoom.State.Phase, Is.EqualTo(RoomPhase.Cleared));
                    Assert.That(secondRoom.Doors.All(d => !d.IsClosed), Is.True);
                }
                screen.Leave();
                yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
            }
        }
        private static void Kill(MonoBehaviour enemy)
        {
            var packet = new DamagePacket(-1, 999);
            enemy.GetComponent<DamageReceiver>().ApplyDamage(in packet);
        }

        [UnityTest]
        public IEnumerator Bootstrap_WalkEntireCorridorRouteWithPhysicsAndFollowingCamera()
        {
            yield return StartMap();
            var screen = Object.FindFirstObjectByType<StreetMapScreen>();
            var hero = screen.Player;
            Assert.That(hero.ReadSnapshot().Health, Is.EqualTo(100));
            Assert.That(hero.ReadSnapshot().Armor, Is.EqualTo(30));
            Assert.That(hero.ReadSnapshot().Magazine, Is.EqualTo(8));
            Assert.That(hero.ReadSnapshot().ReserveAmmo, Is.EqualTo(16));
            Assert.That(Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None), Is.Empty, "Neighbor rooms must remain inactive until entered.");
            var motor = hero.GetComponent<ActorMotor2D>();
            motor.enabled = false;
            var commands = hero.gameObject.AddComponent<MapTestCommands>();
            motor.Configure(commands); motor.enabled = true;
            var body = hero.GetComponent<Rigidbody2D>();
            var waypoints = new[] { new Vector2(10, 0), new Vector2(23, 0), new Vector2(23, 12), new Vector2(23, 24), new Vector2(37, 24), new Vector2(51, 24), new Vector2(65, 24), new Vector2(83, 24) };
            int killed = 0;
            foreach (var destination in waypoints)
            {
                float deadline = Time.realtimeSinceStartup + 8;
                while (Vector2.Distance(body.position, destination) > .18f)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Physical path blocked at " + body.position + " toward " + destination);
                    commands.Move = Vector2.ClampMagnitude((destination - body.position) * 3, 1);
                    foreach (var enemy in Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None))
                    {
                        if (enemy.IsDead) continue;
                        var packet = new DamagePacket(-1, 999);
                        enemy.GetComponent<DamageReceiver>().ApplyDamage(in packet); killed++;
                    }
                    yield return null;
                }
            }
            commands.Move = Vector2.zero;
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(killed, Is.EqualTo(8), "Six ordinary and two explicit final diagnostic enemies.");
            for (int index = 0; index < 5; index++) Assert.That(screen.HasVisited(index), Is.True);
            Assert.That(screen.CurrentRoomId, Is.EqualTo("elite"));
            Assert.That(screen.IsCombatReady, Is.False);
            Assert.That(boot.Coordinator.LastOutcome, Is.Null, "Elite placeholders must not award a win.");
            var camera = Object.FindFirstObjectByType<BoundedFollowCamera>();
            Assert.That(camera.transform.position.x, Is.GreaterThan(75));
            Assert.That((Vector2)camera.transform.position, Is.EqualTo(BoundedFollowCamera.Clamp(hero.transform.position, camera.Bounds, camera.GetComponent<Camera>().orthographicSize, camera.GetComponent<Camera>().aspect)));
            TestContext.WriteLine("S4_04B_WALK 8 waypoints; 5 rooms; 8 diagnostic melee; camera followed; real elite NOT READY.");
        }

        [UnityTest]
        public IEnumerator PhysicalWall_PauseLeaveAndNewAttemptKeepFreshHeroAndResult()
        {
            yield return StartMap();
            var screen = Object.FindFirstObjectByType<StreetMapScreen>();
            var hero = screen.Player;
            string firstId = boot.Coordinator.RunId;
            var motor = hero.GetComponent<ActorMotor2D>(); motor.enabled = false;
            var commands = hero.gameObject.AddComponent<MapTestCommands>(); motor.Configure(commands); motor.enabled = true;
            commands.Move = Vector2.left;
            yield return new WaitForSeconds(1.7f);
            commands.Move = Vector2.zero;
            var body = hero.GetComponent<Rigidbody2D>();
            Assert.That(body.position.x, Is.GreaterThan(-7));
            Assert.That(body.position.x, Is.LessThan(-6), "Hero must reach the wall and stop physically.");
            screen.Pause(); Vector2 stopped = body.position;
            commands.Move = Vector2.right;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(body.position, Is.EqualTo(stopped));
            Assert.That(Time.timeScale, Is.Zero);
            screen.Resume(); commands.Move = Vector2.zero;
            screen.Leave(); screen.Leave();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(boot.Coordinator.LastOutcome, Is.EqualTo(RunOutcome.Abandoned));
            Assert.That(hero == null, Is.True);
            var home = Object.FindFirstObjectByType<HomeScreen>();
            Assert.That(home.IsShowingResult, Is.True);
            Assert.That(home.GetComponentInChildren<UnityEngine.UI.Text>().text, Does.StartWith("1-1:"));
            yield return StartMap();
            Assert.That(boot.Coordinator.RunId, Is.Not.EqualTo(firstId));
            Assert.That((Vector2)Object.FindFirstObjectByType<StreetMapScreen>().Player.transform.position, Is.EqualTo(StreetMapLayout.Entry));
        }

        [UnityTest]
        public IEnumerator HeroDeath_ReturnsHomeAndRetainsDefeatPriority()
        {
            yield return StartMap();
            var screen = Object.FindFirstObjectByType<StreetMapScreen>();
            boot.Coordinator.FinishRun(RunOutcome.Victory);
            var death = new DamagePacket(-1, 999);
            screen.Player.GetComponent<DamageReceiver>().ApplyDamage(in death);
            yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
            Assert.That(boot.Coordinator.LastOutcome, Is.EqualTo(RunOutcome.Defeat));
            Assert.That(Object.FindFirstObjectByType<StreetMapScreen>(), Is.Null);
        }
        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Scene flow timed out.");
                yield return null;
            }
        }
    }
}
