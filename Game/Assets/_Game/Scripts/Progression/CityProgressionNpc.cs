using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Contracts;
using NpcKind = Game.Contracts.ProgressionNpcKind;
using NpcState = Game.Contracts.ProgressionNpcState;

namespace Game.Progression
{
    public sealed partial class CityProgressionSession
    {
        private const string FasterFire = "fire-rate";
        private const string SharpenedBlade = "blade-sharpening";
        private static readonly IReadOnlyDictionary<string, ProgressionModuleKind> EmptyModules =
            new ReadOnlyDictionary<string, ProgressionModuleKind>(new Dictionary<string, ProgressionModuleKind>());
        private static readonly IReadOnlyList<ProgressionEffectSnapshot> EmptyEffects =
            Array.Empty<ProgressionEffectSnapshot>();

        public ProgressionResult BuyNpcOffer(ProgressionCommandKey key, string pointId, string npcId, string offerId,
            string weaponInstanceId = null) =>
            Active(key, nameof(BuyNpcOffer), (pointId, npcId, offerId, weaponInstanceId), () =>
            {
                int index = FindOffer(pointId, npcId, offerId);
                if (index < 0) return Reject(ProgressionFailure.UnknownTarget);
                var npc = run.Npcs[index];
                if (npc.Point.StageId != run.StageId) return Reject(ProgressionFailure.WrongPhase);
                if (npc.State != NpcState.Offered) return Reject(ProgressionFailure.AlreadyPurchased);
                if (run.Coins < npc.PriceCoins) return Reject(ProgressionFailure.InsufficientCoins);

                ProgressionNpcSnapshot next;
                if (npc.Kind == NpcKind.WeaponDealer)
                {
                    if (!Equipped(weaponInstanceId)) return Reject(ProgressionFailure.IncompatibleWeapon);
                    if (run.Modules.ContainsKey(weaponInstanceId)) return Reject(ProgressionFailure.AlreadyPurchased);
                    string definition = combat.EquippedWeaponKind == ProgressionWeaponKind.Blade
                        ? SharpenedBlade : FasterFire;
                    next = new ProgressionNpcSnapshot(npc.Point, npc.NpcId, npc.Kind, npc.OfferId, npc.PriceCoins,
                        NpcState.Working, gameTime() + CityEconomy.WeaponServiceSeconds, itemDefinitionId: definition,
                        weaponInstanceId: weaponInstanceId);
                }
                else
                {
                    if (weaponInstanceId != null) return Reject(ProgressionFailure.InvalidRequest);
                    next = MakeItem(npc, "dealer-bottle");
                }

                run.Coins -= npc.PriceCoins;
                run.Npcs[index] = next;
                return Applied;
            });

        public ProgressionResult CompleteNpcService(ProgressionCommandKey key, string pointId, string npcId,
            string offerId) =>
            Active(key, nameof(CompleteNpcService), (pointId, npcId, offerId), () =>
            {
                int index = FindOffer(pointId, npcId, offerId);
                if (index < 0) return Reject(ProgressionFailure.UnknownTarget);
                var npc = run.Npcs[index];
                if (npc.Kind != NpcKind.WeaponDealer || npc.State != NpcState.Working)
                    return Reject(ProgressionFailure.WrongPhase);
                if (gameTime() < npc.ReadyAtGameTime.Value)
                    return Retry(ProgressionFailure.ServiceNotReady);

                run.Npcs[index] = MakeItem(npc, npc.ItemDefinitionId);
                return Applied;
            });

        public ProgressionResult ApplyWeaponModule(ProgressionCommandKey key, string itemId,
            string weaponInstanceId) =>
            Active(key, nameof(ApplyWeaponModule), (itemId, weaponInstanceId), () =>
            {
                int index = FindItem(itemId, NpcKind.WeaponDealer, out var problem);
                if (problem != ProgressionFailure.None) return Reject(problem);
                var npc = run.Npcs[index];
                if (npc.WeaponInstanceId != weaponInstanceId || !Equipped(weaponInstanceId))
                    return Reject(ProgressionFailure.IncompatibleWeapon);

                bool blade = npc.ItemDefinitionId == SharpenedBlade;
                var expected = blade ? ProgressionWeaponKind.Blade : ProgressionWeaponKind.Firearm;
                if (combat.EquippedWeaponKind != expected) return Reject(ProgressionFailure.IncompatibleWeapon);

                run.Modules.Add(weaponInstanceId, blade ? ProgressionModuleKind.SharpenedBlade :
                    ProgressionModuleKind.FasterFire);
                run.Npcs[index] = UseItem(npc);
                RefreshCombat();
                return Applied;
            });

        public ProgressionResult DrinkBottle(ProgressionCommandKey key, string itemId) =>
            Active(key, nameof(DrinkBottle), itemId, () =>
            {
                int index = FindItem(itemId, NpcKind.BottleDealer, out var problem);
                if (problem != ProgressionFailure.None) return Reject(problem);
                var npc = run.Npcs[index];
                bool beneficial = CityEconomy.Roll(run.Seed, itemId, "bottle-effect", 10) < 7;
                run.BottleEffects.Add(new ProgressionEffectSnapshot(beneficial ? "dealer-haste" : "dealer-slow",
                    beneficial ? ProgressionEffectKind.Beneficial : ProgressionEffectKind.Harmful, itemId));
                run.Npcs[index] = UseItem(npc);
                RefreshCombat();
                return Applied;
            });

        private bool Equipped(string id) => Id(id) && combat != null && combat.EquippedWeaponId == id;

        private int FindOffer(string pointId, string npcId, string offerId) => run.Npcs.FindIndex(npc =>
            npc.Point.PointId == pointId && npc.NpcId == npcId && npc.OfferId == offerId);

        private int FindItem(string itemId, NpcKind kind, out ProgressionFailure failure)
        {
            int index = Id(itemId) ? run.Npcs.FindIndex(npc => npc.ItemId == itemId && npc.Kind == kind) : -1;
            if (index < 0)
            {
                failure = ProgressionFailure.UnknownTarget;
                return index;
            }
            var found = run.Npcs[index];
            failure = found.Point.StageId != run.StageId ? ProgressionFailure.WrongPhase :
                found.State == NpcState.ItemUsed ? ProgressionFailure.AlreadyConsumed : ProgressionFailure.None;
            return index;
        }

        private static ProgressionNpcSnapshot MakeItem(ProgressionNpcSnapshot npc, string definition) =>
            new ProgressionNpcSnapshot(npc.Point, npc.NpcId, npc.Kind, npc.OfferId, npc.PriceCoins,
                NpcState.ItemAvailable, itemId: npc.NpcId + "/item", itemDefinitionId: definition,
                weaponInstanceId: npc.WeaponInstanceId);

        private static ProgressionNpcSnapshot UseItem(ProgressionNpcSnapshot npc) =>
            new ProgressionNpcSnapshot(npc.Point, npc.NpcId, npc.Kind, npc.OfferId, npc.PriceCoins, NpcState.ItemUsed,
                itemId: npc.ItemId, itemDefinitionId: npc.ItemDefinitionId, weaponInstanceId: npc.WeaponInstanceId);

        private void RefreshCombat()
        {
            if (combat == null) return;
            var modules = run == null ? EmptyModules :
                new ReadOnlyDictionary<string, ProgressionModuleKind>(run.Modules);
            combat.SetProgressionModifiers(modules, run == null ? EmptyEffects : run.BottleEffects.AsReadOnly());
        }
    }
}
