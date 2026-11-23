using System;
using UnityEngine;

namespace Game.StreetMap
{
    internal sealed class CityStreetArt : MonoBehaviour
    {
        private const int PixelsPerCell = 10;
        private Texture2D atlas;
        private Sprite sprite;

        internal void Build(int district = 0, bool rooftop = false)
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
                bool road = StreetMapLayout.IsWalkable(cell);
                for (int localY = 0; localY < PixelsPerCell; ++localY)
                for (int localX = 0; localX < PixelsPerCell; ++localX)
                {
                    int x = column * PixelsPerCell + localX, y = row * PixelsPerCell + localY;
                    pixels[y * atlas.width + x] = district == 0
                        ? (road ? Asphalt(cell, localX, localY) : Building(cell, x, y))
                        : Interior(cell, x, y, road, district, rooftop);
                }
            }
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
            sprite = Sprite.Create(atlas, new Rect(0, 0, atlas.width, atlas.height), Vector2.zero, PixelsPerCell);
            transform.position = new Vector3(left, bottom, 0);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -9;
        }

        private static Color32 Interior(Vector2Int cell, int x, int y, bool walkable, int district, bool rooftop)
        {
            if (!walkable)
            {
                if (rooftop) return Building(cell, x, y);
                bool seam = (x + y / 8 % 2 * 4) % 8 == 0 || y % 8 == 0;
                if (district == 1) return seam ? new Color32(40, 51, 49, 255) : new Color32(50, 65, 61, 255);
                return seam ? new Color32(37, 43, 55, 255) : new Color32(55, 62, 77, 255);
            }
            bool joint = x % PixelsPerCell == 0 || y % PixelsPerCell == 0;
            if (joint) return district == 1 ? new Color32(79, 88, 81, 255) : new Color32(46, 55, 69, 255);
            bool alternate = (cell.x + cell.y) % 2 == 0;
            return district == 1 ? (alternate ? new Color32(120, 127, 109, 255) : new Color32(110, 117, 102, 255))
                : (alternate ? new Color32(73, 82, 98, 255) : new Color32(65, 73, 86, 255));
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
