using System;
using System.IO;
using Game.Contracts;
using Game.Progression;
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
        private string purchaseId;
        public CityProgressionSession Session => session ??= Open(directory);

        public Stage5Runtime(string directory)
        {
            this.directory = Path.GetFullPath(directory);
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

        internal ProgressionResult Start(int seed, out string id)
        {
            if (startId == null || startSeed != seed)
            {
                startId = Guid.NewGuid().ToString("N");
                startSeed = seed;
            }
            id = startId;
            var result = Session.StartRun(new ProgressionCommandKey(id, "start"), seed, Points);
            if (result.Status != ProgressionStatus.RetryableFailure) startId = null;
            return result;
        }

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
            startId = purchaseId = null;
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
