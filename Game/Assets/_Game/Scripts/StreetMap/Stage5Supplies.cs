using System;
using System.Collections.Generic;
using Game.Actors;
using Game.Contracts;
using UnityEngine;

namespace Game.StreetMap
{
    internal sealed class Stage5Supplies
    {
        private sealed class Cache
        {
            public Vector2 Approach;
            public string Id;
            public Vector2 ArmorDropOffset;
            public string Prompt;
            public bool Medkit;
            public float Armor;
            public bool Concealed;
            public GameObject Highlight;
            public bool HasLoot => Medkit || Armor > 0;
        }

        private readonly List<Cache> caches = new List<Cache>();
        private readonly PlayerCombatFacade hero;
        private readonly Stage5PreviewController map;
        private readonly Func<Vector2, float, bool> near;
        private readonly Func<string, ProgressionSupplyKind, int, float> apply;
        private string corpseWithArmor;
        private readonly int corpseArmor;
        private Cache selected;
        private bool backpackOpen;
        private readonly Stack<string> medkits = new();
        public bool Open => selected != null || backpackOpen;
        public string Notice { get; private set; }
        public int BackpackMedkits => medkits.Count;
        internal void RestoreMedkits(int count)
        {
            for (int index = 0; index < count; index++) medkits.Push("carried/" + index);
        }
        public bool FoundMedkit => selected != null;
        public bool CanHeal
        {
            get
            {
                var status = hero.ReadSnapshot();
                return (FoundMedkit || medkits.Count > 0) && status.Health < status.MaxHealth;
            }
        }

        public Stage5Supplies(Stage5PreviewController map, Stage5PreviewSession session,
            PlayerCombatFacade hero, Func<Vector2, float, bool> near, string corpseWithArmor,
            Func<string, ProgressionSupplyKind, int, float> apply, int district)
        {
            this.map = map;
            this.hero = hero;
            this.near = near;
            this.apply = apply;
            this.corpseWithArmor = corpseWithArmor;
            corpseArmor = corpseWithArmor == null ? 0 : RollArmorAmount(session, corpseWithArmor);
            int medkitBudget = 2;
            int armorBudget = corpseWithArmor == null ? 4 : 3;
            for (int i = 1; i < StreetMapLayout.Rooms.Count; i++)
            {
                var room = StreetMapLayout.Rooms[i];
                string key = "supply/" + room.Id;
                int side = session.Roll(key + "/side", 2) == 0 ? -1 : 1;
                float y = session.Roll(key + "/height", 2) == 0 ? -3.4f : 3.4f;
                var position = room.Center + new Vector2(side * (room.Bounds.width / 2f - 1.7f), y);
                var size = new Vector2(1.45f, 2.8f);
                if (map.RoomContours?.Overlaps(position, size + Vector2.one * .35f) == true)
                    position.y = room.Center.y - y;
                bool forklift = district == 1 && (i == 2 || i == 4);
                bool occupied = forklift && i == 4;
                var paint = forklift ? new Color(.77f,.57f,.20f) : district == 0 ? new Color(.23f,.28f,.26f) :
                    district == 1 ? new Color(.52f,.38f,.24f) : new Color(.39f,.45f,.48f);
                string containerName = forklift ? "Погрузчик" : district == 0 ? "Сгоревшая машина" :
                    district == 1 ? "Коробки" : "Тумбочки";
                var container = map.Tile(containerName, position, size, paint, 1);
                AddObstacle(container);
                var door = position - Vector2.right * side * .59f;
                Vector2 approach = position - Vector2.right * side * 1.14f;
                Vector2 highlightSize = new Vector2(.22f,.72f);
                if (forklift)
                {
                    map.Tile("Кабина погрузчика", position, new Vector2(.96f,1.1f), new Color(.15f,.19f,.20f), 2);
                    for (int fork = -1; fork <= 1; fork += 2)
                        map.Tile("Вилы", position + new Vector2(fork * .39f, 1.08f), new Vector2(.13f,.57f), new Color(.34f,.38f,.40f), 2);
                    if (occupied)
                    {
                        door = approach;
                        highlightSize = new Vector2(.61f,.96f);
                        DrawBody(map, "Тело водителя погрузчика", approach, new Color(.50f,.49f,.25f));
                    }
                }
                else if (district != 0)
                {
                    for (int seam = -1; seam <= 1; ++seam)
                        map.Tile(district == 1 ? "Упаковочная лента" : "Ручка ящика",
                            position + Vector2.up * (seam * .83f), new Vector2(district == 1 ? 1.19f : .54f,.20f),
                            district == 1 ? new Color(.71f,.59f,.39f) : new Color(.23f,.29f,.32f), 2);
                    highlightSize = new Vector2(.44f,.82f);
                }
                else
                {
                    map.Tile("Лобовое стекло", position + Vector2.up * .63f,
                        new Vector2(1.15f,.44f), new Color(.09f,.12f,.12f), 2);
                    map.Tile("Дверь машины", door, new Vector2(.2f,.66f), new Color(.37f,.42f,.39f), 2);
                }
                bool medkit = medkitBudget > 0 && session.Roll(key + "/medkit", 100) < 35;
                if (medkit) medkitBudget--;
                var cache = new Cache
                {
                    Approach = approach,
                    Id = key,
                    ArmorDropOffset = -Vector2.right * side * .64f,
                    Prompt = occupied ? "E · Обыскать водителя" : forklift ? "E · Обыскать погрузчик" :
                        district == 0 ? "E · Обыскать машину" : district == 1 ? "E · Обыскать коробки" : "E · Обыскать тумбочку",
                    Medkit = medkit,
                    Concealed = true,
                    Armor = TakeArmor(session, key, ref armorBudget)
                };
                if (cache.HasLoot)
                    cache.Highlight = map.Outline(district == 0 ? "Подсветка двери машины" : "Подсветка запаса",
                        door, highlightSize);
                caches.Add(cache);
            }

            var barrel = StreetMapLayout.Rooms[2].Center + new Vector2(-8.3f, 0);
            Vector2 entry = StreetMapLayout.Entry + new Vector2(4.7f, 3.7f);
            if (district == 0)
            {
                AddArmorSpot(session, "supply/barrel", "Бочка со снабжением", "E · Обыскать бочку",
                    barrel, new Vector2(.65f,.65f), new Color(.33f,.28f,.2f), true, false, ref armorBudget);
                AddArmorSpot(session, "supply/scrap", "Металлолом у входа", "E · Подобрать броню",
                    entry, new Vector2(.48f,.31f), new Color(.40f,.45f,.48f), false, false, ref armorBudget);
            }
            else
            {
                var uniform = new Color(.27f,.36f,.51f);
                AddArmorSpot(session, "supply/barrel", "Тело полицейского", "E · Обыскать полицейского",
                    barrel, new Vector2(.62f,.98f), uniform, false, true, ref armorBudget);
                AddArmorSpot(session, "supply/scrap", "Тело полицейского у входа", "E · Обыскать полицейского",
                    entry, new Vector2(.62f,.98f), uniform, false, true, ref armorBudget);
            }
        }

        internal static GameObject DrawBody(Stage5PreviewController map, string title, Vector2 point, Color clothing)
        {
            var body = map.Tile(title, point, new Vector2(.50f,.60f), clothing, 1);
            map.Tile("Голова погибшего", point + Vector2.up * .33f, new Vector2(.26f,.25f), new Color(.57f,.47f,.37f), 1);
            map.Tile("Ботинки", point - Vector2.up * .32f, new Vector2(.38f,.23f), new Color(.12f,.15f,.19f), 1);
            return body;
        }

        private static void AddObstacle(GameObject item)
        {
            item.layer = LayerMask.NameToLayer("World");
            item.AddComponent<BoxCollider2D>().size = item.GetComponent<SpriteRenderer>().sprite.bounds.size;
        }

        private static float TakeArmor(Stage5PreviewSession session, string key, ref int budget)
        {
            if (budget <= 0 || session.Roll(key + "/armor", 100) >= 60) return 0;
            budget--;
            return RollArmorAmount(session, key);
        }

        private static int RollArmorAmount(Stage5PreviewSession session, string key) =>
            3 + session.Roll(key + "/armor-amount", 8);

        public int ArmorFromCorpse(string spawnId)
        {
            if (corpseWithArmor != spawnId) return 0;
            corpseWithArmor = null;
            return corpseArmor;
        }

        private void AddArmorSpot(Stage5PreviewSession session, string key, string title,
            string prompt, Vector2 position, Vector2 size, Color color, bool obstacle, bool body, ref int budget)
        {
            if (body) DrawBody(map, title, position, color);
            else
            {
                var item = map.Tile(title, position, size, color, 1);
                if (obstacle) AddObstacle(item);
            }
            float amount = TakeArmor(session, key, ref budget);
            if (amount <= 0) return;
            caches.Add(new Cache
            {
                Approach = position + (obstacle ? Vector2.right * .72f : Vector2.zero),
                Id = key,
                ArmorDropOffset = obstacle ? Vector2.right * .64f : Vector2.zero,
                Prompt = prompt,
                Armor = amount,
                Concealed = obstacle || body,
                Highlight = map.Outline("Подсветка " + title, position, size)
            });
        }

        private Cache Nearby(Vector2 position, float distance)
        {
            Cache closest = null;
            foreach (var item in caches)
            {
                if (!item.HasLoot || !near(item.Approach, .9f)) continue;
                float candidate = Vector2.SqrMagnitude(item.Approach - position);
                if (candidate >= distance) continue;
                closest = item;
                distance = candidate;
            }
            return closest;
        }

        public string Prompt(Vector2 position, float maxDistanceSquared) => Nearby(position, maxDistanceSquared)?.Prompt;

        internal void DropArmor(string id, Vector2 location, float amount)
        {
            var size = new Vector2(.45f, .31f);
            var plate = map.Tile("Броня на земле", location, size, new Color(.35f, .52f, .65f), 2);
            map.Outline("Контур брони на земле", location, size).transform.SetParent(plate.transform, true);
            caches.Add(new Cache { Approach = location, Id = id, Prompt = "E · Подобрать броню",
                Armor = amount, Highlight = plate });
        }

        public bool Search(Vector2 position, float maxDistanceSquared)
        {
            var cache = Nearby(position, maxDistanceSquared);
            if (cache == null) return false;
            if (cache.Concealed && cache.Armor > 0)
            {
                DropArmor(cache.Id, cache.Approach + cache.ArmorDropOffset, cache.Armor);
                cache.Armor = 0;
                SyncHighlight(cache);
                Notice = "Найдена броня";
                if (!cache.Medkit) return true;
            }
            if (cache.Medkit)
            {
                selected = cache;
                return true;
            }
            float accepted = apply(cache.Id + "/armor", ProgressionSupplyKind.Armor, (int)cache.Armor);
            if (accepted > 0) cache.Armor = 0;
            Notice = accepted > 0 ? "Броня восстановлена" : "Броня полная";
            SyncHighlight(cache);
            return true;
        }

        public void ToggleBackpack()
        {
            if (FoundMedkit) Close();
            else backpackOpen = !backpackOpen;
        }

        public void Close() { selected = null; backpackOpen = false; }

        public void Store()
        {
            if (!FoundMedkit) return;
            medkits.Push(selected.Id);
            ConsumeFound();
        }

        public void Apply()
        {
            if (!Open || (!FoundMedkit && medkits.Count == 0)) return;
            string id = FoundMedkit ? selected.Id : medkits.Peek();
            float restored = apply(id + "/medkit", ProgressionSupplyKind.Medkit, 10);
            if (restored <= 0) return;
            if (FoundMedkit) ConsumeFound();
            else medkits.Pop();
            Notice = $"Здоровье +{restored:0.##}";
        }

        private void ConsumeFound()
        {
            var cache = selected;
            cache.Medkit = false;
            selected = null;
            SyncHighlight(cache);
        }

        private static void SyncHighlight(Cache cache) => cache.Highlight?.SetActive(cache.HasLoot);
    }
}
