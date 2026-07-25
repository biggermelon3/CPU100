using UnityEngine;

// File-scope enum per contract §5.21.
public enum PlaceholderVisualKind { Wallpaper, Noise, Solid, Cursor, Ring, Player }

/// <summary>
/// Assigns the matching PlaceholderSpriteFactory sprite to the SpriteRenderer
/// or UnityEngine.UI.Image on this GameObject and applies the tint. Procedural
/// sprites are HideFlags.DontSave and never survive a scene save, so the sprite
/// is ALWAYS re-assigned on Awake/OnEnable. ExecuteAlways so scenes produced by
/// the editor builder show their visuals in edit mode too.
/// </summary>
[ExecuteAlways]
public class PlaceholderVisual : MonoBehaviour
{
    public PlaceholderVisualKind kind;
    public Color tint = Color.white;

    void Awake()
    {
        Apply();
    }

    // Also heals the reference after play-mode transitions / scene reloads
    // where the previously assigned DontSave sprite was destroyed.
    void OnEnable()
    {
        Apply();
    }

    // Public so the scene builder can re-apply after assigning kind/tint
    // (AddComponent runs Awake with default field values under [ExecuteAlways]).
    public void Apply()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // Keep authored/imported art. Only repair missing procedural placeholders,
            // whose DontSave sprites disappear after scene reloads.
            if (sr.sprite == null || (sr.sprite.hideFlags & HideFlags.DontSave) != 0)
                sr.sprite = SpriteForKind(kind);
            sr.color = tint;
            return;
        }

        var image = GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            if (image.sprite == null || (image.sprite.hideFlags & HideFlags.DontSave) != 0)
                image.sprite = SpriteForKind(kind);
            image.color = tint;
        }
    }

    static Sprite SpriteForKind(PlaceholderVisualKind kind)
    {
        switch (kind)
        {
            case PlaceholderVisualKind.Wallpaper: return PlaceholderSpriteFactory.GetWallpaper();
            case PlaceholderVisualKind.Noise:     return PlaceholderSpriteFactory.GetNoise();
            case PlaceholderVisualKind.Cursor:    return PlaceholderSpriteFactory.GetCursor();
            case PlaceholderVisualKind.Ring:      return PlaceholderSpriteFactory.GetRing();
            case PlaceholderVisualKind.Player:    return PlaceholderSpriteFactory.GetPlayerSprite();
            // Solid: a white 8x8 — the renderer/Image tint provides the color.
            default:                              return PlaceholderSpriteFactory.GetSolid(Color.white);
        }
    }
}
