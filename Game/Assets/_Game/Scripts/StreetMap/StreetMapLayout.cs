using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Game.StreetMap
{
    // Geometry owned by role 2. Encounter contracts will be supplied by role 1.
    public static class StreetMapLayout
    {
        public const int GeometryVersion = 1;
        public sealed class Room
        {
            public string Id { get; }
            public RectInt Bounds { get; }
            public Vector2 Center => Bounds.center;
            public ReadOnlyCollection<Vector2> SpawnPoints { get; }
            public Room(string id, RectInt bounds, params Vector2[] localPoints)
            {
                Id = id;
                Bounds = bounds;
                var points = new Vector2[localPoints.Length];
                for (int i = 0; i < points.Length; i++) points[i] = Center + localPoints[i];
                SpawnPoints = System.Array.AsReadOnly(points);
            }
        }

        public static readonly ReadOnlyCollection<Room> Rooms = System.Array.AsReadOnly(new[]
        {
            new Room("entry", new RectInt(-7, -6, 14, 12)),
            new Room("combat_a", new RectInt(13, -8, 20, 16), new Vector2(-3, 2), new Vector2(3, 2)),
            new Room("combat_b", new RectInt(13, 16, 20, 16), new Vector2(-3, 2), new Vector2(3, 2)),
            new Room("combat_c", new RectInt(41, 16, 20, 16), new Vector2(-3, 2), new Vector2(3, 2)),
            new Room("elite", new RectInt(69, 12, 28, 24), new Vector2(-7, 4), new Vector2(0, 4), new Vector2(7, 4))
        });
        public static readonly ReadOnlyCollection<RectInt> Corridors = System.Array.AsReadOnly(new[]
        {
            new RectInt(7, -2, 6, 4), new RectInt(21, 8, 4, 8),
            new RectInt(33, 22, 8, 4), new RectInt(61, 22, 8, 4)
        });
        public static Vector2 Entry => Rooms[0].Center;
        public static Rect CameraBounds => new Rect(-8, -9, 106, 46);

        public static bool IsWalkable(Vector2Int cell)
        {
            foreach (var room in Rooms) if (room.Bounds.Contains(cell)) return true;
            foreach (var corridor in Corridors) if (corridor.Contains(cell)) return true;
            return false;
        }

        public static IEnumerable<Vector2Int> WalkableCells()
        {
            for (int y = -8; y < 36; y++)
                for (int x = -7; x < 97; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (IsWalkable(cell)) yield return cell;
                }
        }

        public static bool ContainsHero(Room room, Bounds heroBounds)
        {
            // The whole collider must cross the threshold, leaving clearance for future doors.
            var r = room.Bounds;
            return heroBounds.min.x > r.xMin + .1f && heroBounds.max.x < r.xMax - .1f &&
                heroBounds.min.y > r.yMin + .1f && heroBounds.max.y < r.yMax - .1f;
        }
    }
}
