using System.Collections.Generic;
using UnityEngine;

namespace Game.StreetMap
{
    internal sealed class CityRoomContours
    {
        private readonly List<RectInt> removed = new();

        internal CityRoomContours(int segment, int seed)
        {
            for (int i = 1; i < StreetMapLayout.Rooms.Count; i++)
            {
                var room = StreetMapLayout.Rooms[i].Bounds;
                int pattern = (int)((uint)seed % 4) + segment + i;
                switch (pattern % 4)
                {
                    case 0:
                        removed.Add(Corner(room, false, true, 5, 3));
                        break;
                    case 1:
                        removed.Add(Corner(room, true, true, 5, 3));
                        removed.Add(Corner(room, true, false, 3, 3));
                        break;
                    case 2:
                        removed.Add(Corner(room, false, false, 5, 3));
                        removed.Add(Corner(room, true, true, 4, 3));
                        break;
                    default:
                        removed.Add(Corner(room, false, true, 3, 5));
                        removed.Add(Corner(room, true, true, 3, 5));
                        break;
                }
            }
        }

        private static RectInt Corner(RectInt room, bool east, bool north, int width, int height) =>
            new(east ? room.xMax - width : room.xMin, north ? room.yMax - height : room.yMin, width, height);

        internal bool IsWalkable(Vector2Int cell)
        {
            foreach (var cut in removed)
                if (cut.Contains(cell)) return false;
            return StreetMapLayout.IsWalkable(cell);
        }

        internal bool Overlaps(Vector2 center, Vector2 size)
        {
            var bounds = new Rect(center - size * .5f, size);
            foreach (var cut in removed)
                if (bounds.Overlaps(new Rect(cut.position, cut.size))) return true;
            return false;
        }

        internal IEnumerable<RectInt> Strips(RectInt room)
        {
            int y = room.yMin;
            while (y < room.yMax)
            {
                var row = Span(room, y);
                int top = y + 1;
                while (top < room.yMax && Span(room, top) == row) top++;
                yield return new RectInt(row.x, y, row.y - row.x, top - y);
                y = top;
            }
        }

        private Vector2Int Span(RectInt room, int y)
        {
            int left = room.xMin, right = room.xMax;
            foreach (var cut in removed)
            {
                if (y < cut.yMin || y >= cut.yMax) continue;
                if (cut.xMin == room.xMin) left = cut.xMax;
                if (cut.xMax == room.xMax) right = cut.xMin;
            }
            return new Vector2Int(left, right);
        }

        internal void AddBoundaries(Transform parent)
        {
            foreach (var cut in removed)
            {
                var boundary = new GameObject("City room boundary") { layer = LayerMask.NameToLayer("World") };
                boundary.transform.SetParent(parent, false);
                boundary.transform.position = cut.center;
                boundary.AddComponent<BoxCollider2D>().size = cut.size;
            }
        }
    }
}
