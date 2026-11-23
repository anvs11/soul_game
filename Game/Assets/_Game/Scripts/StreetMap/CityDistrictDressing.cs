using Game.Encounters;
using UnityEngine;

namespace Game.StreetMap
{
    internal static class CityDistrictDressing
    {
        internal static void Build(Stage5PreviewController view, EncounterMap map, int segmentIndex)
        {
            int district = CityRegionRoute.DistrictAt(segmentIndex);
            if (district == 0) return;
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
                    var shell = view.Tile(market ? "Стеллаж" : "Шкаф", center, size,
                        market ? new Color(.29f,.28f,.18f) : new Color(.32f,.35f,.43f), 0);
                    shell.layer = LayerMask.NameToLayer("World");
                    shell.AddComponent<BoxCollider2D>();
                    Vector2 axis = market ? Vector2.up : Vector2.right;
                    for (int i = -1; i <= 1; ++i)
                        view.Tile("Секции", center + axis * i * .8f, new Vector2(.56f,.40f),
                            i % 2 == 0 ? new Color(.75f,.57f,.27f) : new Color(.56f,.62f,.45f), 1);
                    if (++added >= 2) break;
                }
            }
        }

        private static bool HasClearance(EncounterRoom room, Vector2 center, Vector2 size)
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
