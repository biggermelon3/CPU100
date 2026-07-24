using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural placeholder art for the whole prototype (contract §5.20).
/// Pure static factory: every sprite is generated once, cached in static
/// dictionaries, and marked HideFlags.DontSave (textures AND sprites) so
/// nothing leaks into saved scenes or assets. Works in edit mode and play
/// mode. Never returns null. Cache reads re-validate against Unity's
/// overloaded null so destroyed sprites (scene reload / play-mode
/// transitions) are transparently rebuilt.
/// </summary>
public static class PlaceholderSpriteFactory
{
    static readonly Dictionary<DesktopIconType, Sprite> IconCache = new Dictionary<DesktopIconType, Sprite>();
    static readonly Dictionary<int, Sprite> SolidCache = new Dictionary<int, Sprite>();
    static readonly Dictionary<string, Sprite> MiscCache = new Dictionary<string, Sprite>();

    static readonly Color32 White = new Color32(255, 255, 255, 255);

    // ------------------------------------------------------------------ public API

    public static Sprite GetIconSprite(DesktopIconType type)
    {
        Sprite s;
        if (IconCache.TryGetValue(type, out s) && s != null) return s;
        s = BuildIcon(type);
        IconCache[type] = s;
        return s;
    }

    public static Sprite GetShortcutArrow()
    {
        Sprite s;
        if (MiscCache.TryGetValue("arrow", out s) && s != null) return s;
        s = BuildShortcutArrow();
        MiscCache["arrow"] = s;
        return s;
    }

    public static Sprite GetSolid(Color color)
    {
        Color32 c = color;
        int key = (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
        Sprite s;
        if (SolidCache.TryGetValue(key, out s) && s != null) return s;

        var px = new Color32[8 * 8];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        s = MakeSprite(px, 8, 8, 8f, "Solid_" + key.ToString("X8"));
        SolidCache[key] = s;
        return s;
    }

    public static Sprite GetNoise()
    {
        Sprite s;
        if (MiscCache.TryGetValue("noise", out s) && s != null) return s;
        s = BuildNoise();
        MiscCache["noise"] = s;
        return s;
    }

    public static Sprite GetRing()
    {
        Sprite s;
        if (MiscCache.TryGetValue("ring", out s) && s != null) return s;
        s = BuildRing();
        MiscCache["ring"] = s;
        return s;
    }

    public static Sprite GetCursor()
    {
        Sprite s;
        if (MiscCache.TryGetValue("cursor", out s) && s != null) return s;
        s = BuildCursor();
        MiscCache["cursor"] = s;
        return s;
    }

    public static Sprite GetPlayerSprite()
    {
        Sprite s;
        if (MiscCache.TryGetValue("player", out s) && s != null) return s;
        s = BuildPlayer();
        MiscCache["player"] = s;
        return s;
    }

    public static Sprite GetWallpaper()
    {
        Sprite s;
        if (MiscCache.TryGetValue("wallpaper", out s) && s != null) return s;
        s = BuildWallpaper();
        MiscCache["wallpaper"] = s;
        return s;
    }

    public static Color IconColor(DesktopIconType type)
    {
        switch (type)
        {
            case DesktopIconType.Folder:      return C(0xE8, 0xB8, 0x4B);
            case DesktopIconType.TextFile:    return C(0xEC, 0xEC, 0xEC);
            case DesktopIconType.ImageFile:   return C(0x7F, 0xC9, 0x7F);
            case DesktopIconType.Shortcut:    return C(0x6F, 0xA8, 0xDC);
            case DesktopIconType.Software:    return C(0x5B, 0x9B, 0xD5);
            case DesktopIconType.Virus:       return C(0xD9, 0x53, 0x4F);
            case DesktopIconType.RecycleBin:  return C(0x9A, 0xA5, 0xB1);
            case DesktopIconType.Accelerator: return C(0x7B, 0xD3, 0x89);
            case DesktopIconType.SystemFile:  return C(0xB3, 0x9D, 0xDB);
            default:                          return C(0xAA, 0xAA, 0xAA);
        }
    }

    // ------------------------------------------------------------------ icon composition

    static Sprite BuildIcon(DesktopIconType type)
    {
        const int S = 64;
        var px = new Color32[S * S];
        Color32 baseCol = IconColor(type);
        Color32 border = Mul(baseCol, 0.55f);

        // Rounded square base + 2px darker border.
        FillRoundedRect(px, S, S, 3, 3, 58, 58, 8, border);
        FillRoundedRect(px, S, S, 5, 5, 54, 54, 6, baseCol);

        switch (type)
        {
            case DesktopIconType.Folder:      DrawFolderGlyph(px, baseCol); break;
            case DesktopIconType.TextFile:    DrawTextFileGlyph(px); break;
            case DesktopIconType.ImageFile:   DrawImageFileGlyph(px); break;
            case DesktopIconType.Shortcut:    DrawGlobeGlyph(px, baseCol); break;
            case DesktopIconType.Software:    DrawWindowGlyph(px, baseCol); break;
            case DesktopIconType.Virus:       DrawVirusGlyph(px); break;
            case DesktopIconType.RecycleBin:  DrawBinGlyph(px, baseCol); break;
            case DesktopIconType.Accelerator: DrawRocketGlyph(px); break;
            case DesktopIconType.SystemFile:  DrawGearGlyph(px, baseCol); break;
        }

        return MakeSprite(px, S, S, 64f, "Icon_" + type);
    }

    /// <summary>Gold base, darker folder tab + body with lighter front panel.</summary>
    static void DrawFolderGlyph(Color32[] px, Color32 baseCol)
    {
        const int S = 64;
        Color32 dark = Mul(baseCol, 0.55f);
        Color32 front = Toward(baseCol, White, 0.45f);
        FillRect(px, S, S, 16, 40, 14, 7, dark);   // tab
        FillRect(px, S, S, 14, 18, 36, 24, dark);  // back panel
        FillRect(px, S, S, 17, 20, 30, 17, front); // front panel
    }

    /// <summary>White page with gray text lines.</summary>
    static void DrawTextFileGlyph(Color32[] px)
    {
        const int S = 64;
        Color32 outline = C(0x9A, 0x9A, 0x9A);
        Color32 ink = C(0x8E, 0x8E, 0x8E);
        FillRect(px, S, S, 20, 12, 24, 40, White);
        RectOutline(px, S, S, 20, 12, 24, 40, 1, outline);
        FillRect(px, S, S, 24, 44, 16, 2, ink);
        FillRect(px, S, S, 24, 39, 16, 2, ink);
        FillRect(px, S, S, 24, 34, 16, 2, ink);
        FillRect(px, S, S, 24, 29, 16, 2, ink);
        FillRect(px, S, S, 24, 24, 16, 2, ink);
        FillRect(px, S, S, 24, 19, 10, 2, ink);    // short last line
    }

    /// <summary>White page with sun + two mountains.</summary>
    static void DrawImageFileGlyph(Color32[] px)
    {
        const int S = 64;
        FillRect(px, S, S, 17, 13, 30, 38, White);
        RectOutline(px, S, S, 17, 13, 30, 38, 1, C(0x9A, 0x9A, 0x9A));
        FillCircle(px, S, S, 38, 42, 4, C(0xF5, 0xC5, 0x42));            // sun
        Color32 far = C(0x2F, 0x6B, 0x2F);
        Color32 near = C(0x4E, 0x8B, 0x4E);
        for (int i = 0; i <= 18; i++)                                     // big mountain
            HLine(px, S, S, Mathf.Max(18, 27 - i), Mathf.Min(45, 27 + i), 34 - i, far);
        for (int i = 0; i <= 12; i++)                                     // small mountain in front
            HLine(px, S, S, Mathf.Max(18, 39 - i), Mathf.Min(45, 39 + i), 28 - i, near);
    }

    /// <summary>White globe (circle + meridian cross) with a small dark arrow.</summary>
    static void DrawGlobeGlyph(Color32[] px, Color32 baseCol)
    {
        const int S = 64;
        CircleOutline(px, S, S, 32, 34, 14, 2, White);
        FillRect(px, S, S, 19, 33, 27, 2, White);  // equator
        FillRect(px, S, S, 31, 21, 2, 27, White);  // meridian
        Color32 dark = Mul(baseCol, 0.45f);
        FillRect(px, S, S, 12, 12, 5, 2, dark);    // arrow head (tip lower-left)
        FillRect(px, S, S, 12, 12, 2, 5, dark);
        for (int i = 0; i <= 5; i++)               // arrow shaft
            FillRect(px, S, S, 14 + i, 14 + i, 2, 2, dark);
    }

    /// <summary>App window: white body, dark title bar with buttons, content lines.</summary>
    static void DrawWindowGlyph(Color32[] px, Color32 baseCol)
    {
        const int S = 64;
        Color32 dark = Mul(baseCol, 0.5f);
        Color32 title = C(0x2E, 0x5F, 0x94);
        Color32 lines = C(0xC4, 0xC4, 0xC4);
        FillRect(px, S, S, 15, 17, 34, 26, White);            // body
        FillRect(px, S, S, 15, 43, 34, 7, title);             // title bar
        RectOutline(px, S, S, 14, 16, 36, 35, 1, dark);
        FillRect(px, S, S, 44, 45, 2, 2, C(0xE0, 0x60, 0x50)); // close button
        FillRect(px, S, S, 40, 45, 2, 2, White);
        FillRect(px, S, S, 36, 45, 2, 2, White);
        FillRect(px, S, S, 18, 36, 22, 2, lines);
        FillRect(px, S, S, 18, 31, 26, 2, lines);
        FillRect(px, S, S, 18, 26, 26, 2, lines);
        FillRect(px, S, S, 18, 21, 14, 2, lines);
    }

    /// <summary>Thick jagged dark X with a center blob.</summary>
    static void DrawVirusGlyph(Color32[] px)
    {
        const int S = 64;
        Color32 dark = C(0x4A, 0x10, 0x0E);
        for (int i = -13; i <= 13; i++)
        {
            // Small perpendicular jogs every few steps make the X look jagged.
            int jag = ((i & 3) == 0) ? (((i & 4) == 0) ? 1 : -1) : 0;
            FillRect(px, S, S, 31 + i + jag, 31 + i, 3, 3, dark);
            FillRect(px, S, S, 31 + i + jag, 31 - i, 3, 3, dark);
        }
        FillCircle(px, S, S, 32, 32, 5, dark);
    }

    /// <summary>Trash bin: lid + handle + tapered body with vertical ribs.</summary>
    static void DrawBinGlyph(Color32[] px, Color32 baseCol)
    {
        const int S = 64;
        Color32 dark = Mul(baseCol, 0.5f);
        FillRect(px, S, S, 19, 43, 26, 4, dark);   // lid
        FillRect(px, S, S, 28, 47, 8, 3, dark);    // handle
        for (int y = 17; y <= 41; y++)             // body trapezoid, narrower at bottom
        {
            int halfw = Mathf.RoundToInt(Mathf.Lerp(11f, 8f, (41f - y) / 24f));
            FillRect(px, S, S, 32 - halfw, y, halfw * 2 + 1, 1, dark);
        }
        FillRect(px, S, S, 26, 20, 2, 19, baseCol); // ribs
        FillRect(px, S, S, 31, 20, 2, 19, baseCol);
        FillRect(px, S, S, 36, 20, 2, 19, baseCol);
    }

    /// <summary>White up arrow with a small exhaust flame (rocket-ish).</summary>
    static void DrawRocketGlyph(Color32[] px)
    {
        const int S = 64;
        for (int i = 0; i <= 12; i++)              // arrow head, apex at top
            FillRect(px, S, S, 32 - i, 48 - i, i * 2 + 1, 1, White);
        FillRect(px, S, S, 28, 15, 9, 22, White);  // stem
        Color32 flame = C(0xF2, 0xA0, 0x3C);
        FillRect(px, S, S, 30, 12, 5, 3, flame);
        FillRect(px, S, S, 31, 10, 3, 2, flame);
    }

    /// <summary>Dark gear: ring of teeth + disc + hub hole in base color.</summary>
    static void DrawGearGlyph(Color32[] px, Color32 baseCol)
    {
        const int S = 64;
        Color32 dark = Mul(baseCol, 0.5f);
        FillRect(px, S, S, 29, 43, 7, 6, dark);    // N
        FillRect(px, S, S, 29, 15, 7, 6, dark);    // S
        FillRect(px, S, S, 43, 29, 6, 7, dark);    // E
        FillRect(px, S, S, 15, 29, 6, 7, dark);    // W
        FillRect(px, S, S, 40, 40, 6, 6, dark);    // NE
        FillRect(px, S, S, 18, 40, 6, 6, dark);    // NW
        FillRect(px, S, S, 40, 18, 6, 6, dark);    // SE
        FillRect(px, S, S, 18, 18, 6, 6, dark);    // SW
        FillCircle(px, S, S, 32, 32, 13, dark);
        FillCircle(px, S, S, 32, 32, 5, baseCol);  // hub hole
    }

    // ------------------------------------------------------------------ other sprites

    static Sprite BuildShortcutArrow()
    {
        const int S = 24;
        var px = new Color32[S * S];
        Color32 blue = C(0x2F, 0x6F, 0xC9);
        FillRoundedRect(px, S, S, 0, 0, 24, 24, 4, Mul(blue, 0.6f));
        FillRoundedRect(px, S, S, 1, 1, 22, 22, 3, blue);
        for (int i = 0; i <= 8; i++)               // diagonal shaft, pointing up-right
            FillRect(px, S, S, 5 + i, 5 + i, 2, 2, White);
        FillRect(px, S, S, 10, 16, 8, 2, White);   // arrow head at upper-right
        FillRect(px, S, S, 16, 10, 2, 8, White);
        return MakeSprite(px, S, S, 24f, "ShortcutArrow");
    }

    static Sprite BuildNoise()
    {
        const int S = 64;
        var px = new Color32[S * S];
        var rnd = new System.Random(1337);         // deterministic
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)(100 + rnd.Next(80));
            byte a = (byte)(90 + rnd.Next(130));
            px[i] = new Color32(v, v, v, a);
        }
        return MakeSprite(px, S, S, 64f, "Noise");
    }

    static Sprite BuildRing()
    {
        const int S = 128;
        var px = new Color32[S * S];
        const float center = (S - 1) * 0.5f;
        const float mid = 61.5f;                   // band center → diameter ≈ 1 world unit at PPU 128
        for (int y = 0; y < S; y++)
        {
            float dy = y - center;
            int row = y * S;
            for (int x = 0; x < S; x++)
            {
                float dx = x - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // ~3px solid band with a 1px falloff each side (anti-aliased-ish).
                float cov = Mathf.Clamp01(2f - Mathf.Abs(d - mid));
                if (cov > 0f) px[row + x] = new Color32(255, 255, 255, (byte)(cov * 255f));
            }
        }
        return MakeSprite(px, S, S, 128f, "Ring");
    }

    static Sprite BuildCursor()
    {
        const int S = 32;
        var mask = new bool[S * S];
        // Shape defined top-down (t = rows from top of texture): classic arrow with
        // a straight left edge, diagonal right edge and a short tail.
        for (int t = 2; t <= 20; t++)
        {
            int right = 5 + (t - 2) * 2 / 3;
            int y = S - 1 - t;
            for (int x = 5; x <= right; x++) mask[y * S + x] = true;
        }
        for (int t = 18; t <= 26; t++)             // tail
        {
            int y = S - 1 - t;
            for (int x = 9; x <= 12; x++) mask[y * S + x] = true;
        }

        var px = new Color32[S * S];
        Color32 black = new Color32(15, 15, 20, 255);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                int i = y * S + x;
                if (mask[i]) { px[i] = White; continue; }
                bool edge = false;                 // 1px dilation = black outline
                for (int oy = -1; oy <= 1 && !edge; oy++)
                {
                    for (int ox = -1; ox <= 1 && !edge; ox++)
                    {
                        int nx = x + ox, ny = y + oy;
                        if (nx >= 0 && nx < S && ny >= 0 && ny < S && mask[ny * S + nx]) edge = true;
                    }
                }
                if (edge) px[i] = black;
            }
        }
        // Pivot sits on the arrow tip (top-left area).
        return MakeSprite(px, S, S, 32f, "Cursor", FilterMode.Point, new Vector2(0.15f, 0.9f));
    }

    static Sprite BuildPlayer()
    {
        const int W = 44, H = 56;
        var px = new Color32[W * H];
        Color32 body = C(0x3E, 0xC8, 0xA8);        // friendly teal-green
        Color32 dark = C(0x17, 0x2B, 0x33);
        Color32 shield = C(0x1E, 0x6E, 0x5C);

        // Rounded blob body + 2px darker border.
        FillRoundedRect(px, W, H, 2, 2, 40, 52, 14, Mul(body, 0.55f));
        FillRoundedRect(px, W, H, 4, 4, 36, 48, 12, body);
        FillCircle(px, W, H, 22, 18, 10, Toward(body, White, 0.25f)); // lighter belly

        // Eyes (pupils nudged right — faces right by default, matches FacingRight).
        FillCircle(px, W, H, 14, 37, 5, White);
        FillCircle(px, W, H, 30, 37, 5, White);
        FillCircle(px, W, H, 15, 37, 2, dark);
        FillCircle(px, W, H, 31, 37, 2, dark);

        // Smile.
        FillRect(px, W, H, 18, 28, 9, 2, dark);
        FillRect(px, W, H, 17, 30, 1, 1, dark);
        FillRect(px, W, H, 27, 30, 1, 1, dark);

        // Shield emblem on the belly (dark shield, white inner shield).
        FillRect(px, W, H, 15, 14, 14, 6, shield);
        for (int j = 1; j <= 6; j++)
            FillRect(px, W, H, 15 + j, 14 - j, 14 - j * 2, 1, shield);
        FillRect(px, W, H, 18, 13, 8, 5, White);
        for (int j = 1; j <= 3; j++)
            FillRect(px, W, H, 18 + j, 13 - j, 8 - j * 2, 1, White);

        return MakeSprite(px, W, H, 56f, "PlayerBlob");
    }

    static Sprite BuildWallpaper()
    {
        const int W = 192, H = 108;
        var px = new Color32[W * H];
        Color32 top = C(0x1E, 0x5B, 0x8C);
        Color32 bottom = C(0x15, 0x3F, 0x63);
        for (int y = 0; y < H; y++)
        {
            Color32 col = Toward(bottom, top, y / (float)(H - 1));
            Color32 grid = Toward(col, White, 0.06f);   // faint grid line color
            bool gridRow = (y % 16) == 0;
            int row = y * W;
            for (int x = 0; x < W; x++)
                px[row + x] = (gridRow || (x % 16) == 0) ? grid : col;
        }
        return MakeSprite(px, W, H, 10f, "Wallpaper", FilterMode.Bilinear);
    }

    // ------------------------------------------------------------------ low-level drawing (y = 0 at bottom)

    static void FillRect(Color32[] px, int w, int h, int x, int y, int rw, int rh, Color32 c)
    {
        int x0 = Mathf.Max(0, x), y0 = Mathf.Max(0, y);
        int x1 = Mathf.Min(w, x + rw), y1 = Mathf.Min(h, y + rh);
        for (int yy = y0; yy < y1; yy++)
        {
            int row = yy * w;
            for (int xx = x0; xx < x1; xx++) px[row + xx] = c;
        }
    }

    static void RectOutline(Color32[] px, int w, int h, int x, int y, int rw, int rh, int thickness, Color32 c)
    {
        FillRect(px, w, h, x, y, rw, thickness, c);                  // bottom
        FillRect(px, w, h, x, y + rh - thickness, rw, thickness, c); // top
        FillRect(px, w, h, x, y, thickness, rh, c);                  // left
        FillRect(px, w, h, x + rw - thickness, y, thickness, rh, c); // right
    }

    static void HLine(Color32[] px, int w, int h, int x0, int x1, int y, Color32 c)
    {
        FillRect(px, w, h, Mathf.Min(x0, x1), y, Mathf.Abs(x1 - x0) + 1, 1, c);
    }

    static void VLine(Color32[] px, int w, int h, int x, int y0, int y1, Color32 c)
    {
        FillRect(px, w, h, x, Mathf.Min(y0, y1), 1, Mathf.Abs(y1 - y0) + 1, c);
    }

    static void FillCircle(Color32[] px, int w, int h, int cx, int cy, int r, Color32 c)
    {
        int r2 = r * r + r; // slightly generous → rounder look at small radii
        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(w - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(h - 1, cy + r);
        for (int yy = y0; yy <= y1; yy++)
        {
            int dy = yy - cy;
            int row = yy * w;
            for (int xx = x0; xx <= x1; xx++)
            {
                int dx = xx - cx;
                if (dx * dx + dy * dy <= r2) px[row + xx] = c;
            }
        }
    }

    static void CircleOutline(Color32[] px, int w, int h, int cx, int cy, int r, int thickness, Color32 c)
    {
        int outer2 = r * r + r;
        int ri = Mathf.Max(0, r - thickness);
        int inner2 = ri * ri + ri;
        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(w - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(h - 1, cy + r);
        for (int yy = y0; yy <= y1; yy++)
        {
            int dy = yy - cy;
            int row = yy * w;
            for (int xx = x0; xx <= x1; xx++)
            {
                int dx = xx - cx;
                int d2 = dx * dx + dy * dy;
                if (d2 <= outer2 && d2 > inner2) px[row + xx] = c;
            }
        }
    }

    /// <summary>Rounded rect = two overlapping rects + four corner discs.</summary>
    static void FillRoundedRect(Color32[] px, int w, int h, int x, int y, int rw, int rh, int radius, Color32 c)
    {
        radius = Mathf.Min(radius, Mathf.Min(rw, rh) / 2);
        FillRect(px, w, h, x + radius, y, rw - radius * 2, rh, c);
        FillRect(px, w, h, x, y + radius, rw, rh - radius * 2, c);
        FillCircle(px, w, h, x + radius, y + radius, radius, c);
        FillCircle(px, w, h, x + rw - radius - 1, y + radius, radius, c);
        FillCircle(px, w, h, x + radius, y + rh - radius - 1, radius, c);
        FillCircle(px, w, h, x + rw - radius - 1, y + rh - radius - 1, radius, c);
    }

    // ------------------------------------------------------------------ sprite/texture plumbing

    static Sprite MakeSprite(Color32[] px, int w, int h, float ppu, string name,
                             FilterMode filter = FilterMode.Point, Vector2? pivot = null)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name + "_tex",
            filterMode = filter,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        tex.SetPixels32(px);
        tex.Apply(false, false);

        var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h),
            pivot ?? new Vector2(0.5f, 0.5f), ppu, 0u, SpriteMeshType.FullRect);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    static Color32 C(int r, int g, int b)
    {
        return new Color32((byte)r, (byte)g, (byte)b, 255);
    }

    /// <summary>Scale RGB by f (alpha kept) — used for borders/darker shades.</summary>
    static Color32 Mul(Color32 c, float f)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * f), 0, 255),
            c.a);
    }

    /// <summary>Lerp RGB toward target (alpha of c kept).</summary>
    static Color32 Toward(Color32 c, Color32 target, float t)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(c.r, target.r, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(c.g, target.g, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(c.b, target.b, t)),
            c.a);
    }
}
