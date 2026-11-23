#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Actors;
using Game.Combat;
using Game.Core;
using Game.StreetMap;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Sandbox
{
    public sealed class Stage5PlayerValidation : MonoBehaviour
    {
        private const int Seed = 5;
        private bool writeProfile;
        private bool stage6;
        private bool stage7;
        private string captureFolder;
        private float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Launch()
        {
            if (Application.isEditor) return;
            string[] args = Environment.GetCommandLineArgs();
            bool write = Array.IndexOf(args, "--stage5-smoke-write") >= 0;
            bool read = Array.IndexOf(args, "--stage5-smoke-read") >= 0;
            if (!write && !read) return;
            int profileOption = Array.IndexOf(args, "-stage5-profile");
            if (write == read || profileOption < 0 || profileOption + 1 == args.Length || args[profileOption + 1].StartsWith("-"))
            {
                Fail("Provide one smoke mode and a separate profile path.");
                return;
            }

            var validation = new GameObject("Stage 5 Player validation").AddComponent<Stage5PlayerValidation>();
            DontDestroyOnLoad(validation.gameObject);
            validation.writeProfile = write;
            validation.stage7 = Array.IndexOf(args, "--stage7-smoke") >= 0;
            validation.stage6 = validation.stage7 || Array.IndexOf(args, "--stage6-smoke") >= 0;
            if (validation.stage6 && !Application.isBatchMode && Array.IndexOf(args, "--stage6-capture") >= 0)
                validation.captureFolder = args[profileOption + 1];
        }

        private IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 45f;
            yield return new WaitUntil(() => FindFirstObjectByType<GameBootstrap>()?.Coordinator?.Phase == RunPhase.Home);
            var run = FindFirstObjectByType<GameBootstrap>().Coordinator;
            var profile = run.Stage5.Session.Snapshot.Profile;
            int balance = profile.ConfirmedCrystals;
            if (!Check(writeProfile ? balance == 0 && profile.SelectedWeaponId == "pistol"
                : balance > 0 && profile.SelectedWeaponId == "pistol-city", "Unexpected profile on startup.")) yield break;

            var home = FindFirstObjectByType<HomeScreen>();
            home.FixedSeedInput.text = Seed.ToString(CultureInfo.InvariantCulture);
            if (stage6)
            {
                var cityMap = FindButton("Карта города");
                if (!Check(cityMap != null && !home.Stage5PreviewButton.gameObject.activeSelf, "Expected the normal City menu.")) yield break;
                cityMap.onClick.Invoke();
                if (!Check(FindButton("Начать маршрут CITY") != null, "The street is missing from the map.")) yield break;
                yield return Capture("Map");
                FindButton("Закрыть").onClick.Invoke();
                home.PrimaryButton.onClick.Invoke();
            }
            else home.Stage5CityButton.onClick.Invoke();
            yield return new WaitUntil(() => run.Phase == RunPhase.InArena);
            var map = FindFirstObjectByType<StreetMapScreen>();
            if (!Check(map.Player.ReadSnapshot().MagazineCapacity == (writeProfile ? 8 : 10), "Wrong starter equipped.")) yield break;
            if (stage6)
            {
                if (!Check(GameObject.Find("City facades and street surface") != null, "Expected the City art layer.")) yield break;
                var skip = FindButton("Пропустить");
                if (!Check(writeProfile == (skip != null), "Tutorial visibility did not match the profile.")) yield break;
                yield return Capture("Street");
                if (skip != null) skip.onClick.Invoke();
            }
            if (stage7)
            {
                yield return ExerciseFullCity(run);
                yield break;
            }

            if (writeProfile)
            {
                var body = map.Player.GetComponent<Rigidbody2D>();
                body.position = StreetMapLayout.Rooms[1].Center;
                body.transform.position = body.position;
                Physics2D.SyncTransforms();
                var room = map.Rooms[0];
                int expectedEnemies = room.State.Plan.Waves[0].Spawns.Count;
                yield return new WaitUntil(() => room.State.AliveCount == expectedEnemies);
                foreach (var enemy in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (enemy is IEnemyTarget target && !target.IsDead)
                        enemy.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1, 999));
                }
                yield return new WaitUntil(() => run.Stage5.Session.Snapshot.Profile.ConfirmedCrystals > 20);
                balance = run.Stage5.Session.Snapshot.Profile.ConfirmedCrystals - 20;
            }

            map.Leave();
            yield return new WaitUntil(() => run.Phase == RunPhase.Home);
            if (!Check(run.LastOutcome == RunOutcome.Abandoned && run.Stage5.Session.Snapshot.Run == null, "Run did not close.")) yield break;
            if (writeProfile)
            {
                home = FindFirstObjectByType<HomeScreen>();
                home.PrimaryButton.onClick.Invoke();
                var openProfile = FindButton("Профиль City");
                if (!Check(openProfile != null, "Profile button is missing.")) yield break;
                openProfile.onClick.Invoke();
                var buy = FindButton("Открыть городской пистолет · 20");
                if (!Check(buy != null && buy.interactable, "Starter is unavailable for purchase.")) yield break;
                buy.onClick.Invoke();
                if (stage6)
                {
                    var basic = FindButton("Выбрать обычный пистолет");
                    if (!Check(basic != null, "Starter selection button is missing.")) yield break;
                    basic.onClick.Invoke();
                    if (!Check(run.Stage5.Session.Snapshot.Profile.SelectedWeaponId == "pistol", "Basic starter was not saved.")) yield break;
                    FindButton("Выбрать городской пистолет").onClick.Invoke();
                    yield return Capture("Profile");
                }
            }

            profile = run.Stage5.Session.Snapshot.Profile;
            var profileLabel = FindObjectsByType<Text>(FindObjectsSortMode.None)
                .Any(label => label.text.Contains(writeProfile ? $"Кристаллы скверны: {balance}" : $"баланс в убежище: {balance}"));
            if (!Check(profile.ConfirmedCrystals == balance && profile.SelectedWeaponId == "pistol-city" && profileLabel,
                "Home does not reflect the saved starter and balance.")) yield break;
            Debug.Log($"S5_PLAYER_OK mode={(writeProfile ? "write" : "read")} seed={Seed} balance={balance} starter={profile.SelectedWeaponId} build={Application.buildGUID}");
            if (stage6) Debug.Log($"S6_PLAYER_OK mode={(writeProfile ? "write" : "read")} seed={Seed} build={Application.buildGUID}");
            enabled = false;
            Application.Quit(0);
        }

        private IEnumerator ExerciseFullCity(RunCoordinator run)
        {
            if (!Check(writeProfile && run.CurrentRequest.IsFullCity, "Stage 7 requires a fresh profile and the City route.")) yield break;
            deadline = Time.realtimeSinceStartup + 90f;
            string id = run.RunId;
            for (int index = 0; index < CityRegionRoute.SegmentCount; index++)
            {
                yield return new WaitUntil(() => run.Phase == RunPhase.InArena && run.CitySegmentIndex == index);
                var arena = FindFirstObjectByType<StreetMapScreen>();
                var body = arena.Player.GetComponent<Rigidbody2D>();
                if (!Check(run.RunId == id && run.Stage5.Session.Snapshot.Run.StageId == CityRegionRoute.IdAt(index),
                    "Unexpected City checkpoint.")) yield break;

                if (index is 5 or 10)
                {
                    Move(body, StreetMapLayout.Entry + new Vector2(-3.4f, -1.3f));
                    arena.TryExit();
                    if (!Check(arena.Player.ReadSnapshot().MagazineCapacity == (index == 5 ? 18 : 6),
                        "Expected the district's weapon on the rack.")) yield break;
                }
                for (int roomIndex = 0; roomIndex < arena.Rooms.Length; roomIndex++)
                {
                    var room = arena.Rooms[roomIndex];
                    Move(body, StreetMapLayout.Rooms[roomIndex + 1].Center);
                    int expected = room.State.Plan.Waves[0].Spawns.Count;
                    yield return new WaitUntil(() => room.State.AliveCount == expected);
                    if (index == 5 && roomIndex == 0) yield return Capture("Market");
                    if (index == 10 && roomIndex == 0) yield return Capture("Building");
                    if (index == 14 && roomIndex == 3) yield return Capture("Roof");
                    foreach (var enemy in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                        if (enemy is IEnemyTarget target && !target.IsDead)
                            enemy.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1, 999));
                }
                yield return new WaitUntil(() => arena.Portal.IsOpen);
                Move(body, arena.Portal.transform.position);
                arena.TryExit();
                Debug.Log($"S7_SEGMENT seed={Seed} segment={CityRegionRoute.IdAt(index)}");
            }
            yield return new WaitUntil(() => run.Phase == RunPhase.Home);
            var profile = run.Stage5.Session.Snapshot.Profile;
            if (!Check(run.LastOutcome == RunOutcome.Victory && profile.CompletedRegionIds.Contains("city"),
                "Roof victory did not save the completion.")) yield break;
            Debug.Log($"S7_PLAYER_OK seed={Seed} segments=15 balance={profile.ConfirmedCrystals} build={Application.buildGUID}");
            enabled = false;
            Application.Quit(0);
        }

        private static void Move(Rigidbody2D body, Vector2 position)
        {
            body.position = position;
            body.transform.position = position;
            Physics2D.SyncTransforms();
        }

        private IEnumerator Capture(string name)
        {
            if (captureFolder == null) yield break;
            yield return new WaitForEndOfFrame();
            var camera = Camera.main;
            if (camera == null) yield break;
            var overlays = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var frame = RenderTexture.GetTemporary(1280, 720, 24);
            var priorTarget = camera.targetTexture;
            var priorActive = RenderTexture.active;
            try
            {
                camera.targetTexture = frame;
                foreach (var overlay in overlays)
                {
                    overlay.renderMode = RenderMode.ScreenSpaceCamera;
                    overlay.worldCamera = camera;
                    overlay.planeDistance = 1;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = frame;
                var screenshot = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
                byte[] png = screenshot.EncodeToPNG();
                Destroy(screenshot);
                File.WriteAllBytes(Path.Combine(captureFolder, name + ".png"), png);
            }
            finally
            {
                foreach (var overlay in overlays)
                {
                    overlay.renderMode = RenderMode.ScreenSpaceOverlay;
                    overlay.worldCamera = null;
                }
                camera.targetTexture = priorTarget;
                RenderTexture.active = priorActive;
                RenderTexture.ReleaseTemporary(frame);
            }
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup <= deadline) return;
            enabled = false;
            Fail("Timed out.");
        }

        private static Button FindButton(string label) => FindObjectsByType<Button>(FindObjectsSortMode.None)
            .FirstOrDefault(button => button.GetComponentInChildren<Text>()?.text == label);

        private static bool Check(bool ok, string reason)
        {
            if (!ok) Fail(reason);
            return ok;
        }

        private static void Fail(string reason)
        {
            Debug.LogError("S5_PLAYER_FAILED " + reason);
            Application.Quit(2);
        }
    }
}
#endif
