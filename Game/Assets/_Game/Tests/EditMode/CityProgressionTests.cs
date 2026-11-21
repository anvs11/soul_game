using System;
using System.Collections.Generic;
using System.Linq;
using Game.Contracts;
using Game.Core;
using Game.Encounters;
using Game.Progression;
using NUnit.Framework;
using NpcKind = Game.Contracts.ProgressionNpcKind;
using LootKind = Game.Contracts.ProgressionLootKind;

namespace Game.Tests
{
    public sealed class CityProgressionTests
    {
        private const string RunId = "city-01";
        private static readonly ProgressionCheckpoint FirstRoom = new("1-1", "combat_a", 0);
        private static readonly ProgressionCheckpoint SecondRoom = new("1-1", "combat_b", 1);
        private static readonly ProgressionTransitionPoint[] LocalStops =
        {
            new("1-1/a-to-b", "1-1", "a-to-b", "alley", NpcKind.WeaponDealer),
            new("1-1/b-to-c", "1-1", "b-to-c", "house-door", NpcKind.BottleDealer),
            new("1-1/c-to-elite", "1-1", "c-to-elite", "house-door", NpcKind.BottleDealer)
        };

        [Test]
        public void LootRequiresDeathAndReplaysCannotRefillPartiallyCollectedAmmo()
        {
            const string spawnId = "1-1/combat_a/shooter";
            int seed = Enumerable.Range(0, 100).First(value => CityEconomy.RollLoot(value, spawnId, EnemyTypes.Shooter).Ammo == 8);
            var sink = new CombatSink { ReserveSpace = 3 };
            var session = Start(seed, sink);
            string ammo = spawnId + "/ammo";
            Assert.That(session.Snapshot.Run.Loot, Is.Empty);
            Assert.That(session.CollectLoot(Key("too-early"), ammo).Failure, Is.EqualTo(ProgressionFailure.UnknownTarget));

            AssertApplied(Register(session, spawnId, EnemyTypes.Shooter));
            Assert.That(Pickup(session, ammo).Remaining, Is.EqualTo(8));
            sink.OnReserveAdded = () => Assert.That(
                session.CollectLoot(Key("inside-resource-callback"), ammo).Failure,
                Is.EqualTo(ProgressionFailure.OperationInProgress));
            AssertApplied(session.CollectLoot(Key("collect-1"), ammo));
            Assert.That(Pickup(session, ammo).Remaining, Is.EqualTo(5));
            Assert.That(Register(session, spawnId, EnemyTypes.Shooter).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(session.ConfirmEnemyDeath(Key("second-callback"), FirstRoom, spawnId, EnemyTypes.Shooter).Status,
                Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(session.ConfirmEnemyDeath(Key("second-callback"), FirstRoom, spawnId, EnemyTypes.Melee).Failure,
                Is.EqualTo(ProgressionFailure.Conflict));
            Assert.That(Pickup(session, ammo).Remaining, Is.EqualTo(5));

            Assert.That(session.CollectLoot(Key("full-reserve"), ammo).Failure, Is.EqualTo(ProgressionFailure.ResourceFull));
            sink.ReserveSpace = 96;
            Assert.That(session.CollectLoot(Key("collect-1"), ammo).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(sink.ReserveAdded, Is.EqualTo(3));
            AssertApplied(session.CollectLoot(Key("collect-2"), ammo));
            Assert.That(sink.ReserveAdded, Is.EqualTo(8));
            Assert.That(session.CollectLoot(Key("empty-body"), ammo).Failure, Is.EqualTo(ProgressionFailure.AlreadyConsumed));
        }

        [Test]
        public void CheckpointSettlesOnlyItsRoomIncludingCrystalDropsStillOnGround()
        {
            var session = Start(42);
            AssertApplied(Register(session, "first", EnemyTypes.Melee));
            AssertApplied(Register(session, "second", EnemyTypes.ShieldShotgun));
            AssertApplied(session.ConfirmEnemyDeath(Key("death:other"), SecondRoom, "other", EnemyTypes.TeleportNurse));
            AssertApplied(session.ConfirmEnemyDeath(Key("death:summon"), SecondRoom, "other/summon/1", EnemyTypes.Melee));
            Assert.That(session.Snapshot.Run.Loot.Any(drop => drop.SpawnId == "other/summon/1"), Is.False);
            int first = Pickup(session, "first/crystals").Remaining;
            AssertApplied(session.CollectLoot(Key("pick-first"), "first/crystals"));
            AssertApplied(session.CollectLoot(Key("pick-other"), "other/crystals"));
            Assert.That(session.Snapshot.Run.PendingCrystals, Is.EqualTo(first + 75));

            AssertApplied(session.ClearCheckpoint(Key("clear-0"), FirstRoom));
            Assert.That(session.Snapshot.Run.PendingCrystals, Is.EqualTo(75));
            Assert.That(session.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(120 + first + 75));
            Assert.That(session.Snapshot.Run.Loot.Any(drop => drop.PickupId == "second/crystals"), Is.False);
            Assert.That(session.ClearCheckpoint(Key("second-clear-callback"), FirstRoom).Status,
                Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(session.ClearCheckpoint(Key("second-clear-callback"), FirstRoom).Status,
                Is.EqualTo(ProgressionStatus.Replayed));
            var wrongIndex = new ProgressionCheckpoint("1-1", "combat_a", 99);
            Assert.That(session.ClearCheckpoint(Key("bad-index"), wrongIndex).Failure, Is.EqualTo(ProgressionFailure.Conflict));
            Assert.That(session.ClearCheckpoint(Key("bad-index"), wrongIndex).Failure, Is.EqualTo(ProgressionFailure.Conflict));
        }

        [TestCase(RunOutcome.Defeat)]
        [TestCase(RunOutcome.Abandoned)]
        [TestCase(RunOutcome.Victory)]
        public void StageTransitionKeepsCoinsAndProfileFinishDiscardsPending(RunOutcome reason)
        {
            var session = Start(11);
            Fund(session, 6);
            int coins = session.Snapshot.Run.Coins;
            Assert.That(session.EnterStage(Key("early"), "1-2").Failure, Is.EqualTo(ProgressionFailure.WrongPhase));
            AssertApplied(session.ClearCheckpoint(Key("clear"), FirstRoom));
            int confirmed = session.Snapshot.Profile.ConfirmedCrystals;
            AssertApplied(session.EnterStage(Key("enter-1-2"), "1-2"));
            Assert.That(session.Snapshot.Run.StageId, Is.EqualTo("1-2"));
            Assert.That(session.Snapshot.Run.Coins, Is.EqualTo(coins));
            Assert.That(session.Snapshot.Run.Loot, Is.Empty);
            var later = new ProgressionCheckpoint("1-2", "combat_a", 4);
            AssertApplied(session.ConfirmEnemyDeath(Key("next-death"), later, "new-zombie", EnemyTypes.Jumper));
            AssertApplied(session.CollectLoot(Key("next-pickup"), "new-zombie/crystals"));
            Assert.That(session.Snapshot.Run.PendingCrystals, Is.GreaterThan(0));

            var finished = session.FinishRun(Key("finish"), reason);
            AssertApplied(finished);
            Assert.That(finished.Snapshot.Run, Is.Null);
            Assert.That(finished.Snapshot.LastRun.Value.Outcome, Is.EqualTo(reason));
            Assert.That(finished.Snapshot.LastRun.Value.FinalCoins, Is.EqualTo(coins));
            Assert.That(finished.Snapshot.LastRun.Value.ConfirmedCrystalsEarned, Is.EqualTo(confirmed - 120));
            Assert.That(finished.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(confirmed));
            Assert.That(session.FinishRun(Key("finish"), reason).Status, Is.EqualTo(ProgressionStatus.Replayed));
            AssertApplied(session.StartRun(new ProgressionCommandKey("city-02", "start"), 11, LocalStops));
            Assert.That(session.Snapshot.Run.Coins, Is.Zero);
            Assert.That(session.Snapshot.Run.PendingCrystals, Is.Zero);
            Assert.That(session.Snapshot.Profile.ConfirmedCrystals, Is.EqualTo(confirmed));
            Assert.That(session.StartRun(Key("restart-old"), 11, LocalStops).Failure, Is.EqualTo(ProgressionFailure.WrongPhase));
        }

        [Test]
        public void SuppliesUseTheCombatSinkAndConsumeOnlyAfterPositiveRestoration()
        {
            var sink = new CombatSink();
            var session = Start(1, sink);
            Assert.That(session.CollectSupply(Key("full-health"), "car-medkit", ProgressionSupplyKind.Medkit, 10).Failure,
                Is.EqualTo(ProgressionFailure.ResourceFull));
            sink.HealthSpace = 3.5f;
            AssertApplied(session.CollectSupply(Key("heal"), "car-medkit", ProgressionSupplyKind.Medkit, 10));
            Assert.That(sink.Healed, Is.EqualTo(3.5f));
            Assert.That(session.CollectSupply(Key("heal"), "car-medkit", ProgressionSupplyKind.Medkit, 10).Status,
                Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(session.CollectSupply(Key("another-heal"), "car-medkit", ProgressionSupplyKind.Medkit, 10).Failure,
                Is.EqualTo(ProgressionFailure.AlreadyConsumed));
            sink.ArmorSpace = 2;
            AssertApplied(session.CollectSupply(Key("repair"), "scrap-armor", ProgressionSupplyKind.Armor, 8));
            Assert.That(sink.Repaired, Is.EqualTo(2));
            Assert.That(session.CollectSupply(Key("repair-again"), "scrap-armor", ProgressionSupplyKind.Armor, 8).Failure,
                Is.EqualTo(ProgressionFailure.AlreadyConsumed));
        }

        [Test]
        public void CityNpcPlanIsStableUniqueAndUnchangedByStageSwitch()
        {
            var catalog = LocalStops.Concat(Enumerable.Range(2, 4).Select(stage =>
                new ProgressionTransitionPoint($"1-{stage}/a-to-b", $"1-{stage}", "a-to-b", "door",
                    NpcKind.WeaponDealer | NpcKind.BottleDealer))).ToArray();
            var seen = new HashSet<(int Weapons, int Bottles)>();
            for (int seed = -128; seed < 128; seed++)
            {
                var session = Start(seed, points: catalog);
                var plan = Plan(session);
                var npcs = session.Snapshot.Run.Npcs;
                int weapons = npcs.Count(npc => npc.Kind == NpcKind.WeaponDealer);
                int bottles = npcs.Count(npc => npc.Kind == NpcKind.BottleDealer);
                Assert.That(weapons, Is.InRange(0, 1));
                Assert.That(bottles, Is.InRange(0, 2));
                Assert.That(npcs.Select(npc => npc.Point.PointId).Distinct().Count(), Is.EqualTo(npcs.Count));
                Assert.That(npcs.All(npc => (npc.Point.AllowedNpcs & npc.Kind) != 0), Is.True);
                seen.Add((weapons, bottles));
                Assert.That(Plan(Start(seed, points: catalog.Reverse().ToArray())), Is.EqualTo(plan));
                for (int stage = 2; stage <= 5; stage++)
                    AssertApplied(session.EnterStage(Key($"enter-{stage}"), $"1-{stage}"));
                Assert.That(session.StartRun(Key("start"), seed, catalog).Status, Is.EqualTo(ProgressionStatus.Replayed));
                Assert.That(Plan(session), Is.EqualTo(plan));
            }
            Assert.That(seen.Count, Is.EqualTo(6));
        }

        [Test]
        public void NpcPlannerRespectsScarceCatalogAndRejectsDuplicatePhysicalPoints()
        {
            var shared = new ProgressionTransitionPoint("shared", "1-1", "a-to-b", "door",
                NpcKind.WeaponDealer | NpcKind.BottleDealer);
            for (int seed = 0; seed < 32; seed++)
                Assert.That(Start(seed, points: new[] { shared }).Snapshot.Run.Npcs.Count, Is.LessThanOrEqualTo(1));
            var alias = new ProgressionTransitionPoint("alias", "1-1", "a-to-b", "door", NpcKind.WeaponDealer);
            var session = new CityProgressionSession(() => 0);
            Assert.That(session.StartRun(Key("start"), 1, new[] { shared, alias }).Failure,
                Is.EqualTo(ProgressionFailure.InvalidRequest));
            Assert.That(session.StartRun(Key("start"), 1, new[] { shared }).Failure,
                Is.EqualTo(ProgressionFailure.Conflict));
        }

        [TestCase(ProgressionWeaponKind.Firearm, ProgressionModuleKind.FasterFire, RunOutcome.Defeat)]
        [TestCase(ProgressionWeaponKind.Blade, ProgressionModuleKind.SharpenedBlade, RunOutcome.Abandoned)]
        public void BrokerPaysOnceWorksOutsideInteriorAndBindsOneModule(
            ProgressionWeaponKind weaponKind, ProgressionModuleKind module, RunOutcome outcome)
        {
            double now = 12;
            var sink = new CombatSink { EquippedWeaponKind = weaponKind };
            int seed = SeedFor(NpcKind.WeaponDealer);
            var session = Start(seed, sink, () => now);
            var npc = FindNpc(session, NpcKind.WeaponDealer);
            Assert.That(Buy(session, npc, "insufficient", "weapon-1").Failure, Is.EqualTo(ProgressionFailure.InsufficientCoins));
            Assert.That(session.Snapshot.Run.Coins, Is.Zero);
            Fund(session, npc.PriceCoins);
            int before = session.Snapshot.Run.Coins;
            Assert.That(FindNpc(session, npc.Kind).State, Is.EqualTo(ProgressionNpcState.Offered)); // Dialog cancelled locally.
            Assert.That(Buy(session, npc, "insufficient", "weapon-1").Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(Buy(session, npc, "bad-weapon", "weapon-2").Failure, Is.EqualTo(ProgressionFailure.IncompatibleWeapon));

            AssertApplied(Buy(session, npc, "pay", "weapon-1"));
            Assert.That(Buy(session, npc, "pay", "weapon-1").Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(Buy(session, npc, "pay-twice", "weapon-1").Failure, Is.EqualTo(ProgressionFailure.AlreadyPurchased));
            Assert.That(session.Snapshot.Run.Coins, Is.EqualTo(before - npc.PriceCoins));
            Assert.That(FindNpc(session, npc.Kind).ReadyAtGameTime, Is.EqualTo(14));
            Assert.That(FindNpc(session, npc.Kind).ItemId, Is.Null);

            now = 13.999;
            Assert.That(Ready(session, npc).Failure, Is.EqualTo(ProgressionFailure.ServiceNotReady));
            Assert.That(FindNpc(session, npc.Kind).State, Is.EqualTo(ProgressionNpcState.Working));
            now = 14; // Player can step out during the service and only read the snapshot on return.
            AssertApplied(Ready(session, npc));
            var item = FindNpc(session, npc.Kind);
            Assert.That(item.State, Is.EqualTo(ProgressionNpcState.ItemAvailable));
            Assert.That(Ready(session, npc).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(FindNpc(session, npc.Kind).ItemId, Is.EqualTo(item.ItemId));
            Assert.That(sink.Modules, Is.Empty);

            sink.EquippedWeaponId = "weapon-2";
            Assert.That(session.ApplyWeaponModule(Key("wrong-apply"), item.ItemId, "weapon-2").Failure,
                Is.EqualTo(ProgressionFailure.IncompatibleWeapon));
            sink.EquippedWeaponId = "weapon-1";
            AssertApplied(session.ApplyWeaponModule(Key("apply"), item.ItemId, "weapon-1"));
            Assert.That(session.ApplyWeaponModule(Key("apply-again"), item.ItemId, "weapon-1").Failure,
                Is.EqualTo(ProgressionFailure.AlreadyConsumed));
            Assert.That(sink.Modules["weapon-1"], Is.EqualTo(module));

            AssertApplied(session.ClearCheckpoint(Key("clear"), FirstRoom));
            AssertApplied(session.EnterStage(Key("next"), "1-2"));
            var nextActor = new CombatSink { EquippedWeaponKind = weaponKind };
            session.BindCombat(nextActor);
            Assert.That(sink.Modules, Is.Empty);
            Assert.That(nextActor.Modules["weapon-1"], Is.EqualTo(module));
            AssertApplied(session.FinishRun(Key("finish"), outcome));
            Assert.That(nextActor.Modules, Is.Empty);
        }

        [TestCase(ProgressionEffectKind.Beneficial)]
        [TestCase(ProgressionEffectKind.Harmful)]
        public void BottleStaysOnTableUntilDrunkAndExpiresOnlyAtNextNewClear(ProgressionEffectKind kind)
        {
            int seed = Enumerable.Range(0, 256).First(value =>
            {
                var candidate = Start(value);
                var available = candidate.Snapshot.Run.Npcs.FirstOrDefault(npc => npc.Kind == NpcKind.BottleDealer);
                if (available.NpcId == null) return false;
                Fund(candidate, available.PriceCoins);
                AssertApplied(Buy(candidate, available, "buy"));
                AssertApplied(candidate.DrinkBottle(Key("drink"), FindNpc(candidate, NpcKind.BottleDealer).ItemId));
                return candidate.Snapshot.Run.ActiveBottleEffect.Value.Kind == kind;
            });
            var sink = new CombatSink();
            var session = Start(seed, sink);
            var dealer = FindNpc(session, NpcKind.BottleDealer);
            Fund(session, dealer.PriceCoins);
            AssertApplied(session.ClearCheckpoint(Key("previous-clear"), FirstRoom));
            AssertApplied(Buy(session, dealer, "buy"));
            var bottle = FindNpc(session, NpcKind.BottleDealer);
            Assert.That(bottle.State, Is.EqualTo(ProgressionNpcState.ItemAvailable));
            Assert.That(sink.BottleEffects, Is.Empty);
            Assert.That(Buy(session, dealer, "buy").Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(FindNpc(session, NpcKind.BottleDealer).ItemId, Is.EqualTo(bottle.ItemId));
            AssertApplied(session.DrinkBottle(Key("drink"), bottle.ItemId));
            Assert.That(sink.BottleEffects.Single().Kind, Is.EqualTo(kind));
            Assert.That(session.DrinkBottle(Key("drink"), bottle.ItemId).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(session.DrinkBottle(Key("drink-again"), bottle.ItemId).Failure, Is.EqualTo(ProgressionFailure.AlreadyConsumed));
            Assert.That(session.ClearCheckpoint(Key("old-clear-callback"), FirstRoom).Status, Is.EqualTo(ProgressionStatus.Replayed));
            Assert.That(sink.BottleEffects.Single().SourceItemId, Is.EqualTo(bottle.ItemId));
            AssertApplied(session.ClearCheckpoint(Key("next-clear"), SecondRoom));
            Assert.That(session.Snapshot.Run.ActiveBottleEffect, Is.Null);
            Assert.That(sink.BottleEffects, Is.Empty);
        }

        [TestCase(RunOutcome.Defeat)]
        [TestCase(RunOutcome.Abandoned)]
        public void FinishingClearsAllActiveBottles(RunOutcome outcome)
        {
            int seed = Enumerable.Range(0, 256).First(value =>
                Start(value).Snapshot.Run.Npcs.Count(npc => npc.Kind == NpcKind.BottleDealer) == 2);
            var sink = new CombatSink();
            var session = Start(seed, sink);
            var dealers = session.Snapshot.Run.Npcs.Where(npc => npc.Kind == NpcKind.BottleDealer).ToArray();
            Fund(session, dealers.Sum(npc => npc.PriceCoins));
            foreach (var dealer in dealers)
            {
                AssertApplied(Buy(session, dealer, "buy:" + dealer.NpcId));
                string item = session.Snapshot.Run.Npcs.Single(npc => npc.NpcId == dealer.NpcId).ItemId;
                AssertApplied(session.DrinkBottle(Key("drink:" + item), item));
            }
            Assert.That(session.Snapshot.Run.ActiveBottleEffects.Count, Is.EqualTo(2));
            Assert.That(sink.BottleEffects.Count, Is.EqualTo(2));
            AssertApplied(session.FinishRun(Key("finish"), outcome));
            Assert.That(session.Snapshot.Run, Is.Null);
            Assert.That(sink.BottleEffects, Is.Empty);
        }

        [Test]
        public void LootDependsOnSeedAndSpawnAndUsesSmallZombieAndSummonRules()
        {
            const string spawn = "1-1/combat_a/shooter-2";
            var expected = CityEconomy.RollLoot(42, spawn, EnemyTypes.Shooter);
            Assert.That(CityEconomy.RollLoot(42, spawn, EnemyTypes.Shooter), Is.EqualTo(expected));
            Assert.That(Enumerable.Range(0, 64).Select(seed => CityEconomy.RollLoot(seed, spawn, EnemyTypes.Shooter))
                .Distinct().Count(), Is.GreaterThan(3));
            Assert.That(Enumerable.Range(0, 64).Select(index => CityEconomy.RollLoot(42, spawn + index, EnemyTypes.Shooter))
                .Distinct().Count(), Is.GreaterThan(3));
            Assert.That(CityEconomy.RollLoot(42, "small", CityEconomy.SmallZombieEnemyTypeId).Crystals, Is.InRange(11, 15));
            Assert.That(CityEconomy.RollLoot(42, "owner/summon/2", EnemyTypes.Shooter), Is.EqualTo((0, 0, 0)));
        }

        [Test]
        public void FullCityEconomyMatchesDropRatesAndExpectedIncome()
        {
            const int seeds = 2048;
            int shooterCount = 0, magazines = 0, zombies = 0, coinDrops = 0, coins = 0, crystals = 0;
            var coinValues = new int[4];
            for (int seed = 0; seed < seeds; seed++)
            {
                for (int stage = 1; stage <= 5; stage++)
                {
                    for (int room = 0; room < 3; room++)
                    {
                        int size = 6 + (seed + stage + room) % 3;
                        for (int index = 0; index < size; index++)
                        {
                            bool shooter = (index + seed + stage) % 3 == 0;
                            if (shooter) shooterCount++;
                            Sample(seed, $"1-{stage}/combat-{room}/{index}", shooter ? EnemyTypes.Shooter : EnemyTypes.Melee,
                                5, 10);
                        }
                    }

                    if (stage == 3 || stage == 5)
                        Sample(seed, $"1-{stage}/boss", CityEconomy.HeavyBossEnemyTypeId, 270, 270);
                    else
                        for (int index = 0; index < 3; index++)
                            Sample(seed, $"1-{stage}/elite/{index}", EnemyTypes.ShieldShotgun, 75, 75);
                }
            }

            double ammoRate = (double)magazines / shooterCount;
            double coinRate = (double)coinDrops / zombies;
            double averageCoins = (double)coins / seeds;
            double averageCrystals = (double)crystals / seeds;
            TestContext.WriteLine($"City: ammo {ammoRate:P3}, coins {coinRate:P3}, {averageCoins:F2} coins and {averageCrystals:F2} crystals per run");
            Assert.That(ammoRate, Is.EqualTo(.50).Within(.008));
            Assert.That(coinRate, Is.EqualTo(.30).Within(.006));
            Assert.That(averageCoins, Is.EqualTo(69.6).Within(1.5));
            Assert.That(averageCrystals, Is.EqualTo(2002.5).Within(2));
            for (int value = 1; value <= 3; value++)
                Assert.That((double)coinValues[value] / coinDrops, Is.EqualTo(1d / 3).Within(.012));

            void Sample(int seed, string spawnId, string type, int minimum, int maximum)
            {
                var drop = CityEconomy.RollLoot(seed, spawnId, type);
                if ((drop.Ammo != 0 && (type != EnemyTypes.Shooter || drop.Ammo != 8)) ||
                    drop.Coins < 0 || drop.Coins > 3 || drop.Crystals < minimum || drop.Crystals > maximum)
                    Assert.Fail($"Invalid loot for seed {seed}, spawn {spawnId}: {drop}");
                if (drop.Ammo != 0) magazines++;
                if (drop.Coins > 0) coinDrops++;
                coinValues[drop.Coins]++;
                zombies++;
                coins += drop.Coins;
                crystals += drop.Crystals;
            }
        }

        [Test]
        public void BottleOutcomeRateIsSeventyThirtyAcrossSeeds()
        {
            int beneficial = 0, total = 0;
            for (int seed = 0; seed < 2048; seed++)
            {
                var session = Start(seed);
                var dealer = session.Snapshot.Run.Npcs.FirstOrDefault(npc => npc.Kind == NpcKind.BottleDealer);
                if (dealer.NpcId == null) continue;
                Fund(session, dealer.PriceCoins);
                AssertApplied(Buy(session, dealer, "buy"));
                AssertApplied(session.DrinkBottle(Key("drink"), FindNpc(session, NpcKind.BottleDealer).ItemId));
                total++;
                if (session.Snapshot.Run.ActiveBottleEffect.Value.Kind == ProgressionEffectKind.Beneficial) beneficial++;
            }
            Assert.That(total, Is.GreaterThan(1000));
            Assert.That((double)beneficial / total, Is.EqualTo(.7).Within(.045));
        }

        private static ProgressionCommandKey Key(string action) => new(RunId, action);

        private static CityProgressionSession Start(int seed, CombatSink sink = null, Func<double> clock = null,
            IReadOnlyList<ProgressionTransitionPoint> points = null)
        {
            var session = new CityProgressionSession(clock ?? (() => 0), 120);
            session.BindCombat(sink);
            AssertApplied(session.StartRun(Key("start"), seed, points ?? LocalStops));
            return session;
        }

        private static ProgressionResult Register(CityProgressionSession session, string spawnId, string type) =>
            session.ConfirmEnemyDeath(Key("death:" + spawnId), FirstRoom, spawnId, type);

        private static ProgressionLootSnapshot Pickup(CityProgressionSession session, string id) =>
            session.Snapshot.Run.Loot.Single(drop => drop.PickupId == id);

        private static ProgressionNpcSnapshot FindNpc(CityProgressionSession session, NpcKind kind) =>
            session.Snapshot.Run.Npcs.First(npc => npc.Kind == kind);

        private static int SeedFor(NpcKind kind) =>
            Enumerable.Range(0, 256).First(seed => Start(seed).Snapshot.Run.Npcs.Any(npc => npc.Kind == kind));

        private static ProgressionResult Buy(CityProgressionSession session, ProgressionNpcSnapshot npc,
            string action, string weaponId = null) =>
            session.BuyNpcOffer(Key(action), npc.Point.PointId, npc.NpcId, npc.OfferId, weaponId);

        private static ProgressionResult Ready(CityProgressionSession session, ProgressionNpcSnapshot npc) =>
            session.CompleteNpcService(Key("ready"), npc.Point.PointId, npc.NpcId, npc.OfferId);

        private static string[] Plan(CityProgressionSession session) =>
            session.Snapshot.Run.Npcs.Select(npc => npc.Point.PointId + ":" + npc.NpcId + ":" + npc.Kind).ToArray();

        private static void Fund(CityProgressionSession session, int target)
        {
            for (int index = 0; index < 256 && session.Snapshot.Run.Coins < target; index++)
            {
                string spawn = "funding/" + index;
                AssertApplied(Register(session, spawn, EnemyTypes.Melee));
                string loot = spawn + "/coins";
                if (session.Snapshot.Run.Loot.Any(drop => drop.PickupId == loot))
                    AssertApplied(session.CollectLoot(Key("collect:" + loot), loot));
            }
            Assert.That(session.Snapshot.Run.Coins, Is.GreaterThanOrEqualTo(target));
        }

        private static void AssertApplied(ProgressionResult result) =>
            Assert.That(result.Status, Is.EqualTo(ProgressionStatus.Applied), result.Failure.ToString());

        private sealed class CombatSink : IProgressionCombatSink
        {
            public string EquippedWeaponId { get; set; } = "weapon-1";
            public ProgressionWeaponKind EquippedWeaponKind { get; set; }
            public Dictionary<string, ProgressionModuleKind> Modules { get; } = new();
            public IReadOnlyList<ProgressionEffectSnapshot> BottleEffects { get; private set; } =
                Array.Empty<ProgressionEffectSnapshot>();
            public int ReserveSpace;
            public int ReserveAdded;
            public float HealthSpace;
            public float ArmorSpace;
            public float Healed;
            public float Repaired;
            public Action OnReserveAdded;

            public int TryAddReserve(int amount)
            {
                int accepted = Math.Min(ReserveSpace, amount);
                ReserveSpace -= accepted;
                ReserveAdded += accepted;
                if (accepted > 0) OnReserveAdded?.Invoke();
                return accepted;
            }

            public float TryHeal(float amount)
            {
                float accepted = Math.Min(HealthSpace, amount);
                HealthSpace -= accepted;
                Healed += accepted;
                return accepted;
            }

            public float TryRepair(float amount)
            {
                float accepted = Math.Min(ArmorSpace, amount);
                ArmorSpace -= accepted;
                Repaired += accepted;
                return accepted;
            }

            public void SetProgressionModifiers(IReadOnlyDictionary<string, ProgressionModuleKind> modules,
                IReadOnlyList<ProgressionEffectSnapshot> bottleEffects)
            {
                Modules.Clear();
                foreach (var module in modules) Modules.Add(module.Key, module.Value);
                BottleEffects = bottleEffects.ToArray();
            }
        }
    }
}
