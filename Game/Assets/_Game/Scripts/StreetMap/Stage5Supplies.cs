using System;
using System.Collections.Generic;
using Game.Actors;
using UnityEngine;

namespace Game.StreetMap
{
    internal sealed class Stage5Supplies
    {
        private sealed class Cache
        {
            public Vector2 Approach;
            public string Prompt;
            public bool Medkit;
            public float Armor;
            public GameObject Highlight;
            public bool HasLoot => Medkit || Armor > 0;
        }

        private readonly List<Cache> caches = new List<Cache>();
        private readonly PlayerCombatFacade hero;
        private readonly Func<Vector2, float, bool> near;
        private Cache selected;
        private bool backpackOpen;
        private int medkits;
        public bool Open => selected != null || backpackOpen;
        public string Notice { get; private set; }
        public int BackpackMedkits => medkits;
        public bool FoundMedkit => selected != null;
        public bool CanHeal
        {
            get
            {
                var status = hero.ReadSnapshot();
                return (FoundMedkit || medkits > 0) && status.Health < status.MaxHealth;
            }
        }

        public Stage5Supplies(Stage5PreviewController map, Stage5PreviewSession session,
            PlayerCombatFacade hero, Func<Vector2, float, bool> near)
        {
            this.hero = hero;
            this.near = near;
            int medkitBudget = 2;
            int armorBudget = 4;
            for (int i = 1; i < StreetMapLayout.Rooms.Count; i++)
            {
                var room = StreetMapLayout.Rooms[i];
                string key = "supply/" + room.Id;
                int side = session.Roll(key + "/side", 2) == 0 ? -1 : 1;
                float y = session.Roll(key + "/height", 2) == 0 ? -3.4f : 3.4f;
                var position = room.Center + new Vector2(side * (room.Bounds.width / 2f - 1.7f), y);
                var size = new Vector2(1.45f, 2.8f);
                var paint = new Color(.23f, .28f, .26f);
                var car = map.Tile("Сгоревшая машина", position, size, paint, 1);
                AddObstacle(car);
                map.Tile("Лобовое стекло", position + Vector2.up * .63f, new Vector2(1.15f, .44f), new Color(.09f, .12f, .12f), 2);
                var door = position - Vector2.right * side * .59f;
                map.Tile("Дверь машины", door, new Vector2(.2f, .66f), new Color(.37f, .42f, .39f), 2);
                bool medkit = medkitBudget > 0 && session.Roll(key + "/medkit", 100) < 35;
                if (medkit) medkitBudget--;
                var cache = new Cache
                {
                    Approach = position - Vector2.right * side * 1.14f,
                    Prompt = "E · Обыскать машину",
                    Medkit = medkit,
                    Armor = TakeArmor(session, key, ref armorBudget)
                };
                if (cache.HasLoot)
                    cache.Highlight = map.Outline("Подсветка двери машины", door, new Vector2(.22f, .72f));
                caches.Add(cache);
            }

            var barrel = StreetMapLayout.Rooms[2].Center + new Vector2(-8.3f, 0);
            AddArmorSpot(map, session, "supply/barrel", "Бочка со снабжением", "E · Обыскать бочку",
                barrel, new Vector2(.65f, .65f), new Color(.33f, .28f, .2f), true, ref armorBudget);
            AddArmorSpot(map, session, "supply/scrap", "Металлолом у входа", "E · Подобрать броню",
                StreetMapLayout.Entry + new Vector2(4.7f, 3.7f), new Vector2(.48f, .31f),
                new Color(.40f, .45f, .48f), false, ref armorBudget);
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
            return 1;
        }

        private void AddArmorSpot(Stage5PreviewController map, Stage5PreviewSession session, string key, string title,
            string prompt, Vector2 position, Vector2 size, Color color, bool obstacle, ref int budget)
        {
            var item = map.Tile(title, position, size, color, 1);
            if (obstacle) AddObstacle(item);
            float amount = TakeArmor(session, key, ref budget);
            if (amount <= 0) return;
            caches.Add(new Cache
            {
                Approach = position + (obstacle ? Vector2.right * .72f : Vector2.zero),
                Prompt = prompt,
                Armor = amount,
                Highlight = map.Outline("Подсветка " + title, position, size)
            });
        }

        private Cache Nearby(Vector2 position)
        {
            Cache closest = null;
            float distance = float.PositiveInfinity;
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

        public string Prompt(Vector2 position) => Nearby(position)?.Prompt;

        public bool Search(Vector2 position)
        {
            var cache = Nearby(position);
            if (cache == null) return false;
            if (cache.Medkit)
            {
                selected = cache;
                return true;
            }
            float accepted = hero.TryRepair(cache.Armor);
            cache.Armor -= accepted;
            Notice = accepted > 0 ? $"Броня +{accepted:0.##}" : "Броня полная";
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
            medkits++;
            ConsumeFound();
        }

        public void Apply()
        {
            if (!Open || (!FoundMedkit && medkits == 0)) return;
            float restored = hero.TryHeal(10);
            if (restored <= 0) return;
            if (FoundMedkit) ConsumeFound();
            else medkits--;
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
