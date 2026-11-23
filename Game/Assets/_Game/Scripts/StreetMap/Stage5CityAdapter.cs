using System;
using System.Collections.Generic;
using System.Linq;
using Game.Contracts;
using Game.Combat;
using Game.Core;
using Game.Progression;
using Game.UI;
using UnityEngine;

namespace Game.StreetMap
{
    public sealed partial class Stage5PreviewController
    {
        private CityProgressionSession city;
        private readonly Queue<ProgressionCheckpoint> awaitingSave = new();
        private long actionIndex;
        public bool HasPendingCheckpoint => awaitingSave.Count > 0;
        private ProgressionSnapshot CitySnapshot => city.Snapshot;

        private void ConnectCity()
        {
            city = run.Stage5.Session;
            BindFirearmIdentity();
            var weapon = hero.GetComponent<WeaponController>();
            if (run.CityWeapon != CityFirearm.Pistol) weapon.EquipCityFamily(run.CityWeapon);
            else if (CitySnapshot.Run.StartingWeaponId == "pistol-city") weapon.EquipCityStarter();
            if (run.CurrentRequest.IsFullCity)
                weapon.EnableCityCriticals(unchecked(run.CurrentRequest.Seed + run.CitySegmentIndex * 1249));
            if (run.PlayerTransfer.HasValue) hero.RestoreForEncounter(run.PlayerTransfer.Value);
            city.BindCombat(hero);
            foreach (var npc in CitySnapshot.Run.Npcs)
            {
                if (npc.Point.StageId != run.CitySegmentId) continue;
                if (npc.Kind == ProgressionNpcKind.WeaponDealer)
                    AddStop(PreviewNpc.Broker, npc.Point.PointId, BrokerRoad, new Vector2(112, -28), npc.PriceCoins);
                if (npc.Kind == ProgressionNpcKind.BottleDealer)
                    AddStop(PreviewNpc.Dealer, npc.Point.PointId, DealerRoad, new Vector2(136, -28), npc.PriceCoins);
            }
        }

        private void BindFirearmIdentity()
        {
            string ranged = run.CityWeapon == CityFirearm.Pistol ? "/firearm" : "/firearm/" + run.CityWeapon;
            hero.BindProgressionWeaponIds(run.RunId + ranged, run.RunId + "/blade");
        }

        internal void EquipCityFirearm(CityFirearm family)
        {
            run.CityWeapon = family;
            BindFirearmIdentity();
            hero.GetComponent<WeaponController>().EquipCityFamily(family);
            city.BindCombat(hero);
        }

        private ProgressionCommandKey Key(string id) => new(run.RunId, id);
        private ProgressionCommandKey Attempt(string operation) => Key(run.CitySegmentId + "/" + operation + "/" + ++actionIndex);

        private ProgressionCheckpoint Checkpoint(string room) =>
            new(run.CitySegmentId, room, run.CitySegmentIndex * 4 + Array.FindIndex(screen.Rooms, candidate => candidate.RoomId == room));

        private void ReadDeath(string room, string spawn, string type, out int ammo, out int coins, out int crystals,
            out string ammoId, out string coinId, out string crystalId)
        {
            ammo = coins = crystals = 0;
            ammoId = coinId = crystalId = null;
            var result = city.ConfirmEnemyDeath(Key("death/" + spawn), Checkpoint(room), spawn, type);
            if (result.Status != ProgressionStatus.Applied) return;
            foreach (var drop in result.Snapshot.Run.Loot)
            {
                if (drop.SpawnId != spawn) continue;
                switch (drop.Kind)
                {
                    case ProgressionLootKind.Ammo: ammo = drop.Remaining; ammoId = drop.PickupId; break;
                    case ProgressionLootKind.Coins: coins = drop.Remaining; coinId = drop.PickupId; break;
                    case ProgressionLootKind.Crystals: crystals = drop.Remaining; crystalId = drop.PickupId; break;
                }
            }
        }

        private bool CollectCurrency(string id)
        {
            var result = city.CollectLoot(Key("currency/" + id), id);
            return result.Failure == ProgressionFailure.None ||
                result.Failure == ProgressionFailure.AlreadyConsumed;
        }

        private int CollectAmmo(BodyLoot corpse)
        {
            if (corpse.Ammo <= 0 || run.CityWeapon != CityFirearm.Pistol) return 0;
            if (city == null) return hero.TryAddReserve(corpse.Ammo);
            var result = city.CollectLoot(Attempt("ammo"), corpse.AmmoId);
            if (result.Failure != ProgressionFailure.None) return 0;
            int remaining = result.Snapshot.Run.Loot.FirstOrDefault(item => item.PickupId == corpse.AmmoId).Remaining;
            return corpse.Ammo - remaining;
        }

        private float ApplySupply(string id, ProgressionSupplyKind kind, int amount)
        {
            if (city == null) return kind == ProgressionSupplyKind.Armor ? hero.TryRepair(amount) : hero.TryHeal(amount);
            var before = hero.ReadSnapshot();
            var result = city.CollectSupply(Attempt("supply"), run.CitySegmentId + "/" + id, kind, amount);
            if (result.Failure != ProgressionFailure.None) return 0;
            screen.Feedback?.Play(CityCue.Pickup);
            var after = hero.ReadSnapshot();
            return kind == ProgressionSupplyKind.Armor ? after.Armor - before.Armor : after.Health - before.Health;
        }

        public bool RetryCheckpoint()
        {
            if (!HasPendingCheckpoint) return false;
            while (awaitingSave.Count > 0)
            {
                var checkpoint = awaitingSave.Peek();
                var result = city.ClearCheckpoint(Key("clear/" + checkpoint.StageId + "/" + checkpoint.RoomId), checkpoint);
                if (result.Failure != ProgressionFailure.None)
                {
                    notice = Stage5Runtime.Describe(result.Failure);
                    return true;
                }
                awaitingSave.Dequeue();
                RemoveRoomCrystals(checkpoint.RoomId);
                notice = null;
            }
            return true;
        }

        private ProgressionNpcSnapshot FindNpc(Stop stop) =>
            CitySnapshot.Run.Npcs.First(npc => npc.Point.PointId == stop.Id);

        private void UpdateCityStop(Stop stop)
        {
            var npc = FindNpc(stop);
            if (npc.State == ProgressionNpcState.Working)
            {
                stop.Remaining = (float)Math.Max(0, npc.ReadyAtGameTime.GetValueOrDefault() - Time.timeAsDouble);
                if (stop.Remaining <= 0)
                    npc = city.CompleteNpcService(Key("ready/" + npc.NpcId), stop.Id, npc.NpcId, npc.OfferId)
                        .Snapshot.Run.Npcs.First(value => value.NpcId == npc.NpcId);
            }
            stop.Melee = npc.ItemDefinitionId == "blade-sharpening";
            if (npc.State == ProgressionNpcState.ItemAvailable && stop.State != Deal.Ready) Ready(stop);
            else if (npc.State == ProgressionNpcState.Working) stop.State = Deal.Working;
            else if (npc.State == ProgressionNpcState.ItemUsed) stop.State = Deal.Collected;
        }

        private bool BuyCityOffer(Stop stop)
        {
            var npc = FindNpc(stop);
            var result = city.BuyNpcOffer(Attempt("buy"), stop.Id, npc.NpcId, npc.OfferId,
                stop.Kind == PreviewNpc.Broker ? hero.EquippedWeaponId : null);
            if (result.Failure != ProgressionFailure.None)
            {
                notice = Stage5Runtime.Describe(result.Failure);
                return false;
            }
            UpdateCityStop(stop);
            return true;
        }

        private bool UseCityReward(Stop stop)
        {
            var npc = FindNpc(stop);
            var result = stop.Kind == PreviewNpc.Dealer
                ? city.DrinkBottle(Key("drink/" + npc.ItemId), npc.ItemId)
                : city.ApplyWeaponModule(Attempt("equip"), npc.ItemId, hero.EquippedWeaponId);
            if (result.Failure == ProgressionFailure.None) return true;
            notice = Stage5Runtime.Describe(result.Failure);
            return false;
        }

        private string CityEffects(ProgressionRunSnapshot current)
        {
            if (current == null) return "";
            var labels = current.ActiveBottleEffects.Select(effect =>
                effect.Kind == ProgressionEffectKind.Beneficial ? "Колба: скорость ×1,2" : "Колба: скорость ×0,8");
            return string.Join(" · ", labels.Concat(stops.Where(stop => stop.Kind == PreviewNpc.Broker &&
                stop.State == Deal.Collected).Select(stop => stop.RewardTitle)));
        }
    }
}
