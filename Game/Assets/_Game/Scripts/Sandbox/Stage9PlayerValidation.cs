#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Progression;
using Game.StreetMap;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Sandbox
{
    public sealed class Stage9PlayerValidation : MonoBehaviour
    {
        private const int Seed = 41;
        private const int Baseline = 30;
        private const string Starter = "pistol-city";
        private const string City = "city";
        private const string Ranch = "ranch";
        private string mode;
        private string directory;
        private float deadline;
        private bool finished;
        private RunCoordinator run;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Launch()
        {
            if (Application.isEditor) return;
            string[] arguments = Environment.GetCommandLineArgs();
            int smoke = Array.IndexOf(arguments, "--stage9-smoke");
            if (smoke < 0) return;
            int profile = Array.IndexOf(arguments, "-stage5-profile");
            if (profile < 0 || profile + 1 >= arguments.Length || smoke + 1 >= arguments.Length ||
                arguments[profile + 1].StartsWith("-", StringComparison.Ordinal) ||
                (arguments[smoke + 1] is not ("death" or "victory" or "verify")))
            {
                Debug.LogError("S9_PLAYER_FAILED: use --stage9-smoke death|victory|verify with -stage5-profile.");
                Application.Quit(2);
                return;
            }
            var validator = new GameObject("Stage 9 Player validation").AddComponent<Stage9PlayerValidation>();
            DontDestroyOnLoad(validator.gameObject);
            validator.mode = arguments[smoke + 1];
            validator.directory = Path.GetFullPath(arguments[profile + 1]);
        }

        private IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 45f;
            var repository = new JsonSaveRepository(directory);
            if (mode == "death")
            {
                if (!Check(repository.Load().Status == SaveLoadStatus.Missing, "Death phase expects a fresh profile.")) yield break;
                var baseline = new ProfileData
                {
                    revision = 1,
                    confirmedCrystals = Baseline,
                    selectedWeaponId = Starter,
                    completedRegionIds = new[] { City },
                    unlockedIds = new[] { Starter, SewerEntryService.BlueprintId }
                };
                if (!Check(repository.TryCommit(0, baseline) == SaveWriteStatus.Saved, "Could not create test profile.")) yield break;
            }

            yield return new WaitUntil(() => FindFirstObjectByType<GameBootstrap>()?.Coordinator?.Phase == RunPhase.Home);
            run = FindFirstObjectByType<GameBootstrap>().Coordinator;
            var initial = repository.Load().Profile;
            if (!Check(initial != null && !initial.activeRun.Exists && initial.selectedWeaponId == Starter &&
                initial.unlockedIds.Contains(SewerEntryService.BlueprintId), "Unexpected saved profile on Home.")) yield break;
            Debug.Log($"S9_HOME_OPEN mode={mode} seed={Seed} revision={initial.revision} balance={initial.confirmedCrystals} " +
                $"regions={string.Join(",", initial.completedRegionIds)} previousRuns={initial.finishedRuns.Length} starter={initial.selectedWeaponId}");

            if (mode == "death")
            {
                yield return ExerciseDeaths();
                if (finished) yield break;
            }
            else
            {
                int victories = mode == "verify" ? 1 : 0;
                if (!Check(ValidateHistory(initial, victories), "Wrong profile history after process restart.")) yield break;
                if (mode == "victory")
                {
                    yield return ExerciseVictory();
                    if (finished) yield break;
                }
            }

            int expectedVictories = mode == "death" ? 0 : 1;
            var saved = repository.Load().Profile;
            if (!Check(saved != null && ValidateHistory(saved, expectedVictories), "Wrong final profile.")) yield break;
            if (!Check(run.Stage5.Session.Snapshot.Profile.ConfirmedCrystals == saved.confirmedCrystals,
                "Home balance differs from disk.")) yield break;
            bool reopenedWithoutWrite = mode != "verify" || initial.revision == saved.revision;
            if (!Check(reopenedWithoutWrite, "Read-only restart advanced the revision.")) yield break;
            Debug.Log($"S9_PLAYER_OK mode={mode} seed={Seed} revision={saved.revision} balance={saved.confirmedCrystals} " +
                $"regions={string.Join(",", saved.completedRegionIds)} runs={saved.finishedRuns.Length} build={Application.buildGUID}");
            finished = true;
            Application.Quit(0);
        }

        private IEnumerator ExerciseDeaths()
        {
            yield return OpenRanch();
            if (finished) yield break;
            var map = FindFirstObjectByType<StreetMapScreen>();
            yield return Enter(map, 0);
            if (finished) yield break;
            var first = Actors(map.Rooms[0]).First();
            Vector2 drop = first.transform.position;
            Kill(first);
            Move(map, drop);
            yield return new WaitUntil(() => run.Stage5.Session.Snapshot.Run?.PendingCrystals > 0);
            Kill(map.Player);
            yield return new WaitUntil(() => run.Phase == RunPhase.Home);
            if (!Check(run.LastOutcome == RunOutcome.Defeat &&
                run.Stage5.Session.Snapshot.Profile.ConfirmedCrystals == Baseline, "Death before clear changed the balance.")) yield break;

            yield return OpenRanch();
            if (finished) yield break;
            map = FindFirstObjectByType<StreetMapScreen>();
            yield return Enter(map, 0);
            if (finished) yield break;
            KillRoom(map.Rooms[0]);
            if (!Check(map.ClearedCount == 1 && run.Stage5.Session.Snapshot.Profile.ConfirmedCrystals > Baseline,
                "Yard checkpoint did not settle.")) yield break;
            int balance = run.Stage5.Session.Snapshot.Profile.ConfirmedCrystals;
            Kill(map.Player);
            yield return new WaitUntil(() => run.Phase == RunPhase.Home);
            Check(run.LastOutcome == RunOutcome.Defeat && run.Stage5.Session.Snapshot.Profile.ConfirmedCrystals == balance,
                "Death after clear lost the confirmed balance.");
        }

        private IEnumerator ExerciseVictory()
        {
            yield return OpenRanch();
            if (finished) yield break;
            var map = FindFirstObjectByType<StreetMapScreen>();
            for (int index = 0; index < map.Rooms.Length; index++)
            {
                yield return Enter(map, index);
                if (finished) yield break;
                KillRoom(map.Rooms[index]);
                if (!Check(map.ClearedCount == index + 1, "A Ranch room did not settle.")) yield break;
            }
            if (!Check(map.Portal.IsOpen, "Boss victory did not open the exit.")) yield break;
            Move(map, map.Portal.transform.position);
            map.TryExit();
            map.TryExit();
            yield return new WaitUntil(() => run.Phase == RunPhase.Home);
            Check(run.LastOutcome == RunOutcome.Victory, "Victory did not reach Home.");
        }

        private IEnumerator OpenRanch()
        {
            var home = FindFirstObjectByType<HomeScreen>();
            if (home.IsShowingResult) home.PrimaryButton.onClick.Invoke();
            home.FixedSeedInput.text = Seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var cityMap = FindButton("Карта города");
            if (!Check(cityMap != null, "Missing city map button.")) yield break;
            cityMap.onClick.Invoke();
            var ranch = FindButton("Ранчо · 5-1");
            if (!Check(ranch != null && ranch.interactable, "Ranch is locked in a completed City profile.")) yield break;
            ranch.onClick.Invoke();
            yield return new WaitUntil(() => run.Phase == RunPhase.InArena);
            var map = FindFirstObjectByType<StreetMapScreen>();
            if (map.IsPaused) map.Resume();
            Check(run.CurrentRequest.IsRanch && !run.CurrentRequest.IsRanchPreview &&
                map.Player.ReadSnapshot().MagazineCapacity == 10, "Ranch did not equip the saved starter.");
        }

        private IEnumerator Enter(StreetMapScreen map, int index)
        {
            float x = RanchLayout.Rooms[index + 1].Bounds.xMin + 4;
            Move(map, new Vector2(x, 0));
            var room = map.Rooms[index];
            int expected = room.State.Plan.Waves.Single().Spawns.Count;
            yield return new WaitUntil(() => room.State.AliveCount == expected);
            Check(room.Doors.All(door => door.IsClosed), "Expected closed encounter doors.");
        }

        private static bool ValidateHistory(ProfileData profile, int victories) =>
            !profile.activeRun.Exists && profile.selectedWeaponId == Starter &&
            profile.unlockedIds.Contains(SewerEntryService.BlueprintId) &&
            profile.completedRegionIds.SequenceEqual(victories == 1 ? new[] { City, Ranch } : new[] { City }) &&
            profile.finishedRuns.Select(attempt => attempt.outcome).SequenceEqual(victories == 1
                ? new[] { RunOutcome.Defeat, RunOutcome.Defeat, RunOutcome.Victory }
                : new[] { RunOutcome.Defeat, RunOutcome.Defeat }) &&
            profile.checkpoints.Length == 1 + victories * 3 &&
            profile.checkpoints.All(receipt => receipt.stageId == RanchLayout.SegmentId) &&
            profile.confirmedCrystals == Baseline + profile.checkpoints.Sum(receipt => receipt.crystals);

        private static Button FindButton(string text) => FindObjectsByType<Button>(FindObjectsSortMode.None)
            .SingleOrDefault(button => button.GetComponentInChildren<Text>()?.text == text);

        private static void Move(StreetMapScreen map, Vector2 point)
        {
            var body = map.Player.GetComponent<Rigidbody2D>();
            body.transform.position = point;
            body.position = point;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private static MonoBehaviour[] Actors(RoomController room) => room.GetComponentsInChildren<MonoBehaviour>()
            .Where(actor => actor is IEnemyTarget target && !target.IsDead).ToArray();

        private static void KillRoom(RoomController room)
        {
            foreach (var actor in Actors(room)) Kill(actor);
        }

        private static void Kill(Component actor) =>
            actor.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1, 1000));

        private bool Check(bool condition, string message)
        {
            if (condition) return true;
            if (finished) return false;
            Debug.LogError($"S9_PLAYER_FAILED mode={mode}: {message}");
            finished = true;
            StopAllCoroutines();
            Application.Quit(2);
            return false;
        }

        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup > deadline)
                Check(false, $"Timed out waiting for phase {run?.Phase}.");
        }
    }
}
#endif
