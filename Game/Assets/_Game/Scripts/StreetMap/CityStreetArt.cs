using System;
using UnityEngine;

namespace Game.StreetMap
{
    internal sealed class CityStreetArt : MonoBehaviour
    {
        private const int PixelsPerCell = 10;
        private Texture2D atlas;
        private Sprite sprite;

        internal void Build(CityRoomContours contours, int district = 0, bool rooftop = false)
        {
            const int left = -18, bottom = -19, width = 128, height = 66;
            atlas = new Texture2D(width * PixelsPerCell, height * PixelsPerCell, TextureFormat.RGBA32, false)
            {
                name = "City streets and masonry",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[atlas.width * atlas.height];
            for (int row = 0; row < height; ++row)
            for (int column = 0; column < width; ++column)
            {
                var cell = new Vector2Int(left + column, bottom + row);
                bool road = Walkable(cell);
                bool border = district > 0 && !road && !rooftop &&
                    (Walkable(cell + Vector2Int.up) || Walkable(cell + Vector2Int.down) ||
                    Walkable(cell + Vector2Int.left) || Walkable(cell + Vector2Int.right));
                for (int localY = 0; localY < PixelsPerCell; ++localY)
                for (int localX = 0; localX < PixelsPerCell; ++localX)
                {
                    int x = column * PixelsPerCell + localX, y = row * PixelsPerCell + localY;
                    pixels[y * atlas.width + x] = district == 0
                        ? (road ? Asphalt(cell, localX, localY) : Building(cell, x, y))
                        : district == 3 ? Sewer(x, y, road, border) : Interior(cell, x, y, road, border, district, rooftop);
                }
            }
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
            sprite = Sprite.Create(atlas, new Rect(0, 0, atlas.width, atlas.height), Vector2.zero, PixelsPerCell);
            transform.position = new Vector3(left, bottom, 0);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -9;

            bool Walkable(Vector2Int cell) => contours?.IsWalkable(cell) ?? StreetMapLayout.IsWalkable(cell);
        }

        private static Color32 Sewer(int x, int y, bool walkable, bool border)
        {
            if (border) return x % 10 is 1 or 8 || y % 10 is 1 or 8
                ? new Color32(78, 94, 89, 255) : new Color32(37, 45, 42, 255);
            if (!walkable) return new Color32(15, 22, 20, 255);
            int groove = (x + y / 20 * 7) % 40;
            if (x % 10 == 0 || y % 10 == 0) return new Color32(49, 58, 54, 255);
            return groove < 12 ? new Color32(47, 88, 74, 255) : new Color32(73, 86, 72, 255);
        }

        private static Color32 Interior(Vector2Int cell, int x, int y, bool walkable, bool border, int district, bool rooftop)
        {
            if (!walkable)
            {
                if (rooftop) return Building(cell, x, y);
                bool exterior = cell.x < -7 || cell.x >= 97 || cell.y < -8 || cell.y >= (cell.x < 61 ? 32 : 36);
                if (border && exterior)
                {
                    bool rim = x % 10 is 0 or 9 || y % 10 is 0 or 9;
                    if (rim) return new Color32(47, 61, 65, 255);
                    if (district == 1) return new Color32(105, 112, 105, 255);
                    return (x + y) % 15 is 0 or 1
                        ? new Color32(114, 152, 165, 255) : new Color32(67, 106, 127, 255);
                }
                if (exterior) return Exterior(x, y, cell.x < -7 || cell.x >= 97, district);
                if (border && (x % 10 == 0 || y % 10 == 0)) return new Color32(58, 72, 72, 255);
                return district == 1 ? MarketAisles(x, y) : OfficeDepartments(x, y);
            }
            bool joint = x % PixelsPerCell == 0 || y % PixelsPerCell == 0;
            if (joint) return district == 1 ? new Color32(79, 88, 81, 255) : new Color32(46, 55, 69, 255);
            bool alternate = (cell.x + cell.y) % 2 == 0;
            return district == 1 ? (alternate ? new Color32(120, 127, 109, 255) : new Color32(110, 117, 102, 255))
                : (alternate ? new Color32(73, 82, 98, 255) : new Color32(65, 73, 86, 255));
        }

        private static Color32 MarketAisles(int x, int y)
        {
            int shelfX = x % 44, shelfY = y % 64;
            if (shelfX is >= 8 and <= 22 && shelfY is >= 6 and <= 50)
            {
                if (shelfX is 8 or 15 or 22 || shelfY % 10 < 2) return new Color32(65, 62, 42, 255);
                return ((x / 8 + y / 10) & 3) switch
                {
                    0 => new Color32(145, 89, 61, 255),
                    1 => new Color32(147, 121, 75, 255),
                    2 => new Color32(100, 129, 76, 255),
                    _ => new Color32(96, 124, 138, 255)
                };
            }
            return x % 10 == 0 || y % 10 == 0
                ? new Color32(67, 77, 69, 255) : new Color32(89, 100, 85, 255);
        }

        private static Color32 OfficeDepartments(int x, int y)
        {
            int bayX = x % 58, bayY = y % 49;
            if (bayX < 37 && bayY is 4 or 5) return new Color32(88, 112, 119, 255);
            if (bayX is > 3 and < 34 && bayY is > 6 and < 17)
                return (bayX is > 9 and < 17 or > 23 and < 31) && bayY < 12
                    ? new Color32(39, 61, 69, 255) : new Color32(112, 98, 77, 255);
            if (bayY is > 18 and < 24 && bayX is > 10 and < 16 or > 24 and < 30)
                return new Color32(63, 86, 97, 255);
            return x % 10 == 0 || y % 10 == 0
                ? new Color32(46, 56, 67, 255) : new Color32(62, 74, 88, 255);
        }

        private static Color32 Exterior(int x, int y, bool alongSide, int district)
        {
            int along = alongSide ? y : x;
            int across = alongSide ? x : y;
            int row = across % 92, bay = along % 56;
            if (row >= 57)
            {
                int dx = bay - 25, dy = row - 73;
                if (dx * dx + dy * dy < 123)
                    return dx * dx + dy * dy < 70 ? new Color32(77, 103, 61, 255) : new Color32(48, 76, 49, 255);
                if (district == 2 && bay is > 44 and < 51 && row is > 60 and < 72)
                    return new Color32(56, 83, 77, 255);
                return row == 57 ? new Color32(123, 124, 109, 255) : new Color32(53, 75, 53, 255);
            }
            if (bay is >= 11 and <= 35 && row is >= 8 and <= 24 && along / 56 % 4 != 1)
            {
                if (row is 8 or 24) return new Color32(36, 44, 47, 255);
                if (bay is >= 23 and <= 28) return new Color32(47, 65, 76, 255);
                return along / 56 % 3 == 1 ? new Color32(151, 115, 77, 255) : new Color32(105, 134, 144, 255);
            }
            if (bay == 0 && row < 34 || row == 43 && along % 25 < 12)
                return new Color32(143, 141, 111, 255);
            return new Color32(65, 70, 73, 255);
        }

        private static Color32 Asphalt(Vector2Int cell, int x, int y)
        {
            bool curb = x < 3 && !StreetMapLayout.IsWalkable(cell + Vector2Int.left) ||
                x > 6 && !StreetMapLayout.IsWalkable(cell + Vector2Int.right) ||
                y < 3 && !StreetMapLayout.IsWalkable(cell + Vector2Int.down) ||
                y > 6 && !StreetMapLayout.IsWalkable(cell + Vector2Int.up);
            int impression = Math.Abs((cell.x * 23 + cell.y * 37 + x * 13 + y * 19) % 17);
            if (curb) return new Color32((byte)(78 + impression), (byte)(77 + impression), (byte)(67 + impression), 255);
            bool stoneSeam = y % 5 == 0 || (x + (y < 5 ? 0 : 3)) % 5 == 0;
            byte tone = (byte)((stoneSeam ? 45 : 51) + impression / 2);
            return new Color32(tone, (byte)(tone + 5), (byte)(tone + 6), 255);
        }

        private static Color32 Building(Vector2Int cell, int x, int y)
        {
            int blockX = x % 96, blockY = y % 73;
            bool alley = blockX < 9 || blockY < 8;
            if (alley) return new Color32(30, 36, 37, 255);
            bool molding = blockY is 10 or 11 or 69 or 70 || blockX is 11 or 12 or 92 or 93;
            if (molding) return new Color32(109, 101, 82, 255);
            bool windowBand = blockY is >= 17 and <= 29 or >= 45 and <= 58;
            int windowX = (blockX - 13) % 20;
            if (windowBand && windowX is >= 5 and <= 14)
            {
                bool rim = windowX == 5 || windowX == 14 || blockY is 17 or 29 or 45 or 58;
                if (rim) return new Color32(141, 124, 91, 255);
                bool lit = (x / 20 + y / 27) % 5 == 0;
                return lit ? new Color32(149, 111, 61, 255) : new Color32(35, 52, 56, 255);
            }
            bool joint = y % 5 == 0 || (x + 3 * (y / 5 % 2)) % 8 == 0;
            int district = Math.Abs((cell.x / 9 + cell.y / 7) % 3);
            int shade = joint ? 9 : 0;
            return new Color32((byte)(89 + 8 * district - shade), (byte)(81 + 4 * district - shade),
                (byte)(70 + 5 * district - shade), 255);
        }

        private void OnDestroy()
        {
            if (sprite != null) Destroy(sprite);
            if (atlas != null) Destroy(atlas);
        }
    }
}
