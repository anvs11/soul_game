using System;
using System.Collections.Generic;
using Game.Actors;
using Game.Core;
using Game.Contracts;
using Game.Encounters;
using Game.Input;
using Game.UI;
using UnityEngine;

namespace Game.StreetMap
{
    public sealed partial class Stage5PreviewController : MonoBehaviour
    {
        private enum Deal { Available, Working, Ready, Collected }

        private sealed class Stop
        {
            public string Id;
            public PreviewNpc Kind;
            public Vector2 Road, Interior;
            public int Price;
            public Deal State;
            public float Remaining;
            public bool Melee;
            public bool Live;
            public GameObject Reward;
            public string Name => Kind == PreviewNpc.Broker ? "Барыга" : "Диллер";
            public string RewardTitle => Kind == PreviewNpc.Dealer ? "Колба Диллера" :
                Melee ? "Заточка клинка" : Live ? "Ускоренная подача" : "Улучшенное дуло";
            public string RewardDescription => Kind == PreviewNpc.Dealer ?
                "Получи случайный эффект на ближайший бой" : !Live ? "+1 к урону (макет)" :
                Melee ? "Урон ножа +25%" : "Интервал между выстрелами −20%";
        }

        private sealed class Loot
        {
            public string Room;
            public PreviewLoot Kind;
            public int Amount;
            public GameObject Object;
            public string PickupId;
        }

        private sealed class BodyLoot
        {
            public GameObject Body, Marker;
            public int Ammo;
            public float Armor;
            public string AmmoId, ArmorId;
            public string Prompt => Ammo > 0 && Armor > 0 ? "E · Обыскать тело · Патроны и броня" :
                Ammo > 0 ? "E · Обыскать тело · Патроны" : "E · Обыскать тело · Броня";
        }

        public static readonly Vector2 BrokerRoad = new Vector2(21.65f, 12f);
        public static readonly Vector2 DealerRoad = new Vector2(37f, 22.65f);
        private readonly List<Stop> stops = new List<Stop>();
        private readonly List<Loot> loot = new List<Loot>();
        private readonly List<BodyLoot> bodies = new List<BodyLoot>();
        private StreetMapScreen screen;
        private RunCoordinator run;
        private PlayerCombatFacade hero;
        private Stage5PreviewSession session;
        private Stage5PreviewView view;
        private Stage5Supplies supplies;
        private CityArmory armory;
        private BattleControls battleControls;
        private BoundedFollowCamera cameraFollow;
        private Rigidbody2D body;
        private LocalInputSource input;
        private Sprite sprite;
        private Stop inside;
        private Vector2 returnPoint;
        private bool offerOpen;
        private string notice;
        public int Coins => city == null ? session.Coins : CitySnapshot.Run?.Coins ?? 0;
        public int PendingCrystals => city == null ? session.Pending : CitySnapshot.Run?.PendingCrystals ?? 0;
        public int ConfirmedCrystals => city == null ? session.Confirmed : CitySnapshot.Profile.ConfirmedCrystals;
        public bool IsInside => inside != null;
        public Vector2 MapPosition => IsInside ? returnPoint : body.position;

        internal void Initialize(StreetMapScreen map, RunCoordinator coordinator, BattleControls controls)
        {
            screen = map;
            run = coordinator;
            hero = map.Player;
            battleControls = controls;
            session = new Stage5PreviewSession(run.CurrentRequest.Seed, Stage5PreviewProfile.For(run));
            body = hero.GetComponent<Rigidbody2D>();
            input = hero.GetComponent<LocalInputSource>();
            sprite = hero.GetComponent<SpriteRenderer>().sprite;
            foreach (var candidate in FindObjectsByType<BoundedFollowCamera>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene) cameraFollow = candidate;
            supplies = new Stage5Supplies(this, session, hero, Near, PlanCorpseArmor(screen.ActiveEncounter), ApplySupply,
                CityRegionRoute.DistrictAt(run.CitySegmentIndex));
            if (run.CurrentRequest.IsFullCity) supplies.RestoreMedkits(run.CityMedkits);
            view = new Stage5PreviewView(controls.SafeRoot, Accept, Decline, ToggleBackpack, ApplyMedkit, StoreMedkit,
                CloseSupplies, run.RetryStage5Finish);
            input?.SetNpcContext(true, false, false);
            input?.SetNpcPassage(AtPassage);
            hero.NpcRequested += TryVisit;
            hero.BackpackRequested += ToggleBackpack;

            if (run.CurrentRequest.IsStage5City)
            {
                ConnectCity();
                if (run.CurrentRequest.IsFullCity) armory = new CityArmory(this, screen, run);
            }
            else
            {
                int layout = (int)((uint)run.CurrentRequest.Seed % 4);
                if ((layout & 1) != 0) AddStop(PreviewNpc.Broker, "1-1/a-to-b", BrokerRoad, new Vector2(112, -28), 3);
                if ((layout & 2) != 0) AddStop(PreviewNpc.Dealer, "1-1/b-to-c", DealerRoad, new Vector2(136, -28), 4);
            }
            foreach (var room in screen.Rooms) room.EnemyDefeated += OnDeath;
            Render();
        }

        private bool CanInteract => run != null && run.Phase == RunPhase.InArena &&
            !run.Stage5FinishBlocked && !screen.IsPaused && !hero.IsDead;

        private string PlanCorpseArmor(EncounterPlan plan)
        {
            foreach (var room in plan.Rooms)
            foreach (var wave in room.Waves)
            foreach (var spawn in wave.Spawns)
                if (!EnemyTypes.IsElite(spawn.EnemyTypeId) && session.Roll(spawn.SpawnId + "/armor", 100) < 12)
                    return spawn.SpawnId;
            return null;
        }

        private void Update()
        {
            if (run == null) return;
            if (CanInteract)
            {
                CollectNearbyCurrency();
                foreach (var stop in stops)
                {
                    if (city != null)
                    {
                        if (stop.State == Deal.Working) UpdateCityStop(stop);
                        continue;
                    }
                    if (stop.State != Deal.Working) continue;
                    stop.Remaining -= Time.deltaTime;
                    if (stop.Remaining <= 0) Ready(stop);
                }
            }
            Render();
        }

        private void AddStop(PreviewNpc kind, string id, Vector2 road, Vector2 interior, int price)
        {
            var stop = new Stop { Kind = kind, Id = id, Road = road, Interior = interior, Price = price, Live = city != null };
            stops.Add(stop);
            bool vertical = kind == PreviewNpc.Broker;
            Vector2 threshold = road - (vertical ? Vector2.right : Vector2.up) * .57f;
            if (vertical)
            {
                var mouth = threshold - Vector2.right * .58f;
                Tile("Проход между домами", mouth, new Vector2(1.35f, 1.9f), new Color(.08f, .105f, .11f), 1);
                Outline("Вход " + stop.Name, threshold, new Vector2(.32f, 1.55f));
            }
            else
            {
                Tile("Дверь Диллера", threshold - Vector2.up * .28f, new Vector2(1.42f, .73f), new Color(.30f, .21f, .15f), 1);
                Outline("Вход " + stop.Name, threshold - Vector2.up * .21f, new Vector2(1.44f, .54f));
            }
            var floor = kind == PreviewNpc.Broker ? new Color(.14f, .16f, .18f) : new Color(.19f, .15f, .13f);
            Tile(stop.Name + " · пол", interior, new Vector2(12, 10), floor, -3);
            Wall(interior + new Vector2(-6, 0), new Vector2(.5f, 10));
            Wall(interior + new Vector2(6, 0), new Vector2(.5f, 10));
            Wall(interior + new Vector2(0, 5), new Vector2(12, .5f));
            Wall(interior + new Vector2(0, -5), new Vector2(12, .5f));
            Tile("Выход", Exit(stop), new Vector2(1.2f, .35f), Color.white, 1);
            Tile(stop.Name, interior + Vector2.up * 2.4f, new Vector2(.85f, 1.15f),
                kind == PreviewNpc.Broker ? new Color(.6f, .47f, .29f) : new Color(.42f, .35f, .58f), 2);
            WorldLabel(transform, interior + Vector2.up * 3.2f, stop.Name);
            if (kind == PreviewNpc.Dealer)
            {
                Tile("Свет лампы", interior + Vector2.up * 1.4f, new Vector2(5f, 4f), new Color(.29f, .24f, .17f), -2);
                Tile("Стол", interior + Vector2.up * 1.25f, new Vector2(2.2f, .75f), new Color(.36f, .24f, .15f), 1);
                Tile("Стул", interior + new Vector2(-1.5f, 1.2f), Vector2.one * .6f, floor * 1.6f, 1);
                Tile("Стул", interior + new Vector2(1.5f, 1.2f), Vector2.one * .6f, floor * 1.6f, 1);
                Tile("Лампа", interior + Vector2.up * 3.65f, Vector2.one * .28f, new Color(.9f, .75f, .45f), 1);
            }
            else
            {
                Wall(interior + new Vector2(-2.5f, 0), new Vector2(.7f, 7));
                Wall(interior + new Vector2(2.5f, 0), new Vector2(.7f, 7));
            }
        }

        internal GameObject Tile(string label, Vector2 position, Vector2 size, Color color, int order)
        {
            var tile = new GameObject(label);
            tile.transform.SetParent(transform, false);
            tile.transform.position = position;
            tile.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
            var renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return tile;
        }

        internal GameObject Outline(string label, Vector2 position, Vector2 size, float angle = 0)
        {
            const float width = .055f;
            var highlight = new GameObject(label);
            highlight.transform.SetParent(transform, false);
            highlight.transform.position = position;
            var tint = new Color(.85f, .96f, .80f);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Tile("Контур", position + Vector2.up * (sign * size.y / 2f), new Vector2(size.x + width, width), tint, 3)
                    .transform.SetParent(highlight.transform, true);
                Tile("Контур", position + Vector2.right * (sign * size.x / 2f), new Vector2(width, size.y + width), tint, 3)
                    .transform.SetParent(highlight.transform, true);
            }
            highlight.transform.rotation = Quaternion.Euler(0, 0, angle);
            return highlight;
        }

        internal static void WorldLabel(Transform parent, Vector2 position, string label)
        {
            var sign = new GameObject(label + " · подпись");
            sign.transform.SetParent(parent, true);
            sign.transform.position = position;
            var text = sign.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var renderer = sign.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = text.font.material;
            renderer.sortingOrder = 3;
            text.anchor = TextAnchor.MiddleCenter;
            text.fontSize = 48;
            text.characterSize = .09f;
            text.text = label;
        }

        private void Wall(Vector2 position, Vector2 size)
        {
            var wall = Tile("Стена", position, size, new Color(.08f, .09f, .10f), 1);
            wall.layer = LayerMask.NameToLayer("World");
            wall.AddComponent<BoxCollider2D>().size = sprite.bounds.size;
        }

        private void OnDeath(RoomController room, string spawn, string type, Vector2 position)
        {
            if (run.Phase != RunPhase.InArena || spawn.Contains("/summon/")) return;
            int ammo, coins, crystals;
            string ammoId = null, coinId = null, crystalId = null;
            if (city != null) ReadDeath(room.RoomId, spawn, type, out ammo, out coins, out crystals, out ammoId, out coinId, out crystalId);
            else if (!session.TryRegisterDeath(room.RoomId, spawn, type, out ammo, out coins, out crystals)) return;
            var corpse = Tile("Тело зомби", position, new Vector2(.96f, .43f), new Color(.28f, .17f, .20f), -1);
            corpse.transform.rotation = Quaternion.Euler(0, 0, -24f);
            int armor = supplies.ArmorFromCorpse(spawn);
            if (ammo > 0 || armor > 0)
            {
                var marker = Outline(ammo > 0 ? "Патроны в теле" : "Броня в теле",
                    position, new Vector2(.96f, .43f), -24f);
                bodies.Add(new BodyLoot { Body = corpse, Marker = marker, Ammo = ammo, Armor = armor,
                    AmmoId = ammoId, ArmorId = "corpse/" + spawn });
            }
            Drop(room.RoomId, position, PreviewLoot.Coins, coins, new Color(.99f, .76f, .19f), coinId);
            Drop(room.RoomId, position + Vector2.right * .33f, PreviewLoot.Crystals, crystals, new Color(.64f, .43f, .95f), crystalId);
        }

        private void Drop(string room, Vector2 position, PreviewLoot kind, int amount, Color color, string pickupId)
        {
            if (amount == 0) return;
            string label = kind == PreviewLoot.Coins ? "Монеты" : "Кристаллы";
            var item = Tile(label, position, new Vector2(.35f, .3f), color, 3);
            loot.Add(new Loot { Room = room, Object = item, Kind = kind, Amount = amount, PickupId = pickupId });
        }

        private bool Near(Vector2 position, float range)
        {
            Vector2 offset = position - body.position;
            return offset.sqrMagnitude <= range * range &&
                !Physics2D.Raycast(body.position, offset.normalized, offset.magnitude, LayerMask.GetMask("World"));
        }

        private void CollectNearbyCurrency()
        {
            for (int i = loot.Count - 1; i >= 0; i--)
            {
                var item = loot[i];
                if (!Near(item.Object.transform.position, 1.2f)) continue;
                if (city != null)
                {
                    if (!CollectCurrency(item.PickupId)) continue;
                }
                else session.Collect(item.Kind, item.Amount);
                screen.Feedback?.Play(CityCue.Pickup);
                RemoveLoot(i);
            }
        }

        private void RemoveLoot(int index)
        {
            loot[index].Object.SetActive(false);
            Destroy(loot[index].Object);
            loot.RemoveAt(index);
        }

        internal void CompleteRoom(string room)
        {
            if (city != null)
            {
                awaitingSave.Enqueue(Checkpoint(room));
                RetryCheckpoint();
                return;
            }
            session.Clear(room);
            RemoveRoomCrystals(room);
        }

        private void RemoveRoomCrystals(string room)
        {
            for (int i = loot.Count - 1; i >= 0; i--)
                if (loot[i].Room == room && loot[i].Kind == PreviewLoot.Crystals) RemoveLoot(i);
        }

        internal bool TryUseInteract()
        {
            if (supplies.Open)
            {
                if (supplies.FoundMedkit) StoreMedkit();
                else CloseSupplies();
                return true;
            }
            if (IsInside)
            {
                if (offerOpen) Decline();
                else TryVisit();
                return true;
            }
            if (AtPassage()) { TryVisit(); return true; }
            if (armory != null && armory.Use(body.position, out var response))
            {
                notice = response;
                return true;
            }
            var corpse = NearbyBody();
            if (supplies.Search(body.position, DistanceToBodySquared(corpse)))
            {
                notice = supplies.Open ? null : supplies.Notice;
                SyncSupplyInput();
                return true;
            }
            if (corpse == null) return false;
            int ammo = CollectAmmo(corpse);
            bool foundArmor = corpse.Armor > 0;
            if (foundArmor)
            {
                Vector2 center = corpse.Body.transform.position;
                Vector2 towardHero = body.position - center;
                Vector2 drop = center + towardHero.normalized * .42f;
                supplies.DropArmor(corpse.ArmorId, Near(drop, 1.25f) ? drop : center, corpse.Armor);
                corpse.Armor = 0;
            }
            corpse.Ammo -= ammo;
            if (ammo > 0) screen.Feedback?.Play(CityCue.Pickup);
            notice = foundArmor ? "Найдена броня" : ammo > 0 ? $"Патроны +{ammo}" :
                run.CityWeapon != Game.Combat.CityFirearm.Pistol ? "В теле патроны 9 мм" : "Резерв патронов полон";
            if (corpse.Ammo == 0 && corpse.Armor <= 0)
            {
                bodies.Remove(corpse);
                corpse.Marker.SetActive(false);
                Destroy(corpse.Marker);
            }
            return true;
        }

        private float DistanceToBodySquared(BodyLoot corpse) => corpse == null ? float.PositiveInfinity :
            ((Vector2)corpse.Body.transform.position - body.position).sqrMagnitude;

        private BodyLoot NearbyBody()
        {
            BodyLoot nearest = null;
            float distance = float.PositiveInfinity;
            foreach (var corpse in bodies)
            {
                var location = (Vector2)corpse.Body.transform.position;
                if (!Near(location, 1.5f)) continue;
                float candidate = Vector2.SqrMagnitude(location - body.position);
                if (candidate >= distance) continue;
                nearest = corpse;
                distance = candidate;
            }
            return nearest;
        }

        private Stop NearbyEntrance()
        {
            foreach (var room in screen.Rooms) if (room.State.IsActive) return null;
            foreach (var stop in stops) if (Near(stop.Road, 1.15f)) return stop;
            return null;
        }

        private static Vector2 Exit(Stop stop) => stop.Interior + Vector2.down * 4f;
        private bool AtPassage() => CanInteract && !offerOpen && !supplies.Open &&
            (inside == null ? NearbyEntrance() != null : Near(Exit(inside), 1.3f));

        public void TryVisit()
        {
            if (!CanInteract) return;
            if (supplies.Open) { ApplyMedkit(); return; }
            if (inside == null)
            {
                var destination = NearbyEntrance();
                if (destination == null) return;
                inside = destination;
                returnPoint = body.position;
                notice = null;
                MoveHero(inside.Interior + Vector2.down * 3.2f, new Rect(inside.Interior - new Vector2(7, 6), new Vector2(14, 12)));
                return;
            }
            if (offerOpen) { Accept(); return; }
            if (Near(Exit(inside), 1.3f))
            {
                inside = null;
                notice = null;
                MoveHero(returnPoint, StreetMapLayout.CameraBounds);
                return;
            }
            if (!Near(inside.Interior + Vector2.up, 2.2f)) return;
            if (inside.State == Deal.Ready)
            {
                if (city != null)
                {
                    if (!UseCityReward(inside)) return;
                }
                else if (inside.Kind == PreviewNpc.Dealer) session.Drink(inside.Id);
                else if (hero.ReadSnapshot().IsMelee == inside.Melee)
                    session.Modification = inside.RewardTitle;
                else { notice = "Возьмите оружие, для которого заказано усиление"; return; }
                inside.State = Deal.Collected;
                inside.Reward.SetActive(false);
                notice = inside.Kind == PreviewNpc.Dealer ? "Колба выпита" : "Усиление подобрано";
            }
            else if (inside.State == Deal.Available)
            {
                notice = null;
                offerOpen = true;
                input?.SetNpcContext(true, true, true);
            }
        }

        private void ToggleBackpack()
        {
            if (!CanInteract || offerOpen) return;
            supplies.ToggleBackpack();
            SyncSupplyInput();
        }

        private void ApplyMedkit()
        {
            if (!CanInteract || !supplies.CanHeal) return;
            supplies.Apply();
            notice = supplies.Notice;
            SyncSupplyInput();
        }

        private void StoreMedkit()
        {
            if (!CanInteract || !supplies.FoundMedkit) return;
            supplies.Store();
            notice = null;
            SyncSupplyInput();
        }

        private void CloseSupplies()
        {
            if (!CanInteract) return;
            supplies.Close();
            SyncSupplyInput();
        }

        private void SyncSupplyInput()
        {
            screen.ReleaseHeldControls();
            hero.GetComponent<PlayerDodgeController>()?.CancelMotion();
            body.linearVelocity = Vector2.zero;
            input?.SetNpcContext(true, IsInside, supplies.Open);
        }

        private void MoveHero(Vector2 position, Rect bounds)
        {
            screen.ReleaseHeldControls();
            hero.GetComponent<PlayerDodgeController>()?.CancelMotion();
            body.linearVelocity = Vector2.zero;
            body.position = position;
            hero.transform.position = position;
            Physics2D.SyncTransforms();
            cameraFollow?.Configure(hero.transform, bounds);
            input?.SetNpcContext(true, IsInside, false);
        }

        private void Accept()
        {
            if (!CanInteract || !offerOpen || inside == null || inside.State != Deal.Available) return;
            if (city != null)
            {
                if (BuyCityOffer(inside)) Decline();
                return;
            }
            if (!session.TryPay(inside.Price))
            {
                notice = $"Не хватает монет: {inside.Price - Coins}";
                return;
            }
            inside.Melee = hero.ReadSnapshot().IsMelee;
            inside.State = Deal.Working;
            inside.Remaining = inside.Kind == PreviewNpc.Broker ? 2 : 0;
            Decline();
            if (inside.Remaining == 0) Ready(inside);
        }

        private void Decline()
        {
            if (!CanInteract) return;
            offerOpen = false;
            notice = null;
            input?.SetNpcContext(true, IsInside, false);
        }

        private void Ready(Stop stop)
        {
            stop.State = Deal.Ready;
            stop.Reward = Tile(stop.Kind == PreviewNpc.Dealer ? stop.RewardTitle : "Усиление",
                stop.Interior + Vector2.up * 1.25f, new Vector2(.35f, .44f), new Color(.62f, .43f, .91f), 4);
            WorldLabel(stop.Reward.transform, (Vector2)stop.Reward.transform.position + Vector2.up * .48f, stop.RewardTitle);
        }

        private void Render()
        {
            var stop = inside ?? (CanInteract ? NearbyEntrance() : null);
            string hint = "";
            var corpse = CanInteract && inside == null && stop == null ? NearbyBody() : null;
            if (corpse == null && notice == "Резерв патронов полон") notice = null;
            if (inside == null && stop == null && CanInteract)
                hint = armory?.Prompt(body.position) ?? supplies.Prompt(body.position, DistanceToBodySquared(corpse)) ?? corpse?.Prompt ?? "";
            if (inside == null && stop != null) hint = "Space / Tab / E · Пройти";
            if (inside != null)
            {
                hint = Near(Exit(stop), 1.3f) ? "Space / Tab / E · На улицу" : stop.Name;
                if (Near(stop.Interior + Vector2.up, 2.2f))
                    hint = stop.State switch
                    {
                        Deal.Available => stop.Name + " · Tab: поговорить",
                        Deal.Working => $"{stop.Name} работает · {Mathf.Max(0, stop.Remaining):0.0} с",
                        Deal.Ready => stop.Kind == PreviewNpc.Dealer ? "Tab / E · Выпить" : "Tab / E · Взять",
                        _ => stop.Name
                    };
            }
            battleControls.SetPassageLabel(AtPassage() ? (IsInside ? "На улицу" : "Пройти") : null);
            string effect = session.ActiveEffect.HasValue
                ? session.Beneficial ? "Пузырёк: ускорение (макет)" : "Пузырёк: замедление (макет)"
                : "";
            if (session.Modification != null) effect += " " + session.Modification + " (макет)";
            var current = city == null ? null : CitySnapshot;
            if (current != null) effect = CityEffects(current.Run);
            view.RenderSupplies(CanInteract && !offerOpen, supplies.Open, supplies.FoundMedkit,
                supplies.BackpackMedkits, supplies.CanHeal);
            bool showReward = CanInteract && inside != null && inside.State == Deal.Ready &&
                !offerOpen && !supplies.Open && Near(inside.Interior + Vector2.up, 2.2f);
            view.RenderReward(showReward ? inside.RewardTitle : null, showReward ? inside.RewardDescription : null);
            view.RenderSaveRetry(run.Stage5FinishBlocked ? run.Error : null);
            view.Render(current == null ? Coins : current.Run?.Coins ?? 0,
                current == null ? PendingCrystals : current.Run?.PendingCrystals ?? 0,
                current == null ? ConfirmedCrystals : current.Profile.ConfirmedCrystals, supplies.Open ? "" : hint, effect,
                CanInteract && offerOpen ? inside.Name + "\n" + (inside.Kind == PreviewNpc.Broker
                    ? $"Улучшить оружие за {inside.Price} монет?" : $"Купить пузырёк за {inside.Price} монет?") : null,
                run.Stage5FinishBlocked ? run.Error : HasPendingCheckpoint ? notice + " · E: повторить" : notice);
        }

        internal int CarriedMedkits => supplies.BackpackMedkits;

        internal void Finish()
        {
            if (city == null) session.Finish();
            else city.BindCombat(null);
        }

        private void OnDestroy()
        {
            if (hero != null)
            {
                hero.NpcRequested -= TryVisit;
                hero.BackpackRequested -= ToggleBackpack;
            }
            if (input != null)
            {
                input.SetNpcPassage(null);
                input.SetNpcContext(false, false, false);
            }
            if (screen != null && screen.Rooms != null)
                foreach (var room in screen.Rooms) if (room != null) room.EnemyDefeated -= OnDeath;
        }
    }
}
