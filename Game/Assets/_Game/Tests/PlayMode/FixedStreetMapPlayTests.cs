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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    // Test-only input provider. Production continues to use LocalInputSource/Player.inputactions.
    public sealed class MapTestCommands : MonoBehaviour, IPlayerInputSource
    {
        public Vector2 Move;
        public PlayerCommand ReadCommand() => new PlayerCommand(Move);
    }
    // The batch Editor can route automatic events away from the unfocused Game View.
    [DefaultExecutionOrder(-1000)]
    public sealed class MapTestInputPump : MonoBehaviour
    {
        public Action ApplyState;
        private void Update()
        {
            InputSystem.Update();
            var apply = ApplyState;
            ApplyState = null;
            apply?.Invoke();
        }
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
        public IEnumerator DodgeKeyboardAndGamepad_UseOnePressAndRespectTheWall()
        {
            yield return StartMap();
            var screen = Object.FindFirstObjectByType<StreetMapScreen>();
            var hero = screen.Player;
            var dodge = hero.GetComponent<PlayerDodgeController>();
            var body = hero.GetComponent<Rigidbody2D>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();
            var updateMode = InputSystem.settings.updateMode;
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var pump = screen.gameObject.AddComponent<MapTestInputPump>();
            try
            {
                hero.GetComponent<LocalInputSource>().SendMessage("OnApplicationFocus", true);
                hero.GetComponent<LocalInputSource>().SendMessage("OnApplicationPause", false);
                if (screen.IsPaused) screen.Resume();
                yield return null;
                // No keyboard window is present in Windows batch. Change the test device
                // after its update so the same real action map samples it before the motor.
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.D, Key.LeftShift));
                yield return null;
                yield return null;
                var sampled = hero.GetComponent<LocalInputSource>().ReadCommand();
                Assert.That(dodge.CooldownRemaining, Is.Positive,
                    $"Shift did not start a dodge. Key={keyboard.leftShiftKey.isPressed}, D={keyboard.dKey.isPressed}, move={sampled.Move}, paused={screen.IsPaused}, pos={body.position}, direction={dodge.Direction}");
                Assert.That(dodge.Direction, Is.EqualTo(Vector2.right));
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.LeftShift));
                yield return new WaitForSeconds(PlayerDodgeController.Cooldown + .15f);
                Assert.That(dodge.IsDodging, Is.False);
                Assert.That(dodge.CooldownRemaining, Is.Zero, "Holding Shift must not repeat after cooldown.");
                Assert.That(body.position.x, Is.EqualTo(2.4f).Within(.25f));

                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                body.position = new Vector2(-6.1f, 0);
                Physics2D.SyncTransforms();
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.left }.WithButton(GamepadButton.RightShoulder));
                yield return Until(() => dodge.IsDodging, "Right bumper did not start a dodge toward the wall.");
                Assert.That(dodge.Direction, Is.EqualTo(Vector2.left));
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return Until(() => !dodge.IsDodging);
                Assert.That(body.position.x, Is.InRange(-6.60f, -6.3f));
                Assert.That(dodge.CooldownRemaining, Is.Positive);
            }
            finally
            {
                pump.enabled = false;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(pad);
                InputSystem.settings.updateMode = updateMode;
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        [UnityTest]
        public IEnumerator DodgeScreenButton_UsesSecondPointerClearsOnFocusAndKeepsPauseMenuSafe()
        {
            yield return StartMap();
            var screen = Object.FindFirstObjectByType<StreetMapScreen>();
            var controls = screen.GetComponent<BattleControls>();
            var hero = screen.Player;
            var dodge = hero.GetComponent<PlayerDodgeController>();
            var input = hero.GetComponent<LocalInputSource>();
            input.SendMessage("OnApplicationFocus", true);
            input.SendMessage("OnApplicationPause", false);
            if (screen.IsPaused) screen.Resume();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var center = RectTransformUtility.WorldToScreenPoint(null, controls.Stick.transform.position);
            var movePointer = new PointerEventData(EventSystem.current) { pointerId = 1, position = center };
            var dodgePointer = new PointerEventData(EventSystem.current) { pointerId = 2 };
            controls.Stick.OnPointerDown(movePointer);
            movePointer.position += Vector2.right * 90;
            controls.Stick.OnDrag(movePointer);
            yield return null;
            yield return null;
            Assert.That(input.ReadCommand().Move.x, Is.GreaterThan(.1f), $"Virtual stick was not processed. Paused={screen.IsPaused}");
            controls.Dodge.OnPointerDown(dodgePointer);
            yield return Until(() => dodge.IsDodging);
            Assert.That(dodge.Direction.x, Is.GreaterThan(.95f));
            var safe = (RectTransform)screen.transform.Find("BattleControlsCanvas/SafeArea");
            Assert.That(controls.Dodge.transform.IsChildOf(safe), Is.True);
            Assert.That(safe.anchorMax.y, Is.EqualTo(Screen.safeArea.yMax / Screen.height).Within(.001f));
            Assert.That(screen.MiniMap.isActiveAndEnabled, Is.True);

            screen.gameObject.SendMessage("OnApplicationFocus", false);
            input.SendMessage("OnApplicationFocus", false);
            Assert.That(screen.IsPaused, Is.True);
            EventSystem.current.SetSelectedGameObject(controls.HomeButton.gameObject);
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                yield return null;
                yield return null;
                Assert.That(screen.IsPaused, Is.True);
                Assert.That(boot.Coordinator.Phase, Is.EqualTo(RunPhase.InArena));
            }
            finally { InputSystem.RemoveDevice(pad); }
            input.SendMessage("OnApplicationFocus", true);
            screen.Resume();
            yield return new WaitForSeconds(PlayerDodgeController.Cooldown + .15f);
            Assert.That(input.ReadCommand().Move.sqrMagnitude, Is.LessThan(.001f));
            Assert.That(dodge.CooldownRemaining, Is.Zero);
            controls.Dodge.OnPointerDown(dodgePointer);
            yield return Until(() => dodge.IsDodging);
            screen.Leave();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
            yield return StartMap();
            var fresh = Object.FindFirstObjectByType<StreetMapScreen>().Player.GetComponent<PlayerDodgeController>();
            Assert.That(fresh.CooldownRemaining, Is.Zero);
            Assert.That(fresh.IsDodging, Is.False);
        }

        [UnityTest]
        public IEnumerator Stage5Preview_VisitsBothStopsAndReturnsLootToHome()
        {
            var home = Object.FindFirstObjectByType<HomeScreen>();
            home.FixedSeedInput.text = "11";
            home.Stage5PreviewButton.onClick.Invoke();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
            var map = Object.FindFirstObjectByType<StreetMapScreen>();
            var preview = map.GetComponent<Stage5PreviewController>();
            var body = map.Player.GetComponent<Rigidbody2D>();
            var controls = map.GetComponent<BattleControls>();
            var input = map.Player.GetComponent<LocalInputSource>();
            int checkpointBalance = preview.ConfirmedCrystals;

            foreach (Vector2 road in new[] { Stage5PreviewController.BrokerRoad, Stage5PreviewController.DealerRoad })
                Assert.That(Physics2D.OverlapCircle(road, .45f, LayerMask.GetMask("World")), Is.Null);
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return ClearPreviewRoom(map, body, 0);
            Assert.That(preview.ConfirmedCrystals, Is.GreaterThan(checkpointBalance));
            Assert.That(preview.PendingCrystals, Is.Zero);
            Assert.That(preview.Coins, Is.GreaterThanOrEqualTo(3));
            var ammo = GameObject.Find("Магазин");
            Assert.That(ammo, Is.Not.Null);
            Park(body, ammo.transform.position);
            int reserve = map.Player.ReadSnapshot().ReserveAmmo;
            map.TryExit();
            Assert.That(map.Player.ReadSnapshot().ReserveAmmo, Is.EqualTo(reserve + 8));

            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();
            var updateMode = InputSystem.settings.updateMode;
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var pump = map.gameObject.AddComponent<MapTestInputPump>();
            try
            {
                input.SendMessage("OnApplicationFocus", true);
                input.SendMessage("OnApplicationPause", false);
                if (map.IsPaused) map.Resume();
                Park(body, Stage5PreviewController.BrokerRoad);
                int rounds = map.Player.ReadSnapshot().Magazine;
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.Tab, Key.Space));
                yield return Until(() => preview.IsInside);
                Assert.That(map.Player.ReadSnapshot().Magazine, Is.EqualTo(rounds));
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                Assert.That(map.MiniMap.HeroMapPosition, Is.EqualTo(StreetMiniMap.Project(Stage5PreviewController.BrokerRoad)));
                Vector2 inside = body.position;
                Assert.That(Object.FindFirstObjectByType<BoundedFollowCamera>().Bounds.Contains(inside), Is.True);
                Park(body, inside + Vector2.up * 4.2f);
                var npcPointer = new PointerEventData(EventSystem.current) { pointerId = 3 };
                controls.Npc.OnPointerDown(npcPointer);
                yield return Until(() => FindPreviewButton("Согласиться · Tab") != null);
                controls.Npc.OnPointerUp(npcPointer);
                yield return null;
                int initialCoins = preview.Coins;
                FindPreviewButton("Отказаться · E").onClick.Invoke();
                Assert.That(preview.Coins, Is.EqualTo(initialCoins));
                preview.TryVisit();
                yield return null;
                var agree = FindPreviewButton("Согласиться · Tab");
                agree.onClick.Invoke();
                agree.onClick.Invoke();
                Assert.That(preview.Coins, Is.EqualTo(initialCoins - 3));
                Assert.That(GameObject.Find("Усиление"), Is.Null);
                Park(body, inside);
                preview.TryVisit();
                Assert.That(preview.IsInside, Is.False);
                Assert.That(body.position, Is.EqualTo(Stage5PreviewController.BrokerRoad));
                yield return new WaitForSeconds(2.1f);
                Assert.That(GameObject.Find("Усиление"), Is.Not.Null);
                preview.TryVisit();
                Park(body, inside + Vector2.up * 4.2f);
                map.TryExit();
                Assert.That(GameObject.Find("Усиление"), Is.Null);
                Park(body, inside);
                preview.TryVisit();

                yield return ClearPreviewRoom(map, body, 1);
                Park(body, Stage5PreviewController.DealerRoad);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
                yield return Until(() => preview.IsInside);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
                Vector2 dealer = body.position;
                Park(body, dealer + Vector2.up * 4.2f);
                preview.TryVisit();
                yield return null;
                FindPreviewButton("Согласиться · Tab").onClick.Invoke();
                Assert.That(GameObject.Find("Пузырёк Диллера"), Is.Not.Null);
                map.TryExit();
                Assert.That(GameObject.Find("Пузырёк Диллера"), Is.Null);
                yield return Until(() => GameObject.Find("Эффекты").GetComponent<Text>().text.Contains("Пузырёк: ускорение"));
                Park(body, dealer);
                preview.TryVisit();
                yield return ClearPreviewRoom(map, body, 2);
                yield return Until(() => !GameObject.Find("Эффекты").GetComponent<Text>().text.Contains("Пузырёк"));
            }
            finally
            {
                pump.enabled = false;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(pad);
                InputSystem.settings.updateMode = updateMode;
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }

            int confirmed = preview.ConfirmedCrystals;
            map.Leave();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
            home = Object.FindFirstObjectByType<HomeScreen>();
            Assert.That(home.GetComponentsInChildren<Text>().Any(t => t.text.Contains($"баланс в убежище: {confirmed}")), Is.True);
        }

        [UnityTest]
        public IEnumerator Stage5Preview_DealerRejectsAnEmptyWalletAndCanGiveAHarmfulBottle()
        {
            var home = Object.FindFirstObjectByType<HomeScreen>();
            home.FixedSeedInput.text = "34";
            home.Stage5PreviewButton.onClick.Invoke();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
            var map = Object.FindFirstObjectByType<StreetMapScreen>();
            var preview = map.GetComponent<Stage5PreviewController>();
            var body = map.Player.GetComponent<Rigidbody2D>();
            Park(body, Stage5PreviewController.DealerRoad);
            preview.TryVisit();
            Vector2 arrival = body.position;
            Park(body, arrival + Vector2.up * 4.2f);
            preview.TryVisit();
            yield return null;
            FindPreviewButton("Согласиться · Tab").onClick.Invoke();
            yield return Until(() => GameObject.Find("Ответ").GetComponent<Text>().text.Contains("Не хватает монет"));
            Assert.That(GameObject.Find("Пузырёк Диллера"), Is.Null);
            Assert.That(preview.Coins, Is.Zero);
            map.TryExit();
            Park(body, arrival);
            preview.TryVisit();

            yield return ClearPreviewRoom(map, body, 0);
            yield return ClearPreviewRoom(map, body, 1);
            Park(body, Stage5PreviewController.DealerRoad);
            preview.TryVisit();
            Park(body, arrival + Vector2.up * 4.2f);
            preview.TryVisit();
            yield return null;
            FindPreviewButton("Согласиться · Tab").onClick.Invoke();
            map.TryExit();
            yield return Until(() => GameObject.Find("Эффекты").GetComponent<Text>().text.Contains("Пузырёк: замедление"));

            var hit = new DamagePacket(-1, 999);
            map.Player.GetComponent<DamageReceiver>().ApplyDamage(in hit);
            yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
            Assert.That(boot.Coordinator.LastOutcome, Is.EqualTo(RunOutcome.Defeat));
            home = Object.FindFirstObjectByType<HomeScreen>();
            home.PrimaryButton.onClick.Invoke();
            home.FixedSeedInput.text = "0";
            home.Stage5PreviewButton.onClick.Invoke();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
            var reset = Object.FindFirstObjectByType<Stage5PreviewController>();
            Assert.That(reset.Coins, Is.Zero);
            Assert.That(reset.PendingCrystals, Is.Zero);
            Assert.That(reset.ConfirmedCrystals, Is.GreaterThan(120));
            Assert.That(GameObject.Find("Эффекты").GetComponent<Text>().text, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Stage5Preview_HomePurchaseRetriesAndDoesNotDoubleDebit()
        {
            var home = Object.FindFirstObjectByType<HomeScreen>();
            FindPreviewButton("Профиль Stage 5").onClick.Invoke();
            var scenario = FindPreviewButton("Сменить сценарий профиля");
            var buy = FindPreviewButton("Открыть тестовый вариант · 100");
            scenario.onClick.Invoke();
            buy.onClick.Invoke();
            var balance = GameObject.Find("Кристаллы профиля").GetComponent<Text>();
            Assert.That(balance.text, Does.Contain("120"));
            Assert.That(GameObject.Find("Результат операции").GetComponent<Text>().text, Does.Contain("Повторите"));
            buy.onClick.Invoke();
            buy.onClick.Invoke();
            Assert.That(balance.text, Does.Contain(": 20"));
            Assert.That(buy.GetComponentInChildren<Text>().text, Is.EqualTo("Тестовый вариант выбран"));
            FindPreviewButton("Закрыть").onClick.Invoke();
            home.FixedSeedInput.text = "0";
            home.Stage5PreviewButton.onClick.Invoke();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
            var map = Object.FindFirstObjectByType<StreetMapScreen>();
            var preview = map.GetComponent<Stage5PreviewController>();
            Assert.That(preview.ConfirmedCrystals, Is.EqualTo(20));
            Assert.That(preview.Coins, Is.Zero);
            Park(map.Player.GetComponent<Rigidbody2D>(), Stage5PreviewController.BrokerRoad);
            preview.TryVisit();
            Assert.That(preview.IsInside, Is.False);
            map.Leave();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
            home = Object.FindFirstObjectByType<HomeScreen>();
            home.PrimaryButton.onClick.Invoke();
            FindPreviewButton("Профиль Stage 5").onClick.Invoke();
            scenario = FindPreviewButton("Сменить сценарий профиля");
            scenario.onClick.Invoke();
            scenario.onClick.Invoke();
            Assert.That(GameObject.Find("Состояние профиля").GetComponent<Text>().text, Does.Contain("резервной копии"));
            scenario.onClick.Invoke();
            Assert.That(home.Stage5PreviewButton.interactable, Is.False);
            Assert.That(GameObject.Find("Кристаллы профиля").GetComponent<Text>().text, Does.Contain(": 20"));
            FindPreviewButton("Открыть отдельный профиль").onClick.Invoke();
            Assert.That(GameObject.Find("Кристаллы профиля").GetComponent<Text>().text, Does.Contain(": 0"));
            Assert.That(home.Stage5PreviewButton.interactable, Is.True);
        }

        private static Button FindPreviewButton(string label) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.GetComponentInChildren<Text>()?.text == label);

        private static void Park(Rigidbody2D body, Vector2 position)
        {
            body.position = position;
            body.transform.position = position;
            Physics2D.SyncTransforms();
        }

        private static IEnumerator ClearPreviewRoom(StreetMapScreen map, Rigidbody2D body, int index)
        {
            Park(body, StreetMapLayout.Rooms[index + 1].Center);
            var room = map.Rooms[index];
            yield return Until(() => room.State.AliveCount == room.State.Plan.Waves[0].Spawns.Count);
            var actors = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(actor => actor is IEnemyTarget target && !target.IsDead).ToArray();
            Assert.That(actors.Length, Is.InRange(6, 8));
            foreach (var actor in actors)
            {
                Kill(actor);
                Park(body, actor.transform.position);
                yield return null;
                yield return null;
            }
            Assert.That(room.State.Phase, Is.EqualTo(RoomPhase.Cleared));
        }

        [UnityTest]
        public IEnumerator SeededHome_RejectsInvalidInputAndStartsAllOrdinaryEnemiesTogether()
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
                Assert.That(screen.Rooms.Take(3).All(r => r.State.Plan.Waves.Count == 1), Is.True);
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
                    var actors = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                        .OfType<IEnemyTarget>().Where(e => !e.IsDead).Cast<MonoBehaviour>().ToArray();
                    Assert.That(actors.Length, Is.InRange(6, 8));
                    Assert.That(secondRoom.Doors.All(d => d.IsClosed), Is.True);
                    Assert.That(actors.Select(a => a.name),
                        Is.EquivalentTo(secondRoom.State.Plan.Waves[0].Spawns.Select(s => s.SpawnId)));
                    foreach (var enemy in actors.Take(actors.Length - 1)) Kill(enemy);
                    Assert.That(secondRoom.State.AliveCount, Is.EqualTo(1));
                    Assert.That(secondRoom.Doors.All(d => d.IsClosed), Is.True);
                    Kill(actors.Last());
                    Assert.That(secondRoom.State.AliveCount, Is.Zero);
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
        public IEnumerator EliteAndPickups_TenSeededCyclesKeepRewardsAndMandatoryChildrenInsideRun()
        {
            var seenEliteTypes = new System.Collections.Generic.HashSet<string>();
            int completed = 0;
            for (int cycle = 0; cycle < 10; cycle++)
            {
                var home = Object.FindFirstObjectByType<HomeScreen>();
                if (home.IsShowingResult) home.PrimaryButton.onClick.Invoke();
                home.FixedSeedInput.text = (cycle == 0 ? 42 : cycle).ToString(System.Globalization.CultureInfo.InvariantCulture);
                home.SeededStreetButton.onClick.Invoke();
                yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
                var map = Object.FindFirstObjectByType<StreetMapScreen>();
                var hero = map.Player;
                var body = hero.GetComponent<Rigidbody2D>();
                var receiver = hero.GetComponent<DamageReceiver>();
                string runId = boot.Coordinator.RunId;
                Assert.That(hero.ReadSnapshot().Magazine, Is.EqualTo(8));
                Assert.That(hero.ReadSnapshot().ReserveAmmo, Is.EqualTo(16));
                Assert.That(Object.FindObjectsByType<RoomPickup>(FindObjectsSortMode.None), Is.Empty);
                var projectile = new ProjectileSpawnRequest(body.position, Vector2.down, 1f, 30f, .1f, 0,
                    hero.GetComponent<ActorIdentity>().ActorId, CombatFaction.Player, new DamagePacket(-1, 1));
                Assert.That(hero.GetComponent<ProjectilePool>().TrySpawn(in projectile), Is.True);
                bool defeat = cycle == 8, abandon = cycle == 9;
                for (int index = 0; index < (defeat || abandon ? 2 : map.Rooms.Length); index++)
                {
                    var room = map.Rooms[index];
                    body.position = StreetMapLayout.Rooms[index + 1].Center;
                    yield return Until(() => room.State.AliveCount > 0);
                    if (index == 1 && (defeat || abandon))
                    {
                        Assert.That(room.State.AliveCount, Is.InRange(6, 8));
                        Assert.That(room.Doors.All(d => d.IsClosed), Is.True);
                        break;
                    }
                    if (index == 3)
                    {
                        var elites = Object.FindObjectsByType<EliteEnemy>(FindObjectsSortMode.None);
                        Assert.That(elites.Length, Is.EqualTo(3));
                        var expected = room.State.Plan.Waves[0].Spawns;
                        Assert.That(elites.Select(e => e.EnemyTypeId), Is.EquivalentTo(expected.Select(s => s.EnemyTypeId)));
                        Assert.That(elites.Select(e => e.EnemyTypeId).Distinct().Count(), Is.EqualTo(2));
                        foreach (var elite in elites)
                            Assert.That(StreetMapLayout.Rooms[4].Bounds.Contains(Vector2Int.FloorToInt(elite.transform.position)),
                                Is.True, "An elite must appear in its planned room.");
                        foreach (var elite in elites) seenEliteTypes.Add(elite.EnemyTypeId);
                        if (cycle == 0)
                        {
                            var grenadier = elites.OfType<SlimeGrenadier>().First();
                            Apply(grenadier.GetComponent<DamageReceiver>(), 100);
                            yield return Until(() => grenadier.SummonAttempts == 4);
                            var children = Object.FindObjectsByType<MiniSlime>(FindObjectsSortMode.None);
                            Assert.That(children.Length, Is.EqualTo(4));
                            foreach (var elite in elites) Kill(elite);
                            Assert.That(room.State.AliveCount, Is.EqualTo(4));
                            Assert.That(room.Doors.All(d => d.IsClosed), Is.True);
                            yield return new WaitForFixedUpdate();
                            Assert.That(grenadier.ActiveGrenades, Is.Zero);
                            foreach (var child in children) Kill(child);
                        }
                    }
                    yield return Until(() => room.State.Phase == RoomPhase.Cleared || room.State.AliveCount > 0);
                    float waveDeadline = Time.realtimeSinceStartup + 12f;
                    while (room.State.Phase != RoomPhase.Cleared)
                    {
                        Assert.That(Time.realtimeSinceStartup, Is.LessThan(waveDeadline), "Room stalled: " + room.RoomId);
                        foreach (var actor in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                            .OfType<IEnemyTarget>().Where(e => !e.IsDead).Cast<MonoBehaviour>()) Kill(actor);
                        yield return null;
                    }
                    if (room.State.Phase != RoomPhase.Cleared) break;
                    yield return new WaitForFixedUpdate();
                    var drops = Object.FindObjectsByType<RoomPickup>(FindObjectsSortMode.None)
                        .Where(p => p.PickupId.StartsWith(runId + "/" + room.RoomId + "/")).ToArray();
                    Assert.That(drops.Length, Is.EqualTo(index == 3 ? 3 : 1));
                    Assert.That(drops.Select(d => d.PickupId).Distinct().Count(), Is.EqualTo(drops.Length));
                    if (index == 0)
                    {
                        var before = hero.ReadSnapshot();
                        map.TryExit();
                        map.TryExit();
                        Assert.That(hero.ReadSnapshot().ReserveAmmo, Is.EqualTo(before.ReserveAmmo + 6));
                        Assert.That(hero.ReadSnapshot().Magazine, Is.EqualTo(before.Magazine));
                    }
                    if (index == 1)
                    {
                        map.TryExit();
                        Assert.That(drops[0].Remaining, Is.EqualTo(10));
                        Apply(receiver, 40);
                        map.TryExit();
                        Assert.That(hero.ReadSnapshot().Health, Is.EqualTo(100));
                    }
                    if (index == 2)
                    {
                        map.Pause();
                        map.TryExit();
                        Assert.That(drops[0].Remaining, Is.EqualTo(5));
                        map.Resume();
                        map.TryExit();
                        Assert.That(hero.ReadSnapshot().Armor, Is.EqualTo(5));
                    }
                }
                if (defeat) Apply(receiver, 999);
                else if (abandon) map.Leave();
                else
                {
                    Assert.That(map.Portal.IsOpen, Is.True);
                    body.position = map.Portal.transform.position;
                    yield return new WaitForFixedUpdate();
                    yield return null;
                    map.TryExit();
                    yield return null;
                    if (boot.Coordinator.Phase == RunPhase.InArena) map.TryExit();
                    completed++;
                }
                yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
                Assert.That(boot.Coordinator.LastOutcome, Is.EqualTo(defeat ? RunOutcome.Defeat : abandon ? RunOutcome.Abandoned : RunOutcome.Victory));
                Assert.That(Object.FindObjectsByType<RoomPickup>(FindObjectsSortMode.None), Is.Empty);
                Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None), Is.Empty);
            }
            Assert.That(completed, Is.EqualTo(8));
            Assert.That(seenEliteTypes.Count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator NurseMist_RejectsHitsInSlimeAndReleasesOnlyItsOwnEffects()
        {
            yield return StartMap();
            var hero = Object.FindFirstObjectByType<StreetMapScreen>().Player;
            var statuses = hero.GetComponent<ActorStatusController>();
            var registry = Object.FindFirstObjectByType<TargetRegistry>();
            var assets = Resources.Load<Stage4CombatAssets>(Stage4CombatAssets.ResourceName);
            var nurse = (TeleportNurse)assets.CreateElite(EnemyTypes.TeleportNurse, null);
            nurse.transform.position = new Vector2(4, 0);
            nurse.Initialize(registry, hero);
            nurse.gameObject.SetActive(true);
            var hp = nurse.GetComponent<DamageReceiver>();
            Assert.That(hp.MaxHealth, Is.EqualTo(120));
            Apply(hp, 60);
            yield return Until(() => nurse.IsSlime);
            Assert.That(registry.Count, Is.Zero);
            Assert.That(Apply(hp, 15), Is.False);
            statuses.TryApply(new ActorStatusEffect(9001, "other", ActorStatusKind.Slow, 1f, true, .8f));
            yield return Until(() => statuses.Contains(nurse.ActorId, "mist_stun"));
            Assert.That(statuses.StunRemaining, Is.InRange(1.9f, 2f));
            Assert.That(hero.GetComponent<ActorMotor2D>().Permissions.CanAttack, Is.False);
            Assert.That(registry.Count, Is.EqualTo(1));
            yield return Until(() => statuses.StunRemaining <= 0);
            Assert.That(hero.GetComponent<ActorMotor2D>().Permissions.CanAttack, Is.True);
            Assert.That(statuses.Contains(nurse.ActorId, "mist_poison"), Is.True);
            Apply(hp, 999);
            Assert.That(statuses.Contains(nurse.ActorId, "mist_poison"), Is.False);
            Assert.That(statuses.Contains(nurse.ActorId, "mist_slow"), Is.False);
            Assert.That(statuses.Contains(9001, "other"), Is.True);
        }

        [UnityTest]
        public IEnumerator Shield_BlocksFrontProjectilesAndTriggersRamAt100Health()
        {
            yield return StartMap();
            var hero = Object.FindFirstObjectByType<StreetMapScreen>().Player;
            var assets = Resources.Load<Stage4CombatAssets>(Stage4CombatAssets.ResourceName);
            var guard = (ShieldShotgun)assets.CreateElite(EnemyTypes.ShieldShotgun, null);
            guard.transform.position = new Vector2(3, 0);
            guard.Initialize(Object.FindFirstObjectByType<TargetRegistry>(), hero);
            guard.gameObject.SetActive(true);
            var hp = guard.GetComponent<DamageReceiver>();
            int attacker = hero.GetComponent<ActorIdentity>().ActorId;
            var pool = hero.GetComponent<ProjectilePool>();
            int enemyLayer = 1 << guard.gameObject.layer;
            var front = new ProjectileSpawnRequest(new Vector2(1.5f, 0), Vector2.right, 30f, 1f,
                .1f, enemyLayer, attacker, CombatFaction.Player, new DamagePacket(attacker, 100));
            var rear = new ProjectileSpawnRequest(new Vector2(4.5f, 0), Vector2.left, 30f, 1f,
                .1f, enemyLayer, attacker, CombatFaction.Player,
                new DamagePacket(attacker, 25, protectionSecondsOverride: 0));
            Assert.That(hp.MaxHealth, Is.EqualTo(200f));
            Assert.That(pool.TrySpawn(in front), Is.True);
            yield return Until(() => pool.ActiveCount == 0);
            Assert.That(hp.Health, Is.EqualTo(200f));
            Assert.That(guard.SpecialUsed, Is.False);
            Assert.That(pool.TrySpawn(in rear), Is.True);
            yield return Until(() => pool.ActiveCount == 0);
            Assert.That(hp.Health, Is.EqualTo(175f), "The shot comes from behind while the hero stays in front.");
            hero.transform.position = new Vector2(5, 0);
            Physics2D.SyncTransforms();
            Apply(hp, 75, attacker);
            Assert.That(hp.Health, Is.EqualTo(100f));
            CreateTestWall(new Vector2(4, 0));
            yield return Until(() => guard.SpecialUsed);
            yield return new WaitForSeconds(1.1f);
            Assert.That(guard.GetComponent<Rigidbody2D>().position.x, Is.LessThan(3.3f), "The ram stops at World.");
            Assert.That(hero.ReadSnapshot().Armor, Is.EqualTo(30), "Charge and shotgun cannot hit through the wall.");
        }

        [UnityTest]
        public IEnumerator Nurse_DirectBoltBindsAndSlimeTimesOutBehindWorld()
        {
            yield return StartMap();
            var hero = Object.FindFirstObjectByType<StreetMapScreen>().Player;
            var assets = Resources.Load<Stage4CombatAssets>(Stage4CombatAssets.ResourceName);
            var registry = Object.FindFirstObjectByType<TargetRegistry>();
            var statuses = hero.GetComponent<ActorStatusController>();
            var nurse = (TeleportNurse)assets.CreateElite(EnemyTypes.TeleportNurse, null);
            nurse.transform.position = new Vector2(3f, 0);
            nurse.Initialize(registry, hero);
            nurse.gameObject.SetActive(true);
            yield return Until(() => statuses.Contains(nurse.ActorId, "bind_1"));
            float hitTime = Time.time;
            Assert.That(statuses.Contains(nurse.ActorId, "syringe_poison_1"), Is.True);
            var motor = hero.GetComponent<ActorMotor2D>();
            Assert.That(motor.Permissions.CanMove, Is.False);
            Assert.That(motor.Permissions.CanAttack, Is.False);
            Assert.That(Vector2.Distance(nurse.transform.position, hero.transform.position), Is.LessThanOrEqualTo(1.5f));
            yield return Until(() => motor.Permissions.CanMove);
            Assert.That(Time.time - hitTime, Is.InRange(.9f, 1.15f));
            float escapeDeadline = hitTime + 3.1f;
            while (Time.time < escapeDeadline)
            {
                Assert.That(motor.Permissions.CanMove && motor.Permissions.CanAttack, Is.True,
                    "The nurse must leave a chance to escape even if the hero stays within range.");
                yield return null;
            }
            yield return Until(() => statuses.Contains(nurse.ActorId, "bind_2"));
            Assert.That(Time.time - hitTime, Is.InRange(3.2f, 4f));
            var hp = nurse.GetComponent<DamageReceiver>();
            var body = nurse.GetComponent<Rigidbody2D>();
            body.position = new Vector2(3f, 0);
            CreateTestWall(new Vector2(1.2f, 0));
            Apply(hp, 80);
            yield return Until(() => nurse.IsSlime);
            yield return new WaitForSeconds(5f);
            Assert.That(nurse.IsSlime, Is.True);
            Assert.That(body.position.x, Is.GreaterThan(1.5f));
            yield return Until(() => !nurse.IsSlime);
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(statuses.Contains(nurse.ActorId, "mist_stun"), Is.False);
        }

        [UnityTest]
        public IEnumerator Pickups_RejectFullAndDeadReceiversAndKeepPartialReserve()
        {
            yield return StartMap();
            var hero = Object.FindFirstObjectByType<StreetMapScreen>().Player;
            var assets = Resources.Load<Stage4CombatAssets>(Stage4CombatAssets.ResourceName);
            int changes = 0;
            hero.Changed += _ => changes++;
            Assert.That(hero.TryAddReserve(78), Is.EqualTo(78));
            var ammo = assets.CreatePickup(PickupKind.Ammo, Vector2.zero, hero.transform.parent);
            ammo.Initialize("test", "room");
            Assert.That(ammo.TryCollect(hero), Is.EqualTo(2));
            Assert.That(ammo.Remaining, Is.EqualTo(4));
            Assert.That(hero.ReadSnapshot().ReserveAmmo, Is.EqualTo(96));
            Assert.That(hero.ReadSnapshot().Magazine, Is.EqualTo(8));
            Assert.That(ammo.TryCollect(hero), Is.Zero);
            Assert.That(hero.TryHeal(10), Is.Zero);
            Assert.That(hero.TryRepair(float.PositiveInfinity), Is.Zero);
            Assert.That(changes, Is.EqualTo(2));
            Apply(hero.GetComponent<DamageReceiver>(), 999);
            Assert.That(ammo.TryCollect(hero), Is.Zero);
            Assert.That(ammo.Remaining, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator Grenadier_GrenadeStopsAtWorldAndOwnerDeathClearsHazards()
        {
            yield return StartMap();
            var hero = Object.FindFirstObjectByType<StreetMapScreen>().Player;
            var assets = Resources.Load<Stage4CombatAssets>(Stage4CombatAssets.ResourceName);
            var grenadier = (SlimeGrenadier)assets.CreateElite(EnemyTypes.SlimeGrenadier, null);
            grenadier.transform.position = new Vector2(3, 0);
            grenadier.Initialize(Object.FindFirstObjectByType<TargetRegistry>(), hero);
            grenadier.gameObject.SetActive(true);
            CreateTestWall(new Vector2(1.2f, 0));
            yield return Until(() => grenadier.ActiveGrenades == 1);
            yield return new WaitForSeconds(1.6f);
            Assert.That(hero.ReadSnapshot().Armor, Is.EqualTo(30));
            Assert.That(grenadier.ActiveGrenades, Is.Positive);
            Apply(grenadier.GetComponent<DamageReceiver>(), 999);
            Assert.That(grenadier.ActiveGrenades, Is.Zero);
            Assert.That(grenadier.SpecialUsed, Is.False, "Direct death must not start a special.");
        }

        private static void CreateTestWall(Vector2 point)
        {
            var wall = new GameObject("Test World wall", typeof(BoxCollider2D));
            wall.transform.position = point;
            wall.layer = LayerMask.NameToLayer("World");
            wall.GetComponent<BoxCollider2D>().size = new Vector2(.25f, 5f);
            Physics2D.SyncTransforms();
        }

        private static bool Apply(DamageReceiver receiver, float amount, int source = -1)
        {
            var packet = new DamagePacket(source, amount, protectionSecondsOverride: 0);
            return receiver.ApplyDamage(in packet).Applied;
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
        private static IEnumerator Until(Func<bool> condition, string failure = "Scene flow timed out.")
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), failure);
                yield return null;
            }
        }
    }
}
