using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The cube in your scene has a built-in "icon" reference that is actually blank
/// (icon: {fileID: 10907, guid: ...0000f00000000000000, type: 0}), so inventory
/// slots come out empty even though an item is there.
///
/// To fix that without shipping any art, this class auto-generates a small colored
/// tile sprite for any item whose icon is missing/null, so a slot is never blank.
/// Drop a real sprite onto ItemData.icon in the inspector to replace the tile later.
/// </summary>
public static class ItemIconDatabase
{
    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    public static Sprite Get(ItemData item)
    {
        if (item == null) return null;
        if (item.icon != null) return item.icon;        // real icon assigned -> use it
        if (!_cache.TryGetValue(item.name, out Sprite cached))
        {
            cached = BuildFallback(item);
            _cache[item.name] = cached;
        }
        return cached;
    }

    private static Sprite BuildFallback(ItemData item)
    {
        Color color = ColorFromHash(item.name);
        int size = 64;
        Color32 bg = new Color32((byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255), 255);
        Color32 edge = new Color32((byte)(color.r * 160), (byte)(color.g * 160), (byte)(color.b * 160), 255);
        Color32[] px = new Color32[size * size];
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            bool border = x < 4 || y < 4 || x >= size - 4 || y >= size - 4;
            px[y * size + x] = border ? edge : bg;
        }

                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "FallbackIcon_" + item.name;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(px);
        tex.Apply(false);

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64);
        sprite.name = "Fallback_" + item.name;
        return sprite;
    }

    private static Color ColorFromHash(string s)
    {
        if (string.IsNullOrEmpty(s)) return new Color(0.5f, 0.6f, 0.7f);
        uint h = 1463143649;
        foreach (char c in s) { h ^= (h << 5) + c; } // FNV-1a-ish
        return new Color(((h >> 0) & 0xff) / 255f,
                         ((h >> 8) & 0xff) / 255f,
                         ((h >> 16) & 0xff) / 255f);
    }
}
