using System;
using System.Collections;
using System.IO;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Encounters;
using Game.Progression;
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
        public bool Attack, Switch;
        public PlayerCommand ReadCommand() => new PlayerCommand(Move, attackHeld: Attack, switchPressed: Switch);
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
            Assert.That(GameObject.Find("Вход Барыга").transform.position.x, Is.EqualTo(21).Within(.15f));
            Assert.That(GameObject.Find("Вход Диллер").transform.position.y, Is.EqualTo(22).Within(.15f));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return ClearPreviewRoom(map, body, 0);
            Assert.That(preview.ConfirmedCrystals, Is.GreaterThan(checkpointBalance));
            Assert.That(preview.PendingCrystals, Is.Zero);
            Assert.That(preview.Coins, Is.GreaterThanOrEqualTo(3));
            var ammo = GameObject.Find("Патроны в теле");
            Assert.That(ammo, Is.Not.Null);
            Assert.That(ammo.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(4));
            Park(body, ammo.transform.position);
            int reserve = map.Player.ReadSnapshot().ReserveAmmo;
            int bodies = map.GetComponentsInChildren<SpriteRenderer>().Count(sprite => sprite.name == "Тело зомби");
            yield return null;
            Assert.That(map.Player.ReadSnapshot().ReserveAmmo, Is.EqualTo(reserve));
            map.TryExit();
            Assert.That(map.Player.ReadSnapshot().ReserveAmmo, Is.EqualTo(reserve + 8));
            Assert.That(ammo.activeSelf, Is.False);
            Assert.That(map.GetComponentsInChildren<SpriteRenderer>().Count(sprite => sprite.name == "Тело зомби"), Is.EqualTo(bodies));

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
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.Tab));
                yield return Until(() => preview.IsInside, "Tab must open the passage on its own.");
                Assert.That(map.Player.ReadSnapshot().Magazine, Is.EqualTo(rounds));
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                Assert.That(map.MiniMap.HeroMapPosition, Is.EqualTo(StreetMiniMap.Project(Stage5PreviewController.BrokerRoad)));
                Vector2 inside = body.position;
                Assert.That(Object.FindFirstObjectByType<BoundedFollowCamera>().Bounds.Contains(inside), Is.True);
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.Space));
                yield return Until(() => !preview.IsInside, "Space must leave through the nearby exit.");
                yield return null;
                Assert.That(preview.IsInside, Is.False, "Holding Space must not re-enter the passage.");
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                Assert.That(controls.Attack.GetComponentInChildren<Text>().text, Is.EqualTo("Пройти"));
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.Space));
                yield return Until(() => preview.IsInside, "The attack key must enter at the passage.");
                Assert.That(map.Player.ReadSnapshot().Magazine, Is.EqualTo(rounds));
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                yield return null;
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
                yield return Until(() => GameObject.Find("Название находки")?.GetComponent<Text>().text == "Улучшенное дуло");
                Assert.That(GameObject.Find("Описание находки").GetComponent<Text>().text, Does.Contain("+1 к урону"));
                map.TryExit();
                Assert.That(GameObject.Find("Усиление"), Is.Null);
                Park(body, inside);
                preview.TryVisit();

                yield return ClearPreviewRoom(map, body, 1);
                var medkit = GameObject.Find("Аптечка · подпись");
                Assert.That(medkit, Is.Null);
                Assert.That(Object.FindObjectsByType<RoomPickup>(FindObjectsSortMode.None), Is.Empty);
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
                Assert.That(GameObject.Find("Колба Диллера"), Is.Not.Null);
                yield return Until(() => GameObject.Find("Название находки")?.GetComponent<Text>().text == "Колба Диллера");
                Assert.That(GameObject.Find("Описание находки").GetComponent<Text>().text, Does.Contain("на ближайший бой"));
                map.TryExit();
                Assert.That(GameObject.Find("Колба Диллера"), Is.Null);
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
        public IEnumerator Stage5Preview_SearchesCarsAndKeepsOnlyMedkitsInTheBackpack()
        {
            var home = Object.FindFirstObjectByType<HomeScreen>();
            home.FixedSeedInput.text = "11";
            home.Stage5PreviewButton.onClick.Invoke();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
            var map = Object.FindFirstObjectByType<StreetMapScreen>();
            var body = map.Player.GetComponent<Rigidbody2D>();
            var cars = map.GetComponentsInChildren<BoxCollider2D>().Where(x => x.name == "Сгоревшая машина")
                .OrderBy(x => x.transform.position.x).ThenBy(x => x.transform.position.y).ToArray();
            Assert.That(cars.Length, Is.EqualTo(4));
            var reachable = StreetMapCatalog.Reachable(.45f);
            Assert.That(reachable.Contains(new Vector2Int(85, 24)), Is.True);
            foreach (var room in StreetMapCatalog.Create().Rooms)
            foreach (var point in room.Points)
                Assert.That(Physics2D.OverlapCircle(new Vector2(room.CenterX + point.X, room.CenterY + point.Y),
                    point.ClearanceRadius, LayerMask.GetMask("World")), Is.Null, point.PointId);
            var firstRoom = StreetMapLayout.Rooms[1];
            var secondRoom = StreetMapLayout.Rooms[2];
            var firstCar = cars.Single(car => firstRoom.Bounds.Contains(Vector2Int.FloorToInt(car.transform.position)));
            var secondCar = cars.Single(car => secondRoom.Bounds.Contains(Vector2Int.FloorToInt(car.transform.position)));
            Vector2 Approach(BoxCollider2D car)
            {
                Vector2 point = car.transform.position;
                var room = StreetMapLayout.Rooms.Single(r => r.Bounds.Contains(Vector2Int.FloorToInt(point)));
                return point + Vector2.right * Mathf.Sign(room.Center.x - point.x) * 1.14f;
            }

            Park(body, Approach(firstCar));
            map.TryExit();
            yield return Until(() => FindPreviewButton("В рюкзак · E") != null);
            Assert.That(FindPreviewButton("Применить · Tab").interactable, Is.False);
            var store = FindPreviewButton("В рюкзак · E");
            store.onClick.Invoke();
            store.onClick.Invoke();
            Assert.That(map.Player.ReadSnapshot().Health, Is.EqualTo(100));
            yield return Until(() => FindPreviewButton("Рюкзак · B · 1") != null);
            var droppedArmor = GameObject.Find("Броня на земле");
            Assert.That(droppedArmor, Is.Not.Null, "A full armor bar must not prevent searching the car.");
            map.TryExit();
            Assert.That(droppedArmor.activeSelf, Is.True);

            Apply(map.Player.GetComponent<DamageReceiver>(), 60);
            FindPreviewButton("Рюкзак · B · 1").onClick.Invoke();
            yield return Until(() => FindPreviewButton("Применить · Tab") != null);
            Assert.That(FindPreviewButton("В рюкзак · E"), Is.Null);
            FindPreviewButton("Применить · Tab").onClick.Invoke();
            Assert.That(map.Player.ReadSnapshot().Health, Is.EqualTo(80));
            FindPreviewButton("Закрыть · B").onClick.Invoke();

            Park(body, Approach(secondCar));
            map.TryExit();
            yield return Until(() => FindPreviewButton("В рюкзак · E") != null);
            FindPreviewButton("Применить · Tab").onClick.Invoke();
            Assert.That(map.Player.ReadSnapshot().Health, Is.EqualTo(90));
            yield return Until(() => FindPreviewButton("Рюкзак · B · 0") != null);
            foreach (var car in cars)
            {
                Park(body, Approach(car));
                float armorBefore = map.Player.ReadSnapshot().Armor;
                map.TryExit();
                map.TryExit();
                if (car == firstCar)
                    Assert.That(map.Player.ReadSnapshot().Armor - armorBefore, Is.EqualTo(10));
                Assert.That(FindPreviewButton("В рюкзак · E"), Is.Null);
            }
            Assert.That(map.Player.ReadSnapshot().Health, Is.EqualTo(90));
            Assert.That(map.Player.ReadSnapshot().Armor, Is.EqualTo(21));
            yield return ClearPreviewRoom(map, body, 0);
            var corpseArmor = GameObject.Find("Броня в теле");
            Assert.That(corpseArmor, Is.Not.Null);
            Park(body, corpseArmor.transform.position);
            yield return Until(() => GameObject.Find("Подсказка NPC").GetComponent<Text>().text.Contains("Обыскать тело · Броня"));
            Assert.That(map.Player.TryRepair(99), Is.EqualTo(9));
            map.TryExit();
            Assert.That(corpseArmor.activeSelf, Is.False);
            var corpseDrop = GameObject.Find("Броня на земле");
            Assert.That(corpseDrop, Is.Not.Null);
            Park(body, corpseDrop.transform.position);
            map.TryExit();
            Assert.That(corpseDrop.activeSelf, Is.True, "A full armor bar leaves the revealed plate on the ground.");
            yield return new WaitForSeconds(.55f);
            Apply(map.Player.GetComponent<DamageReceiver>(), 2);
            map.TryExit();
            Assert.That(map.Player.ReadSnapshot().Armor, Is.EqualTo(30));
            Assert.That(corpseDrop.activeSelf, Is.False, "Applying any armor must consume the revealed plate.");
            yield return new WaitForSeconds(.55f);
            Apply(map.Player.GetComponent<DamageReceiver>(), 5);
            map.TryExit();
            Assert.That(map.Player.ReadSnapshot().Armor, Is.EqualTo(25), "An already used item must not restore more armor.");
            Assert.That(GameObject.Find("Подсветка двери машины"), Is.Null);
            Assert.That(GameObject.Find("Подсветка Бочка со снабжением"), Is.Null);
            Assert.That(GameObject.Find("Подсветка Металлолом у входа"), Is.Null);
        }

        [UnityTest]
        public IEnumerator Stage5Preview_DealerRejectsAnEmptyWalletAndCanGiveAHarmfulBottle()
        {
            var home = Object.FindFirstObjectByType<HomeScreen>();
            home.FixedSeedInput.text = "87";
            home.Stage5PreviewButton.onClick.Invoke();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena);
            var map = Object.FindFirstObjectByType<StreetMapScreen>();
            var preview = map.GetComponent<Stage5PreviewController>();
            var body = map.Player.GetComponent<Rigidbody2D>();
            var scrap = GameObject.Find("Подсветка Металлолом у входа");
            var barrel = GameObject.Find("Подсветка Бочка со снабжением");
            Assert.That(scrap, Is.Not.Null);
            Assert.That(barrel, Is.Not.Null);
            Park(body, scrap.transform.position);
            map.TryExit();
            Assert.That(scrap.activeSelf, Is.True, "Full armor must leave the item available.");
            Apply(map.Player.GetComponent<DamageReceiver>(), 5);
            map.TryExit();
            Assert.That(scrap.activeSelf, Is.False);
            Park(body, Stage5PreviewController.DealerRoad);
            preview.TryVisit();
            Vector2 arrival = body.position;
            Park(body, arrival + Vector2.up * 4.2f);
            preview.TryVisit();
            yield return null;
            FindPreviewButton("Согласиться · Tab").onClick.Invoke();
            yield return Until(() => GameObject.Find("Ответ").GetComponent<Text>().text.Contains("Не хватает монет"));
            Assert.That(GameObject.Find("Колба Диллера"), Is.Null);
            Assert.That(preview.Coins, Is.Zero);
            map.TryExit();
            Park(body, arrival);
            preview.TryVisit();

            yield return ClearPreviewRoom(map, body, 0);
            yield return ClearPreviewRoom(map, body, 1);
            map.Player.TryRepair(99);
            Apply(map.Player.GetComponent<DamageReceiver>(), 1);
            float armor = map.Player.ReadSnapshot().Armor;
            Park(body, (Vector2)barrel.transform.position + Vector2.right * .72f);
            map.TryExit();
            Assert.That(barrel.activeSelf, Is.False);
            Assert.That(map.Player.ReadSnapshot().Armor, Is.EqualTo(armor));
            var groundArmor = GameObject.Find("Броня на земле");
            Assert.That(groundArmor, Is.Not.Null);
            map.TryExit();
            Assert.That(map.Player.ReadSnapshot().Armor, Is.EqualTo(30));
            Assert.That(groundArmor.activeSelf, Is.False, "A ground find is consumed even if only one point fits.");
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
        public IEnumerator Stage5City_TenAttemptsPersistOnlyClearedRoomsAndReloadThePurchasedPistol()
        {
            string saves = CityTestDirectory();
            boot.Coordinator.SetStage5Runtime(new Stage5Runtime(saves));
            var ids = new System.Collections.Generic.HashSet<string>();
            int balance = 0;
            bool unlocked = false;
            for (int i = 0; i < 10; i++)
            {
                yield return StartCity(i == 0 ? 5 : i);
                var coordinator = boot.Coordinator;
                var map = Object.FindFirstObjectByType<StreetMapScreen>();
                var body = map.Player.GetComponent<Rigidbody2D>();
                var service = coordinator.Stage5.Session;
                Assert.That(ids.Add(coordinator.RunId), Is.True);
                Assert.That(service.Snapshot.Run.RunId, Is.EqualTo(coordinator.RunId));
                Assert.That(service.Snapshot.Run.Coins, Is.Zero);
                Assert.That(map.Player.ReadSnapshot().MagazineCapacity, Is.EqualTo(unlocked ? 10 : 8));
                if (i == 0) Assert.That(service.Snapshot.Run.Npcs, Is.Empty);
                var outcome = i % 3 == 0 ? RunOutcome.Defeat : i % 3 == 1 ? RunOutcome.Abandoned : RunOutcome.Victory;
                if (i == 0)
                {
                    Park(body, StreetMapLayout.Rooms[1].Center);
                    yield return Until(() => map.Rooms[0].State.AliveCount >= 6);
                    var killed = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                        .First(item => item is IEnemyTarget target && !target.IsDead);
                    Kill(killed);
                    Park(body, killed.transform.position);
                    yield return Until(() => service.Snapshot.Run.PendingCrystals > 0);
                    Assert.That(service.Snapshot.Profile.ConfirmedCrystals, Is.Zero);
                }
                else
                {
                    int rooms = outcome == RunOutcome.Victory ? 4 : 1;
                    for (int roomIndex = 0; roomIndex < rooms; roomIndex++)
                    {
                        var room = map.Rooms[roomIndex];
                        balance += room.State.Plan.Waves.SelectMany(w => w.Spawns)
                            .Sum(spawn => CityEconomy.RollLoot(i, spawn.SpawnId, spawn.EnemyTypeId).Crystals);
                        if (roomIndex < 3) yield return ClearPreviewRoom(map, body, roomIndex);
                        else
                        {
                            Park(body, StreetMapLayout.Rooms[4].Center);
                            yield return Until(() => room.State.AliveCount == 3);
                            foreach (var enemy in Object.FindObjectsByType<EliteEnemy>(FindObjectsSortMode.None)) Kill(enemy);
                            yield return Until(() => map.Portal.IsOpen);
                        }
                    }
                    Assert.That(service.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(balance));
                }

                if (outcome == RunOutcome.Defeat) Apply(map.Player.GetComponent<DamageReceiver>(), 999);
                else if (outcome == RunOutcome.Abandoned) map.Leave();
                else
                {
                    Park(body, map.Portal.transform.position);
                    map.TryExit();
                }
                yield return Until(() => coordinator.Phase == RunPhase.Home);
                Assert.That(coordinator.LastOutcome, Is.EqualTo(outcome));
                Assert.That(service.Snapshot.Run, Is.Null);
                Assert.That(service.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(balance));

                var home = Object.FindFirstObjectByType<HomeScreen>();
                Assert.That(home.GetComponentsInChildren<Text>().Any(t => t.text.Contains($"баланс в убежище: {balance}")), Is.True);
                if (i == 1)
                {
                    home.PrimaryButton.onClick.Invoke();
                    FindPreviewButton("Профиль City").onClick.Invoke();
                    var buy = FindPreviewButton("Открыть городской пистолет · 20");
                    Assert.That(buy, Is.Not.Null);
                    buy.onClick.Invoke();
                    balance -= 20;
                    unlocked = true;
                }
                if (i == 0 || i == 1 || i == 4)
                {
                    coordinator.SetStage5Runtime(new Stage5Runtime(saves));
                    Assert.That(coordinator.Stage5.Session.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(balance));
                    Assert.That(coordinator.Stage5.Session.Snapshot.Profile.SelectedWeaponId, Is.EqualTo(unlocked ? "pistol-city" : "pistol"));
                }
            }
        }

        [UnityTest]
        public IEnumerator Stage5City_ConnectsBothNpcOffersAndClearsTheActualBottleEffect()
        {
            boot.Coordinator.SetStage5Runtime(new Stage5Runtime(CityTestDirectory()));
            foreach (int seed in new[] { 0, 7 })
            {
                yield return StartCity(seed);
                var map = Object.FindFirstObjectByType<StreetMapScreen>();
                var city = boot.Coordinator.Stage5.Session;
                var view = map.GetComponent<Stage5PreviewController>();
                var body = map.Player.GetComponent<Rigidbody2D>();
                Assert.That(city.Snapshot.Run.Npcs.Count, Is.EqualTo(2));
                Park(body, Stage5PreviewController.BrokerRoad);
                view.TryVisit();
                Vector2 broker = body.position;
                Park(body, broker + Vector2.up * 4.2f);
                view.TryVisit();
                yield return Until(() => FindPreviewButton("Согласиться · Tab") != null);
                FindPreviewButton("Согласиться · Tab").onClick.Invoke();
                yield return Until(() => GameObject.Find("Ответ").GetComponent<Text>().text.Contains("Не хватает монет"));
                FindPreviewButton("Отказаться · E").onClick.Invoke();
                Park(body, broker);
                view.TryVisit();
                yield return ClearPreviewRoom(map, body, 0);
                Assert.That(view.Coins, Is.GreaterThanOrEqualTo(3));
                Park(body, Stage5PreviewController.BrokerRoad);
                view.TryVisit();
                Park(body, broker + Vector2.up * 4.2f);
                view.TryVisit();
                yield return Until(() => FindPreviewButton("Согласиться · Tab") != null);
                FindPreviewButton("Согласиться · Tab").onClick.Invoke();
                Assert.That(city.Snapshot.Run.Npcs.Single(n => n.Kind == Game.Contracts.ProgressionNpcKind.WeaponDealer).State,
                    Is.EqualTo(Game.Contracts.ProgressionNpcState.Working));
                Park(body, broker);
                view.TryVisit();
                yield return new WaitForSeconds(2.1f);
                Park(body, Stage5PreviewController.BrokerRoad);
                view.TryVisit();
                Park(body, broker + Vector2.up * 4.2f);
                map.TryExit();
                Assert.That(city.Snapshot.Run.Npcs.Single(n => n.Kind == Game.Contracts.ProgressionNpcKind.WeaponDealer).State,
                    Is.EqualTo(Game.Contracts.ProgressionNpcState.ItemUsed));
                Park(body, broker);
                view.TryVisit();
                yield return ClearPreviewRoom(map, body, 1);
                Park(body, Stage5PreviewController.DealerRoad);
                view.TryVisit();
                Vector2 dealer = body.position;
                Park(body, dealer + Vector2.up * 4.2f);
                view.TryVisit();
                yield return Until(() => FindPreviewButton("Согласиться · Tab") != null);
                FindPreviewButton("Согласиться · Tab").onClick.Invoke();
                Assert.That(city.Snapshot.Run.ActiveBottleEffects, Is.Empty);
                map.TryExit();
                Assert.That(city.Snapshot.Run.ActiveBottleEffect?.Kind, Is.EqualTo(seed == 0
                    ? Game.Contracts.ProgressionEffectKind.Beneficial : Game.Contracts.ProgressionEffectKind.Harmful));
                Park(body, dealer);
                view.TryVisit();
                yield return ClearPreviewRoom(map, body, 2);
                Assert.That(city.Snapshot.Run.ActiveBottleEffects, Is.Empty);
                map.Leave();
                yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
            }
        }

        [UnityTest]
        public IEnumerator Stage6City_TutorialCanBeSkippedAndReplayedFromHome()
        {
            string saves = CityTestDirectory();
            boot.Coordinator.SetStage5Runtime(new Stage5Runtime(saves));
            yield return StartCity(5);
            var map = Object.FindFirstObjectByType<StreetMapScreen>();
            Assert.That(GameObject.Find("City facades and street surface"), Is.Not.Null);
            Assert.That(GameObject.Find("Подсказка City").GetComponent<Text>().text, Does.Contain("WASD"));
            map.Pause();
            yield return null;
            Assert.That(FindPreviewButton("Пропустить"), Is.Null);
            map.Resume();
            yield return Until(() => FindPreviewButton("Пропустить") != null);
            FindPreviewButton("Дальше").onClick.Invoke();
            Assert.That(GameObject.Find("Подсказка City").GetComponent<Text>().text, Does.Contain("Space"));
            FindPreviewButton("Пропустить").onClick.Invoke();
            map.Leave();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
            boot.Coordinator.SetStage5Runtime(new Stage5Runtime(saves));

            yield return StartCity(5);
            Assert.That(FindPreviewButton("Пропустить"), Is.Null);
            Object.FindFirstObjectByType<StreetMapScreen>().Leave();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);
            var home = Object.FindFirstObjectByType<HomeScreen>();
            home.PrimaryButton.onClick.Invoke();
            FindPreviewButton("Профиль City").onClick.Invoke();
            FindPreviewButton("Повторить обучение").onClick.Invoke();
            yield return StartCity(5);
            Assert.That(FindPreviewButton("Пропустить"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Stage7City_TabAppliesMedkitAndCompletesBothMarketDeals()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pump = new GameObject("Stage7 keyboard").AddComponent<MapTestInputPump>();
            Object.DontDestroyOnLoad(pump.gameObject);
            var update = InputSystem.settings.updateMode;
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            try
            {
                boot.Coordinator.SetStage5Runtime(new Stage5Runtime(CityTestDirectory()));
                yield return StartCity(11);
                var map = Object.FindFirstObjectByType<StreetMapScreen>();
                var body = map.Player.GetComponent<Rigidbody2D>();
                var room = StreetMapLayout.Rooms[1];
                var car = map.GetComponentsInChildren<BoxCollider2D>().Single(item => item.name == "Сгоревшая машина" &&
                    room.Bounds.Contains(Vector2Int.FloorToInt(item.transform.position)));
                var origin = (Vector2)car.transform.position;
                Park(body, origin + Vector2.right * Mathf.Sign(room.Center.x - origin.x) * 1.14f);
                Apply(map.Player.GetComponent<DamageReceiver>(), 60);
                map.TryExit();
                yield return Until(() => FindPreviewButton("Применить · Tab") != null);
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.Tab));
                yield return Until(() => map.Player.ReadSnapshot().Health == 80);
                yield return null;
                Assert.That(map.Player.ReadSnapshot().Health, Is.EqualTo(80));
                pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                map.Leave();
                yield return Until(() => boot.Coordinator.Phase == RunPhase.Home);

                yield return StartVisualPreview("Гипермаркет · 2-1");
                map = Object.FindFirstObjectByType<StreetMapScreen>();
                body = map.Player.GetComponent<Rigidbody2D>();
                var preview = map.GetComponent<Stage5PreviewController>();
                var wall = Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).First(item => item.name.StartsWith("Wall_", StringComparison.Ordinal));
                Assert.That(wall.enabled && wall.GetComponent<SpriteRenderer>().forceRenderingOff, Is.True);
                for (int roomIndex = 0; roomIndex < 2; roomIndex++) yield return ClearPreviewRoom(map, body, roomIndex);
                foreach (var stop in new[]
                         {
                             (Stage5PreviewController.BrokerRoad, Game.Contracts.ProgressionNpcKind.WeaponDealer),
                             (Stage5PreviewController.DealerRoad, Game.Contracts.ProgressionNpcKind.BottleDealer)
                         })
                {
                    Park(body, stop.Item1);
                    pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.Tab));
                    yield return Until(() => preview.IsInside);
                    Vector2 exit = body.position;
                    pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                    yield return null;
                    // Reach the customer side of the counter, outside the former narrow target.
                    Park(body, exit + new Vector2(1.2f, 1.8f));
                    pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.Tab));
                    yield return Until(() => FindPreviewButton("Согласиться · Tab") != null);
                    for (int i = 0; i < 3; i++) yield return null;
                    Assert.That(FindPreviewButton("Согласиться · Tab"), Is.Not.Null, "Held Tab must not also confirm the offer.");
                    pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                    yield return null;
                    pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.Tab));
                    yield return Until(() => FindPreviewButton("Согласиться · Tab") == null);
                    pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                    string reward = stop.Item2 == Game.Contracts.ProgressionNpcKind.WeaponDealer ? "Ускоренная подача" : "Колба Диллера";
                    yield return Until(() => GameObject.Find("Название находки")?.GetComponent<Text>().text == reward);
                    pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.Tab));
                    yield return Until(() => GameObject.Find("Название находки") == null);
                    pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                    yield return null;
                    Park(body, exit);
                    pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState(Key.Tab));
                    yield return Until(() => !preview.IsInside);
                    pump.ApplyState = () => InputState.Change(keyboard, new KeyboardState());
                    yield return null;
                }
            }
            finally
            {
                Object.Destroy(pump.gameObject);
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.updateMode = update;
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        [UnityTest]
        public IEnumerator Stage7City_FireDamageExpiresAndClearsOnDisable()
        {
            var target = new GameObject("Fire test target");
            target.SetActive(false);
            var damage = target.AddComponent<DamageReceiver>();
            damage.Configure(100, 0, .10f);
            target.SetActive(true);
            damage.ApplyDamage(new DamagePacket(-1, 4, DamageType.Fire));
            Assert.That(damage.Health, Is.EqualTo(96));
            yield return Until(() => damage.Health == 94);
            target.SetActive(false);
            target.SetActive(true);
            yield return new WaitForSeconds(.7f);
            Assert.That(damage.Health, Is.EqualTo(94));
            damage.ApplyDamage(new DamagePacket(-1, 1, DamageType.Fire));
            yield return Until(() => damage.Health == 87);
            yield return new WaitForSeconds(.7f);
            Assert.That(damage.Health, Is.EqualTo(87));
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator Stage7City_AllFifteenSegmentsKeepResourcesAndOnlyRoofCompletesTheRegion()
        {
            string directory = CityTestDirectory();
            boot.Coordinator.SetStage5Runtime(new Stage5Runtime(directory));
            var home = Object.FindFirstObjectByType<HomeScreen>();
            home.FixedSeedInput.text = "5";
            home.FullCityButton.onClick.Invoke();
            var coordinator = boot.Coordinator;
            yield return Until(() => coordinator.Phase == RunPhase.InArena, coordinator.Error);
            string runId = coordinator.RunId;
            int lastBalance = 0;
            for (int segment = 0; segment < 15; segment++)
            {
                yield return Until(() => coordinator.Phase == RunPhase.InArena &&
                    coordinator.CitySegmentIndex == segment, coordinator.Error);
                var screen = Object.FindFirstObjectByType<StreetMapScreen>();
                var body = screen.Player.GetComponent<Rigidbody2D>();
                var progression = coordinator.Stage5.Session.Snapshot;
                Assert.That(coordinator.RunId, Is.EqualTo(runId));
                Assert.That(progression.Run.StageId, Is.EqualTo(CityRegionRoute.IdAt(segment)));
                Assert.That(progression.Profile.CompletedRegionIds, Is.Empty);
                Assert.That(progression.Profile.ConfirmedCrystals, Is.EqualTo(lastBalance));
                var reachable = StreetMapCatalog.Reachable(.45f);
                foreach (var room in StreetMapLayout.Rooms)
                    Assert.That(reachable.Contains(Vector2Int.FloorToInt(room.Center)), Is.True, $"{segment}/{room.Id}");
                var props = screen.GetComponentsInChildren<Transform>();
                Assert.That(props.Any(prop => prop.name.StartsWith("Ящик · ", StringComparison.Ordinal)), Is.False);
                var police = props.Where(prop => prop.name == "Тело полицейского с патронами").ToArray();
                Assert.That(police.Length, Is.EqualTo(segment is 5 or 10 ? 1 : 0));
                if (police.Length == 1)
                {
                    var cell = Vector2Int.FloorToInt(police[0].position);
                    Assert.That(StreetMapLayout.Rooms[1].Bounds.Contains(cell) && reachable.Contains(cell), Is.True);
                }
                if (segment == 0) Apply(screen.Player.GetComponent<DamageReceiver>(), 58);
                else Assert.That(screen.Player.ReadSnapshot().Health, Is.EqualTo(72));
                if (segment is 5 or 10)
                {
                    if (segment == 10)
                    {
                        int prior = screen.Player.ReadSnapshot().ReserveAmmo;
                        Park(body, police[0].position);
                        screen.TryExit();
                        Assert.That(screen.Player.ReadSnapshot().ReserveAmmo, Is.EqualTo(prior));
                        Assert.That(GameObject.Find("Патроны · 5,56 мм"), Is.Not.Null);
                    }
                    Park(body, StreetMapLayout.Entry + new Vector2(-3.4f, -1.3f));
                    screen.TryExit();
                    int pack = segment == 5 ? 18 : 6;
                    Assert.That(screen.Player.ReadSnapshot().MagazineCapacity, Is.EqualTo(pack));
                    int reserve = screen.Player.ReadSnapshot().ReserveAmmo;
                    Park(body, police[0].position);
                    screen.TryExit();
                    Assert.That(screen.Player.ReadSnapshot().ReserveAmmo, Is.EqualTo(reserve + pack));
                    screen.TryExit();
                    Assert.That(screen.Player.ReadSnapshot().ReserveAmmo, Is.EqualTo(reserve + pack));
                    Assert.That(police[0].gameObject.activeSelf, Is.True);
                    Assert.That(GameObject.Find("Контур тела полицейского"), Is.Null);
                }
                if (segment > 5 && segment < 10)
                    Assert.That(screen.Player.ReadSnapshot().MagazineCapacity, Is.EqualTo(18));
                if (segment > 10) Assert.That(screen.Player.ReadSnapshot().MagazineCapacity, Is.EqualTo(6));

                foreach (var room in coordinator.FullCityRoute.MapAt(segment).Rooms)
                foreach (var point in room.Points)
                    Assert.That(Physics2D.OverlapCircle(new Vector2(room.CenterX + point.X, room.CenterY + point.Y),
                        point.ClearanceRadius, LayerMask.GetMask("World")), Is.Null, $"{segment}/{point.PointId}");
                for (int room = 0; room < 3; room++)
                    yield return ClearPreviewRoom(screen, body, room);
                Park(body, StreetMapLayout.Rooms[4].Center);
                bool bossRoom = CityRegionRoute.IsBoss(segment);
                yield return Until(() => screen.Rooms[3].State.AliveCount == (bossRoom ? 1 : 3));
                if (bossRoom) Assert.That(Object.FindFirstObjectByType<CityHeavyBoss>(), Is.Not.Null);
                foreach (var enemy in Object.FindObjectsByType<EliteEnemy>(FindObjectsSortMode.None)) Kill(enemy);
                yield return Until(() => screen.Portal.IsOpen);
                lastBalance = coordinator.Stage5.Session.Snapshot.Profile.ConfirmedCrystals;
                Park(body, screen.Portal.transform.position);
                screen.TryExit();
            }
            yield return Until(() => coordinator.Phase == RunPhase.Home, coordinator.Error);
            Assert.That(coordinator.LastOutcome, Is.EqualTo(RunOutcome.Victory));
            coordinator.SetStage5Runtime(new Stage5Runtime(directory));
            Assert.That(coordinator.Stage5.Session.Snapshot.Profile.CompletedRegionIds, Is.EqualTo(new[] { "city" }));
            Assert.That(coordinator.Stage5.Session.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(lastBalance));
        }

        [UnityTest]
        public IEnumerator CityVisualPreview_StreetEndsAfterThreeSegmentsWithoutWritingHomeProfile()
        {
            string profile = CityTestDirectory();
            var run = boot.Coordinator;
            run.SetStage5Runtime(new Stage5Runtime(profile));
            yield return StartVisualPreview("Улица · 1-1 → 1-3");
            for (int segment = 0; segment < 3; ++segment)
            {
                yield return Until(() => run.Phase == RunPhase.InArena && run.CitySegmentIndex == segment, run.Error);
                var map = Object.FindFirstObjectByType<StreetMapScreen>();
                if (segment == 1) Assert.That(GameObject.Find("Зебра"), Is.Not.Null);
                if (segment == 2) Assert.That(GameObject.Find("Газон двора"), Is.Not.Null);
                yield return ClearVisualSegment(map);
                Park(map.Player.GetComponent<Rigidbody2D>(), map.Portal.transform.position);
                map.TryExit();
            }
            yield return Until(() => run.Phase == RunPhase.Home, run.Error);
            Assert.That(run.LastOutcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(run.CitySegmentId, Is.EqualTo("1-3"));
            Assert.That(run.Stage5.Session.Snapshot.Profile.Revision, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CityVisualPreview_MarketCheckoutIsOneUseAndOfficeLiftOpensRoof()
        {
            var run = boot.Coordinator;
            run.SetStage5Runtime(new Stage5Runtime(CityTestDirectory()));
            yield return StartVisualPreview("Гипермаркет · 2-1");
            Assert.That(run.CitySegmentId, Is.EqualTo("2-1"));
            Assert.That(GameObject.Find("Воротник шубы"), Is.Not.Null);
            Assert.That(GameObject.Find("Подсобная · подпись"), Is.Not.Null);
            Assert.That(GameObject.Find("Тело водителя погрузчика"), Is.Not.Null);
            Assert.That(GameObject.Find("Тело полицейского"), Is.Not.Null);
            var map = Object.FindFirstObjectByType<StreetMapScreen>();
            var player = map.Player.GetComponent<Rigidbody2D>();
            var controller = map.GetComponent<Stage5PreviewController>();
            Assert.That(GameObject.Find("Обводка кассы"), Is.Null);
            yield return ClearPreviewRoom(map, player, 0);
            var checkout = GameObject.Find("Обводка кассы");
            Assert.That(checkout, Is.Not.Null);
            Park(player, checkout.transform.position);
            yield return null;
            int before = controller.Coins;
            map.TryExit();
            map.TryExit();
            Assert.That(controller.Coins, Is.EqualTo(before + 2));
            Assert.That(checkout.activeSelf, Is.False);
            map.Leave();
            yield return Until(() => run.Phase == RunPhase.Home);

            yield return StartVisualPreview("Офис и лифт · 3-4 → 3-5");
            Assert.That(run.CitySegmentId, Is.EqualTo("3-4"));
            Assert.That(GameObject.Find("Тренажёрный зал · подпись"), Is.Not.Null);
            Assert.That(GameObject.Find("Столовая · подпись"), Is.Not.Null);
            Assert.That(GameObject.Find("Перегородка"), Is.Not.Null);
            map = Object.FindFirstObjectByType<StreetMapScreen>();
            yield return ClearVisualSegment(map);
            Assert.That(GameObject.Find("Двери лифта"), Is.Not.Null);
            Park(map.Player.GetComponent<Rigidbody2D>(), map.Portal.transform.position);
            map.TryExit();
            yield return Until(() => run.Phase == RunPhase.InArena && run.CitySegmentId == "3-5", run.Error);
            Assert.That(run.Stage5.Session.Snapshot.Profile.Revision, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CityVisualPreview_IncompatibleCorpseAmmoWaitsOnGroundAndPacksOnSecondSearch()
        {
            yield return StartVisualPreview("Гипермаркет · 2-1");
            var map = Object.FindFirstObjectByType<StreetMapScreen>();
            var body = map.Player.GetComponent<Rigidbody2D>();
            Park(body, StreetMapLayout.Entry + new Vector2(-3.4f, -1.3f));
            map.TryExit();
            Assert.That(map.Player.ReadSnapshot().MagazineCapacity, Is.EqualTo(18));
            ShootingEnemy carrier = null;
            for (int room = 0; room < 3 && carrier == null; room++)
            {
                Park(body, StreetMapLayout.Rooms[room + 1].Center);
                var encounter = map.Rooms[room];
                yield return Until(() => encounter.State.AliveCount == encounter.State.Plan.Waves[0].Spawns.Count);
                carrier = Object.FindObjectsByType<ShootingEnemy>(FindObjectsSortMode.None)
                    .FirstOrDefault(enemy => !enemy.IsDead && CityEconomy.RollLoot(0, enemy.name, EnemyTypes.Shooter).Ammo > 0);
                if (carrier == null) yield return ClearPreviewRoom(map, body, room);
            }
            Assert.That(carrier, Is.Not.Null);
            Park(body, carrier.transform.position);
            Kill(carrier);
            var before = map.Player.ReadSnapshot().ReserveAmmo;
            map.TryExit();
            var ground = GameObject.Find("Патроны · 9 мм");
            Assert.That(ground, Is.Not.Null);
            Assert.That(map.Player.ReadSnapshot().ReserveAmmo, Is.EqualTo(before));

            Park(body, ground.transform.position);
            map.TryExit();
            Assert.That(ground.activeSelf, Is.False);
            Assert.That(map.Player.ReadSnapshot().ReserveAmmo, Is.EqualTo(before));
            FindPreviewButton("Рюкзак · B · 0").onClick.Invoke();
            yield return Until(() => GameObject.Find("Патроны в рюкзаке")?.GetComponent<Text>().text.Contains("9 мм: 8") == true);
            Assert.That(FindPreviewButton("Патроны в резерв · E").interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator CityVisualPreview_MeleeGoesAroundARealMarketShelf()
        {
            yield return StartVisualPreview("Гипермаркет · 2-1");
            var map = Object.FindFirstObjectByType<StreetMapScreen>();
            var playerBody = map.Player.GetComponent<Rigidbody2D>();
            var layout = StreetMapLayout.Rooms[1];
            Park(playerBody, layout.Center);
            yield return Until(() => map.Rooms[0].State.AliveCount == map.Rooms[0].State.Plan.Waves[0].Spawns.Count);
            var zombie = Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None).First(enemy => !enemy.IsDead);
            foreach (var actor in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (actor != zombie && actor is IEnemyTarget enemy && !enemy.IsDead) Kill(actor);
            var shelf = map.GetComponentsInChildren<BoxCollider2D>().First(item => item.name == "Стеллаж с продуктами" &&
                layout.Bounds.Contains(Vector2Int.FloorToInt(item.transform.position)));
            var origin = (Vector2)shelf.bounds.center;
            var zombieBody = zombie.GetComponent<Rigidbody2D>();
            Park(zombieBody, origin + Vector2.left * 2.1f);
            Park(playerBody, origin + new Vector2(-2.1f, 2.2f));
            var perception = zombie.GetComponent<EnemyPerception>();
            yield return Until(() => perception.HasTarget);
            Park(zombieBody, origin + Vector2.left * 2.1f);
            Park(playerBody, origin + Vector2.right * 2.1f);
            Assert.That(Physics2D.Linecast(zombieBody.position, playerBody.position, LayerMask.GetMask("World")).collider, Is.SameAs(shelf));
            yield return Until(() => zombieBody.position.x > origin.x + .75f, "Melee stayed on the wrong side of the shelf.");
        }

        [UnityTest]
        public IEnumerator Sewer_CancelKeepsRoofEquipmentAndKnifeCanFightAfterTheEntry()
        {
            var run = boot.Coordinator;
            run.SetStage5Runtime(new Stage5Runtime(CityTestDirectory()));
            yield return StartVisualPreview("Крыша и канализация · 3-5 → 4-5");
            var roof = Object.FindFirstObjectByType<StreetMapScreen>();
            yield return ClearVisualSegment(roof);
            var before = roof.Player.ReadSnapshot();
            OpenSewerHatch(roof);
            FindPreviewButton("Остаться на крыше").onClick.Invoke();
            Assert.That(roof.Player.ReadSnapshot(), Is.EqualTo(before));
            Assert.That(Time.timeScale, Is.EqualTo(1));
            OpenSewerHatch(roof);
            FindPreviewButton("Взять нож").onClick.Invoke();
            yield return Until(() => run.Phase == RunPhase.InArena && run.CitySegmentId == "4-1");
            var map = Object.FindFirstObjectByType<StreetMapScreen>();
            var hero = map.Player;
            var state = hero.ReadSnapshot();
            Assert.That(state.Armor, Is.Zero);
            Assert.That(state.Health, Is.EqualTo(before.Health));
            Assert.That(state.IsMelee && state.Magazine == 0 && state.ReserveAmmo == 0, Is.True);
            Assert.That(GameObject.Find("Свет факела"), Is.Not.Null);
            var weapon = hero.GetComponent<WeaponController>().State;
            weapon.RestoreForEncounter(0, 0, 0, true);
            var commands = SetSewerTestInput(hero);
            commands.Switch = true;
            commands.Attack = true;
            yield return Until(() => weapon.Stamina > 0);
            Assert.That(weapon.IsMelee, Is.True);
            Apply(hero.GetComponent<DamageReceiver>(), 999);
            yield return Until(() => run.Phase == RunPhase.Home);
            Assert.That(run.LastOutcome, Is.EqualTo(RunOutcome.Defeat));
            Assert.That(run.Stage5.Session.Snapshot.Profile.CompletedRegionIds, Is.Empty, "The practice profile is isolated.");
        }

        [UnityTest]
        public IEnumerator Sewer_CityVictorySurvivesDefeatAndRewardsPersistAcrossRealAttempts()
        {
            var run = boot.Coordinator;
            string directory = CityTestDirectory();
            run.SetStage5Runtime(new Stage5Runtime(directory));
            const string blueprint = "blueprint-sealed-mechanism";
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var home = Object.FindFirstObjectByType<HomeScreen>();
                if (home.IsShowingResult) home.PrimaryButton.onClick.Invoke();
                home.FixedSeedInput.text = "5";
                home.FullCityButton.onClick.Invoke();
                for (int segment = 0; segment < 15; segment++)
                {
                    int expected = segment;
                    yield return Until(() => run.Phase == RunPhase.InArena && !run.IsInSewer && run.CitySegmentIndex == expected);
                    var map = Object.FindFirstObjectByType<StreetMapScreen>();
                    yield return ClearSewerTestSegment(map);
                    if (segment == 14)
                    {
                        OpenSewerHatch(map);
                        FindPreviewButton("Взять Пистолет").onClick.Invoke();
                    }
                    else UsePortal(map);
                }
                yield return Until(() => run.Phase == RunPhase.InArena && run.CitySegmentId == "4-1");
                Assert.That(run.Stage5.Session.Snapshot.Profile.CompletedRegionIds, Does.Contain("city"));
                int before = run.Stage5.Session.Snapshot.Profile.ConfirmedCrystals;
                if (attempt == 0)
                {
                    var hero = Object.FindFirstObjectByType<StreetMapScreen>().Player;
                    Apply(hero.GetComponent<DamageReceiver>(), 999);
                    yield return Until(() => run.Phase == RunPhase.Home);
                    Assert.That(run.LastOutcome, Is.EqualTo(RunOutcome.Defeat));
                }
                else
                {
                    for (int segment = 0; segment < SewerStage.SegmentCount; segment++)
                    {
                        string expected = $"4-{segment + 1}";
                        yield return Until(() => run.Phase == RunPhase.InArena && run.CitySegmentId == expected);
                        var map = Object.FindFirstObjectByType<StreetMapScreen>();
                        var reachable = StreetMapCatalog.Reachable(.45f);
                        Assert.That(StreetMapLayout.Rooms.All(room => reachable.Contains(Vector2Int.FloorToInt(room.Center))), Is.True);
                        if (segment == 0 && attempt == 1) yield return VerifyTorchAttack(map);
                        yield return ClearSewerTestSegment(map, verifyBoss: segment == 4 && attempt == 1);
                        UsePortal(map);
                        map.TryExit();
                    }
                    yield return Until(() => run.Phase == RunPhase.Home);
                    Assert.That(run.LastOutcome, Is.EqualTo(RunOutcome.Victory));
                }
                run.SetStage5Runtime(new Stage5Runtime(directory));
                var profile = run.Stage5.Session.Snapshot.Profile;
                Assert.That(profile.CompletedRegionIds, Does.Contain("city"));
                Assert.That(profile.UnlockedIds.Count(id => id == blueprint), Is.EqualTo(attempt == 0 ? 0 : 1));
                Assert.That(profile.ConfirmedCrystals, Is.EqualTo(before + (attempt == 2 ? 150 : 0)));
            }
        }

        private static IEnumerator VerifyTorchAttack(StreetMapScreen map)
        {
            var hero = map.Player;
            var body = hero.GetComponent<Rigidbody2D>();
            var weapon = hero.GetComponent<WeaponController>().State;
            weapon.RestoreForEncounter(0, 0, 0, false);
            var commands = SetSewerTestInput(hero);
            commands.Switch = true;
            Park(body, StreetMapLayout.Rooms[1].Center);
            yield return Until(() => map.Rooms[0].State.AliveCount > 0);
            var enemy = Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None).First(actor => !actor.IsDead);
            var target = enemy.GetComponent<DamageReceiver>();
            float before = target.Health;
            Park(body, StreetMapLayout.Rooms[1].Center);
            Park(enemy.GetComponent<Rigidbody2D>(), body.position + Vector2.right * .95f);
            commands.Attack = true;
            yield return Until(() => target.Health < before, "An empty gun must allow a torch hit.");
            Assert.That(weapon.IsMelee, Is.False);
            Assert.That(weapon.Magazine + weapon.ReserveAmmo, Is.Zero);
            commands.Attack = commands.Switch = false;
        }

        private static MapTestCommands SetSewerTestInput(PlayerCombatFacade hero)
        {
            var motor = hero.GetComponent<ActorMotor2D>();
            motor.enabled = false;
            var commands = hero.gameObject.AddComponent<MapTestCommands>();
            motor.Configure(commands);
            motor.enabled = true;
            return commands;
        }

        private static IEnumerator ClearSewerTestSegment(StreetMapScreen map, bool verifyBoss = false)
        {
            var body = map.Player.GetComponent<Rigidbody2D>();
            for (int index = 0; index < map.Rooms.Length; index++)
            {
                var room = map.Rooms[index];
                Park(body, StreetMapLayout.Rooms[index + 1].Center);
                yield return Until(() => room.State.AliveCount == room.State.Plan.Waves[0].Spawns.Count);
                if (verifyBoss && index == 3)
                {
                    var boss = Object.FindFirstObjectByType<CityHeavyBoss>();
                    Assert.That(boss, Is.Not.Null);
                    var damage = boss.GetComponent<DamageReceiver>();
                    Assert.That(damage.Health, Is.EqualTo(300));
                    yield return Until(() => boss.GetComponentsInChildren<SpriteRenderer>().Count(renderer =>
                        renderer.enabled && renderer.name == "City boss warning") == 3);
                    Apply(damage, 160);
                    yield return Until(() => boss.GetComponentsInChildren<SpriteRenderer>().Count(renderer =>
                        renderer.enabled && renderer.name == "City boss warning") == 5);
                }
                foreach (var actor in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (actor is IEnemyTarget enemy && !enemy.IsDead) Kill(actor);
            }
            yield return Until(() => map.Portal.IsOpen);
        }

        private static void UsePortal(StreetMapScreen map)
        {
            Park(map.Player.GetComponent<Rigidbody2D>(), map.Portal.transform.position);
            map.TryExit();
        }

        private static void OpenSewerHatch(StreetMapScreen map)
        {
            var hatch = GameObject.Find("Люк канализации");
            Assert.That(hatch, Is.Not.Null);
            Assert.That(Vector2.Distance(hatch.transform.position, map.Portal.transform.position), Is.GreaterThanOrEqualTo(3.19f));
            Park(map.Player.GetComponent<Rigidbody2D>(), hatch.transform.position);
            map.TryExit();
        }

        private IEnumerator StartVisualPreview(string label)
        {
            var home = Object.FindFirstObjectByType<HomeScreen>();
            if (home.IsShowingResult) home.PrimaryButton.onClick.Invoke();
            home.FixedSeedInput.text = "0";
            FindPreviewButton("Карта города").onClick.Invoke();
            FindPreviewButton(label).onClick.Invoke();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena, boot.Coordinator.Error);
        }

        private static IEnumerator ClearVisualSegment(StreetMapScreen map)
        {
            var body = map.Player.GetComponent<Rigidbody2D>();
            for (int room = 0; room < 3; ++room) yield return ClearPreviewRoom(map, body, room);
            var final = map.Rooms[3];
            Park(body, StreetMapLayout.Rooms[4].Center);
            yield return Until(() => final.State.AliveCount == final.State.Plan.Waves[0].Spawns.Count);
            foreach (var enemy in Object.FindObjectsByType<EliteEnemy>(FindObjectsSortMode.None)) Kill(enemy);
            yield return Until(() => map.Portal.IsOpen);
        }

        private IEnumerator StartCity(int seed)
        {
            var home = Object.FindFirstObjectByType<HomeScreen>();
            if (home.IsShowingResult) home.PrimaryButton.onClick.Invoke();
            home.FixedSeedInput.text = seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            home.Stage5CityButton.onClick.Invoke();
            yield return Until(() => boot.Coordinator.Phase == RunPhase.InArena, boot.Coordinator.Error);
        }

        private static string CityTestDirectory() => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "TestResults", "Saves", "stage5-integration-" + Guid.NewGuid().ToString("N"));

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
            float chaseStarted = Time.fixedTime;
            yield return new WaitForSeconds(1f);
            Assert.That(nurse.IsSlime, Is.True);
            Assert.That(body.position.x, Is.GreaterThan(1.5f));
            yield return Until(() => !nurse.IsSlime);
            Assert.That(Time.fixedTime - chaseStarted, Is.EqualTo(2.7f).Within(.10f));
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
