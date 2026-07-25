using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hover tooltip for the taskbar software slots (GO "SoftwareTooltip" under the UI
/// canvas). Builds its own panel at runtime, jam-style. TaskbarSlotUI calls
/// Show/HideIfOwner on pointer enter/exit. Shows the software description plus every
/// CPU price: per-use cost, launch cost, running load, close relief and cooldown.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SoftwareTooltipUI : MonoBehaviour
{
    public GameStateManager gameState;    // fallback find; tooltip hides off-Play
    public float width = 280f;

    RectTransform panel;
    Text titleText;
    Text bodyText;
    TaskbarSlotUI owner;

    const float Pad = 10f;
    const float TitleHeight = 20f;
    // Glyphs are rasterized at fontSize then magnified by the CanvasScaler (~1.5x on
    // high-DPI screens), which blurs small text. Render them 3x and scale the text
    // node down 3x: same on-screen size, triple the pixel density -> crisp.
    const float TextCrisp = 3f;

    void Awake()
    {
        if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
        // The scene GO was created with world scale 1 under the scaled canvas, which
        // shrank the tooltip below the rest of the UI. Force clean local transform.
        transform.localScale = Vector3.one;
        BuildPanel();
        panel.gameObject.SetActive(false);
    }

    void Update()
    {
        // Result screens: never leave a stale tooltip floating over the end card.
        if (owner != null && gameState != null && gameState.State != GameState.Playing)
            Hide();
    }

    void BuildPanel()
    {
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel = (RectTransform)panelGo.transform;
        panel.SetParent(transform, false);
        panel.pivot = new Vector2(0.5f, 0f);

        Image bg = panelGo.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.1f, 0.95f);
        bg.raycastTarget = false;   // must never steal the pointer from the slot

        titleText = CreateText("Title", 15, FontStyle.Bold, Color.white);
        titleText.rectTransform.sizeDelta = new Vector2((width - Pad * 2f) * TextCrisp, TitleHeight * TextCrisp);
        titleText.rectTransform.anchoredPosition = new Vector2(Pad, -Pad);

        bodyText = CreateText("Body", 13, FontStyle.Normal, new Color(0.78f, 0.84f, 0.9f, 1f));
        bodyText.rectTransform.sizeDelta = new Vector2((width - Pad * 2f) * TextCrisp, 100f * TextCrisp);
        bodyText.rectTransform.anchoredPosition = new Vector2(Pad, -(Pad + TitleHeight + 4f));
    }

    Text CreateText(string childName, int size, FontStyle style, Color color)
    {
        var go = new GameObject(childName, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(panel, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);

        rt.localScale = Vector3.one / TextCrisp;

        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = Mathf.RoundToInt(size * TextCrisp);
        text.fontStyle = style;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    public void Show(TaskbarSlotUI slot, SoftwareRuntimeItem item)
    {
        if (slot == null || item == null || item.Data == null) return;
        if (gameState != null && gameState.State != GameState.Playing) return;

        owner = slot;
        SoftwareData d = item.Data;
        titleText.text = string.IsNullOrEmpty(d.softwareName) ? "Software" : d.softwareName;
        bodyText.text = BuildBody(d);
        LayoutAndPlace((RectTransform)slot.transform);
        panel.gameObject.SetActive(true);
    }

    public void HideIfOwner(TaskbarSlotUI slot)
    {
        if (owner == slot) Hide();
    }

    public void Hide()
    {
        owner = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    static string BuildBody(SoftwareData d)
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(d.description))
        {
            sb.Append(d.description);
            sb.Append("\n\n");
        }

        bool passive = d.abilityType == SoftwareAbilityType.DoubleJump ||
                       d.abilityType == SoftwareAbilityType.None;
        if (passive)
            sb.Append("Passive - always on while running\n");
        else
            sb.Append("Use [E]: +").Append(Num(d.usageCpuCost)).Append(" CPU per use\n");

        sb.Append("Launch: +").Append(Num(d.startupCpuCost)).Append(" CPU   Running: +")
          .Append(Num(d.runningCpuLoadPerSecond)).Append(" CPU/s\n");
        sb.Append("Close: -").Append(Num(d.closeCpuRelief)).Append(" CPU");
        if (!passive && d.cooldown > 0f)
            sb.Append("   Cooldown: ").Append(Num(d.cooldown)).Append("s");
        return sb.ToString();
    }

    static string Num(float v)
    {
        return v.ToString("0.#");
    }

    void LayoutAndPlace(RectTransform slotRect)
    {
        // Text.preferredHeight measures in the text's own (3x) units against the rect
        // width set in BuildPanel; divide back into display units for the panel.
        float bodyH = Mathf.Max(16f, bodyText.preferredHeight / TextCrisp);
        bodyText.rectTransform.sizeDelta = new Vector2((width - Pad * 2f) * TextCrisp, bodyH * TextCrisp);
        panel.sizeDelta = new Vector2(width, Pad + TitleHeight + 4f + bodyH + Pad);

        // Overlay canvas: world corners are screen pixels. Sit just above the slot,
        // rounded to whole pixels so the bitmap glyphs stay sharp.
        Vector3[] corners = new Vector3[4];
        slotRect.GetWorldCorners(corners);
        Vector3 pos = (corners[1] + corners[2]) * 0.5f + new Vector3(0f, 8f, 0f);
        float halfW = width * 0.5f * panel.lossyScale.x;
        pos.x = Mathf.Clamp(pos.x, halfW + 4f, Screen.width - halfW - 4f);
        pos.x = Mathf.Round(pos.x);
        pos.y = Mathf.Round(pos.y);
        panel.position = pos;
    }
}
