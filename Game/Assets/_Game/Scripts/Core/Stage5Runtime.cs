using System;
using System.IO;
using Game.Contracts;
using Game.Progression;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    public sealed class Stage5Runtime
    {
        internal const string StageId = "1-1";
        internal const string BrokerPoint = "1-1/a-to-b";
        internal const string DealerPoint = "1-1/b-to-c";
        private static readonly ProgressionTransitionPoint[] Points =
        {
            new(BrokerPoint, StageId, "a-to-b", "broker-alley", ProgressionNpcKind.WeaponDealer),
            new(DealerPoint, StageId, "b-to-c", "dealer-door", ProgressionNpcKind.BottleDealer)
        };

        private readonly string directory;
        private CityProgressionSession session;
        private string startId;
        private int startSeed;
        private bool startFullCity;
        private string purchaseId;
        private string selectionId;
        private string selectionWeapon;
        private bool separateProfile;
        private bool replayTutorial;
        public CityProgressionSession Session => session ??= Open(directory);

        public Stage5Runtime(string directory)
        {
            this.directory = Path.GetFullPath(directory);
        }

        private Stage5Runtime(CityProgressionSession session) => this.session = session;
        internal static Stage5Runtime ForVisualPreview() => new Stage5Runtime(
            new CityProgressionSession(() => Time.timeAsDouble, new PreviewSave()));

        private sealed class PreviewSave : ISaveRepository
        {
            private ProfileData profile;
            public SaveLoadResult Load() => profile == null ? new SaveLoadResult(SaveLoadStatus.Missing) :
                new SaveLoadResult(SaveLoadStatus.Ready, profile.Copy());

            public SaveWriteStatus TryCommit(long expectedRevision, ProfileData candidate)
            {
                if ((profile?.revision ?? 0) != expectedRevision) return SaveWriteStatus.RevisionConflict;
                if (candidate.revision != expectedRevision + 1 || !candidate.IsValid()) return SaveWriteStatus.InvalidProfile;
                profile = candidate.Copy();
                return SaveWriteStatus.Saved;
            }
        }

        internal static Stage5Runtime ForApplication()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-stage5-profile");
            if (option >= 0 && option + 1 < args.Length) return new Stage5Runtime(args[option + 1]);
            string root = Application.isEditor ? Path.Combine(Application.dataPath, "..", "LocalState") :
                Application.persistentDataPath;
            return new Stage5Runtime(Path.Combine(root, "Stage5"));
        }

        private static CityProgressionSession Open(string path) => new(() => Time.timeAsDouble, new JsonSaveRepository(path));

        internal ProgressionResult Start(int seed, bool fullCity, out string id, string previewStage = null)
        {
            if (startId == null || startSeed != seed || startFullCity != fullCity)
            {
                startId = Guid.NewGuid().ToString("N");
                startSeed = seed;
                startFullCity = fullCity;
            }
            id = startId;
            var segments = previewStage == null ? CityEconomy.FullRoute : new[] { previewStage };
            var points = fullCity ? segments.SelectMany(segment => Points.Select(point =>
                new ProgressionTransitionPoint(segment + "/" + point.RoadId, segment, point.RoadId, point.EntranceId, point.AllowedNpcs))).ToArray() : Points;
            var result = Session.StartRun(new ProgressionCommandKey(id, "start"), seed, points, fullCity);
            if (result.Status != ProgressionStatus.RetryableFailure) startId = null;
            return result;
        }

        internal ProgressionResult Enter(string id, string segment) =>
            Session.EnterStage(new ProgressionCommandKey(id, "enter/" + segment), segment);

        internal ProgressionResult Finish(string id, RunOutcome outcome) =>
            Session.FinishRun(new ProgressionCommandKey(id, "finish"), outcome);

        internal ProgressionResult Buy(string offerId)
        {
            purchaseId ??= Guid.NewGuid().ToString("N");
            var result = Session.BuyHomeUnlock(new ProgressionCommandKey(null, purchaseId), offerId);
            if (result.Status != ProgressionStatus.RetryableFailure) purchaseId = null;
            return result;
        }

        internal void OpenSeparateProfile()
        {
            session = Open(directory + "-separate");
            separateProfile = true;
            replayTutorial = false;
            startId = purchaseId = selectionId = null;
        }

        internal ProgressionResult SelectStarter(string weaponId)
        {
            if (selectionId == null || selectionWeapon != weaponId)
            {
                selectionId = Guid.NewGuid().ToString("N");
                selectionWeapon = weaponId;
            }
            var result = Session.SelectStarter(new ProgressionCommandKey(null, selectionId), weaponId);
            if (result.Status != ProgressionStatus.RetryableFailure) selectionId = null;
            return result;
        }

        private string TutorialMarker => Path.Combine(separateProfile ? directory + "-separate" : directory, "tutorial-seen");
        internal bool HasSeenTutorial => directory == null || !replayTutorial && File.Exists(TutorialMarker);
        internal void ReplayTutorial() => replayTutorial = true;

        internal void MarkTutorialSeen()
        {
            replayTutorial = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TutorialMarker));
                using var marker = File.Open(TutorialMarker, FileMode.OpenOrCreate);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static string Describe(ProgressionFailure failure) => failure switch
        {
            ProgressionFailure.SaveFailed => "Не удалось записать профиль. Повторите сохранение.",
            ProgressionFailure.ProfileIncompatible => "Профиль создан более новой версией. Откройте отдельный профиль.",
            ProgressionFailure.InsufficientCoins => "Не хватает монет",
            ProgressionFailure.InsufficientCrystals => "Не хватает кристаллов",
            ProgressionFailure.ResourceFull => "Ресурс уже полон",
            ProgressionFailure.IncompatibleWeapon => "Возьмите оружие, для которого заказано усиление",
            _ => $"Действие недоступно ({failure})"
        };
    }
}
