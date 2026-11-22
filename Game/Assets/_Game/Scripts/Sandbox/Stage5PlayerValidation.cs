#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
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
            home.Stage5CityButton.onClick.Invoke();
            yield return new WaitUntil(() => run.Phase == RunPhase.InArena);
            var map = FindFirstObjectByType<StreetMapScreen>();
            if (!Check(map.Player.ReadSnapshot().MagazineCapacity == (writeProfile ? 8 : 10), "Wrong starter equipped.")) yield break;

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
            }

            profile = run.Stage5.Session.Snapshot.Profile;
            var profileLabel = FindObjectsByType<Text>(FindObjectsSortMode.None)
                .Any(label => label.text.Contains(writeProfile ? $"Кристаллы скверны: {balance}" : $"баланс в убежище: {balance}"));
            if (!Check(profile.ConfirmedCrystals == balance && profile.SelectedWeaponId == "pistol-city" && profileLabel,
                "Home does not reflect the saved starter and balance.")) yield break;
            Debug.Log($"S5_PLAYER_OK mode={(writeProfile ? "write" : "read")} seed={Seed} balance={balance} starter={profile.SelectedWeaponId} build={Application.buildGUID}");
            enabled = false;
            Application.Quit(0);
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
