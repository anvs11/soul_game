using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Core;
using Game.Encounters;

namespace Game.StreetMap
{
    internal enum PreviewNpc { Broker, Dealer }
    internal enum PreviewLoot { Coins, Crystals }
    internal enum PreviewProfileScenario { Normal, WriteFailure, Backup, FutureSchema }

    internal sealed class Stage5PreviewProfile
    {
        private const int UnlockPrice = 100;
        private static readonly ConditionalWeakTable<RunCoordinator, Stage5PreviewProfile> Profiles =
            new ConditionalWeakTable<RunCoordinator, Stage5PreviewProfile>();
        private Stage5PreviewProfile separate;

        public static Stage5PreviewProfile For(RunCoordinator run)
        {
            var profile = Profiles.GetValue(run, _ => new Stage5PreviewProfile());
            while (profile.separate != null) profile = profile.separate;
            return profile;
        }
        public int Crystals { get; private set; } = 120;
        public bool Unlocked { get; private set; }
        public bool Selected { get; private set; }
        public int LastCoins { get; private set; }
        public int LastCrystals { get; private set; }
        public PreviewProfileScenario Scenario { get; private set; }

        public string Summary => $"Кристаллы скверны: {Crystals} · профиль макета";
        public string PurchaseLabel => Scenario == PreviewProfileScenario.FutureSchema ? "Открыть отдельный профиль" :
            Unlocked ? "Выбрать тестовый вариант" : $"Открыть тестовый вариант · {UnlockPrice}";
        public string ScenarioLabel => Scenario switch
        {
            PreviewProfileScenario.WriteFailure => "Сценарий: следующая запись завершится ошибкой",
            PreviewProfileScenario.Backup => "Профиль загружен из резервной копии",
            PreviewProfileScenario.FutureSchema => "Профиль создан более новой версией игры",
            _ => "Сценарий: обычный профиль"
        };

        public void AdvanceScenario() => Scenario = (PreviewProfileScenario)(((int)Scenario + 1) % 4);
        public Stage5PreviewProfile OpenSeparate() => separate ?? (separate = new Stage5PreviewProfile { Crystals = 0 });
        public bool TryCredit(int crystals)
        {
            if (Scenario == PreviewProfileScenario.FutureSchema) return false;
            Crystals += crystals;
            return true;
        }
        public void Finish(int coins, int crystals) { LastCoins = coins; LastCrystals = crystals; }

        public string PurchaseOrSelect()
        {
            if (Scenario == PreviewProfileScenario.FutureSchema) return "Операция недоступна для этой версии профиля";
            if (Scenario == PreviewProfileScenario.WriteFailure)
            {
                Scenario = PreviewProfileScenario.Normal;
                return "Запись не удалась. Повторите то же действие";
            }
            if (!Unlocked)
            {
                if (Crystals < UnlockPrice) return "Недостаточно кристаллов";
                Crystals -= UnlockPrice;
                Unlocked = true;
                return "Тестовый вариант открыт";
            }
            Selected = true;
            return "Тестовый вариант выбран";
        }
    }

    // UI fixture. The progression contract and persistent service are supplied by role 1.
    internal sealed class Stage5PreviewSession
    {
        private readonly Stage5PreviewProfile profile;
        private readonly int seed;
        private readonly HashSet<string> deaths = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> cleared = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> roomCrystals = new Dictionary<string, int>(StringComparer.Ordinal);
        private int earned;
        private bool finished;

        public int Coins { get; private set; }
        public int Pending { get; private set; }
        public int Confirmed => profile.Crystals;
        public PreviewNpc? ActiveEffect { get; private set; }
        public bool Beneficial { get; private set; }
        public string Modification { get; set; }

        public Stage5PreviewSession(int seed, Stage5PreviewProfile profile)
        {
            this.seed = seed;
            this.profile = profile;
        }

        public bool TryRegisterDeath(string room, string spawn, string type, out int ammo, out int coins, out int crystals)
        {
            ammo = coins = crystals = 0;
            if (cleared.Contains(room) || !EnemyTypes.IsKnown(type) || !deaths.Add(spawn)) return false;
            ammo = type == EnemyTypes.Shooter && Roll(spawn + "/ammo", 100) < 50 ? 8 : 0;
            coins = Roll(spawn + "/coins", 100) < 30 ? 1 + Roll(spawn + "/coin-count", 3) : 0;
            crystals = EnemyTypes.IsElite(type) ? 75 : 5 + Roll(spawn + "/crystals", 6);
            roomCrystals.TryGetValue(room, out int existing);
            roomCrystals[room] = existing + crystals;
            return true;
        }

        public void Collect(PreviewLoot kind, int amount)
        {
            if (kind == PreviewLoot.Coins) Coins += amount;
            if (kind == PreviewLoot.Crystals) Pending += amount;
        }

        public int Clear(string room)
        {
            if (!cleared.Add(room)) return 0;
            ActiveEffect = null;
            roomCrystals.TryGetValue(room, out int crystals);
            Pending = 0;
            if (profile.TryCredit(crystals)) earned += crystals;
            return crystals;
        }

        public bool TryPay(int price)
        {
            if (Coins < price) return false;
            Coins -= price;
            return true;
        }

        public void Drink(string id)
        {
            ActiveEffect = PreviewNpc.Dealer;
            Beneficial = Roll(id + "/bottle", 100) < 70;
        }

        public void Finish()
        {
            if (finished) return;
            finished = true;
            profile.Finish(Coins, earned);
            Coins = Pending = 0;
            ActiveEffect = null;
            Modification = null;
        }

        internal int Roll(string key, int bound)
        {
            unchecked
            {
                uint hash = 2166136261u ^ (uint)seed;
                foreach (char value in key) hash = (hash ^ value) * 16777619u;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                return (int)(hash % (uint)bound);
            }
        }
    }
}
