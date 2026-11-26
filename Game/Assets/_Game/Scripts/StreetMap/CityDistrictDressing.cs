using Game.Encounters;
using UnityEngine;

namespace Game.StreetMap
{
    internal static class CityDistrictDressing
    {
        internal static string RoomName(int district, string roomId) => (district, roomId) switch
        {
            (1, "entry") => "Вход в гипермаркет",
            (1, "combat_a") => "Кассовая зона",
            (1, "combat_b") => "Торговые ряды",
            (1, "combat_c") => "Зона разгрузки",
            (1, "elite") => "Склад гипермаркета",
            (2, "entry") => "Холл",
            (2, "combat_a") => "Открытый офис",
            (2, "combat_b") => "Переговорные",
            (2, "combat_c") => "Рабочие места и зона отдыха",
            _ => null
        };

        internal static void Build(Stage5PreviewController view, EncounterMap map, int segmentIndex)
        {
            int district = CityRegionRoute.DistrictAt(segmentIndex);
            if (district == 0)
            {
                DressStreet(view, segmentIndex);
                return;
            }
            bool market = district == 1;
            bool roof = segmentIndex == 14;
            string name = CityRegionDefinition.Load().DistrictAt(segmentIndex).DisplayName;
            string entry = market ? $"{name} · секция {segmentIndex - 4}" :
                roof ? "Крыша" : $"{name} · этаж {segmentIndex - 9}";
            Stage5PreviewController.WorldLabel(view.transform, StreetMapLayout.Entry + Vector2.up * 2.3f, entry);

            foreach (var room in map.Rooms)
            {
                if (room.RoomId == "entry" || (roof && room.IsElite)) continue;
                float sign = segmentIndex % 2 == 0 ? 1f : -1f;
                var size = market ? new Vector2(1.12f, 3.75f) : new Vector2(2.45f, 1.12f);
                var candidates = new[] { new Vector2(-4, -2.6f * sign), new Vector2(4, 2.6f * sign),
                    new Vector2(-5.5f, 3.5f), new Vector2(5.5f, -3.5f) };
                int added = 0;
                foreach (var position in candidates)
                {
                    if (!HasClearance(room, position, size)) continue;
                    var center = new Vector2(room.CenterX, room.CenterY) + position;
                    if (view.RoomContours?.Overlaps(center, size + Vector2.one * .3f) == true) continue;
                    var shell = view.Tile(market ? "Стеллаж с продуктами" : roof ? "Вентиляция" : "Столы и перегородка", center, size,
                        market ? new Color(.29f,.28f,.18f) : new Color(.32f,.35f,.43f), 0);
                    shell.layer = LayerMask.NameToLayer("World");
                    shell.AddComponent<BoxCollider2D>().size = shell.GetComponent<SpriteRenderer>().sprite.bounds.size;
                    if (market)
                    {
                        var products = new[] {new Color(.8f,.40f,.21f),new Color(.79f,.64f,.34f),
                            new Color(.48f,.62f,.36f),new Color(.47f,.63f,.72f)};
                        for (int row = 0; row < 4; ++row)
                        for (int side = -1; side <= 1; side += 2)
                            view.Tile("Продукты", center + new Vector2(side * .26f, (row - 1.5f) * .84f),
                                new Vector2(.32f,.64f), products[(row + (side + 1) / 2) % products.Length], 1);
                    }
                    else if (!roof) DressOfficeCover(view, center, room.RoomId, added);
                    if (++added >= 2) break;
                }
            }
            if (segmentIndex == 5) view.AddMarketCheckout();
        }

        private static void DressStreet(Stage5PreviewController view, int segmentIndex)
        {
            if (segmentIndex == 0) return;
            Vector2 street = StreetMapLayout.Rooms[1].Center;
            if (segmentIndex == 1)
            {
                Vector2 crossing = street - Vector2.right * 6;
                for (int stripe = -3; stripe <= 3; ++stripe)
                    view.Tile("Зебра", crossing + Vector2.up * stripe * .52f, new Vector2(2.1f,.22f),
                        new Color(.67f,.65f,.53f), -7);
                WorldSign(view, street + new Vector2(-5.8f, 5.4f), "Перекрёсток");
                return;
            }
            if (segmentIndex != 2) return;
            Vector2 yard = StreetMapLayout.Rooms[3].Center;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 verge = yard + new Vector2(side * 5.2f, 4.8f);
                view.Tile("Газон двора", verge, new Vector2(3.5f, 2.5f), new Color(.23f,.32f,.19f), -7);
                view.Tile("Скамья", verge, new Vector2(2.2f,.4f), new Color(.46f,.32f,.21f), -5);
                view.Tile("Парковые фонари", verge + Vector2.up * .79f, new Vector2(.21f,.21f), new Color(.8f,.68f,.36f), -5);
            }
            WorldSign(view, yard + Vector2.up * 6, "Жилой двор");
        }

        private static void DressOfficeCover(Stage5PreviewController view, Vector2 center, string roomId, int index)
        {
            if (roomId == "combat_c" && index == 0)
            {
                view.Tile("Подиум зоны отдыха", center, new Vector2(2.4f,1.09f), new Color(.34f,.50f,.38f), 1);
                view.Tile("Мягкое кресло", center + Vector2.left * .64f, new Vector2(.64f,.65f), new Color(.24f,.56f,.59f), 2);
                view.Tile("Диван в зоне отдыха", center + Vector2.right * .40f, new Vector2(.96f,.69f), new Color(.42f,.55f,.59f), 2);
                return;
            }
            view.Tile("Перегородка", center + Vector2.up * .39f, new Vector2(2.43f,.25f), new Color(.54f,.66f,.71f), 1);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 desk = center + Vector2.right * side * .61f;
                view.Tile("Рабочий стол", desk, new Vector2(1.1f,.67f), new Color(.55f,.50f,.40f), 1);
                view.Tile("Монитор", desk + Vector2.up * .20f, new Vector2(.43f,.18f), new Color(.15f,.23f,.29f), 2);
                view.Tile("Офисный стул", desk - Vector2.up * .64f, new Vector2(.42f,.36f), new Color(.28f,.40f,.46f), 1);
            }
        }

        internal static void DressTraderRoom(Stage5PreviewController view, Vector2 center, int district, bool dealer)
        {
            if (district == 0) return;
            WorldSign(view, center + Vector2.up * 4.2f, district == 1
                ? dealer ? "Холодильная камера" : "Подсобная" : dealer ? "Столовая" : "Тренажёрный зал");
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 wing = center + Vector2.right * (side * 4.0f);
                if (district == 1)
                {
                    for (int row = -1; row <= 1; ++row)
                    {
                        Vector2 item = wing + Vector2.up * (row * 1.62f);
                        view.Tile(dealer ? "Крюк холодильника" : "Полка подсобки", item, new Vector2(1.28f,.30f),
                            dealer ? new Color(.53f,.65f,.68f) : new Color(.46f,.42f,.34f), 1);
                        view.Tile(dealer ? "Подвес охлаждённых туш" : "Упаковка подсобки",
                            item - Vector2.up * .32f, new Vector2(.48f,.55f),
                            dealer ? new Color(.64f,.37f,.36f) : new Color(.61f,.50f,.34f), 1);
                    }
                }
                else
                {
                    var station = wing + Vector2.up * 1.0f;
                    view.Tile(dealer ? "Стол столовой" : "Тренажёр", station, new Vector2(1.46f,2.3f),
                        dealer ? new Color(.60f,.49f,.35f) : new Color(.30f,.35f,.44f), 1);
                    view.Tile(dealer ? "Стулья столовой" : "Скамья тренажёрного зала", station - Vector2.up * 1.35f,
                        new Vector2(1f,.45f), dealer ? new Color(.55f,.61f,.43f) : new Color(.74f,.45f,.21f), 1);
                }
            }
        }

        private static void WorldSign(Stage5PreviewController view, Vector2 position, string label) =>
            Stage5PreviewController.WorldLabel(view.transform, position, label);

        internal static bool HasClearance(EncounterRoom room, Vector2 center, Vector2 size)
        {
            var min = center - size * .5f;
            var max = center + size * .5f;
            foreach (var point in room.Points)
            {
                var closest = new Vector2(Mathf.Clamp(point.X, min.x, max.x), Mathf.Clamp(point.Y, min.y, max.y));
                if ((closest - new Vector2(point.X, point.Y)).sqrMagnitude < Mathf.Pow(point.ClearanceRadius + .25f, 2))
                    return false;
            }
            return true;
        }
    }
}
