using UnityEngine;
using UnityEngine.UI;

/// <summary>Loops a neon-green title from left to right inside the menu screen.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class MainMenuScrollingTitle : MonoBehaviour
{
    public string title = "CPU 100%";
    public float pixelsPerSecond = 105f;
    public Color textColor = new Color(0.25f, 1f, 0.38f, 1f);
    public MainMenuPixelTitleGraphic scrollingText;

    RectTransform viewport;
    RectTransform textRect;
    float textWidth;
    float cycleWidth;

    void Awake()
    {
        EnsureVisuals();
        ResetToLeft();
    }

    void Start()
    {
        RefreshDimensions();
        ResetToLeft();
    }

    void Update()
    {
        if (scrollingText == null) return;

        if (textWidth <= 0f) RefreshDimensions();
        Vector2 position = textRect.anchoredPosition;
        position.x -= pixelsPerSecond * Time.unscaledDeltaTime;

        if (position.x <= -cycleWidth)
            position.x += cycleWidth;

        textRect.anchoredPosition = position;
    }

    public void EnsureVisuals()
    {
        viewport = (RectTransform)transform;

        RectMask2D mask = GetComponent<RectMask2D>();
        if (mask == null) mask = gameObject.AddComponent<RectMask2D>();
        mask.padding = new Vector4(8f, 8f, 8f, 8f);

        Transform existing = transform.Find("PixelScrollingTitle");
        if (existing == null)
        {
            GameObject textObject = new GameObject(
                "PixelScrollingTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(MainMenuPixelTitleGraphic));
            textObject.layer = gameObject.layer;
            textRect = (RectTransform)textObject.transform;
            textRect.SetParent(transform, false);
            scrollingText = textObject.GetComponent<MainMenuPixelTitleGraphic>();
        }
        else
        {
            textRect = (RectTransform)existing;
            scrollingText = existing.GetComponent<MainMenuPixelTitleGraphic>();
        }

        // The first version used a legacy Text graphic. A GameObject can only host
        // one Graphic, so keep that authored node disabled instead of adding the
        // dot-matrix Graphic to it.
        Transform legacyTitle = transform.Find("ScrollingTitle");
        if (legacyTitle != null)
            legacyTitle.gameObject.SetActive(false);

        if (scrollingText == null) return;

        scrollingText.pixelSize = 14f;
        scrollingText.pixelGap = 2f;
        string cycle = title + " ";
        scrollingText.title = cycle + cycle + cycle;
        cycleWidth = scrollingText.GetAdvanceWidth(cycle);
        scrollingText.color = textColor;
        scrollingText.raycastTarget = false;

        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(420f, 112f);
        textRect.localRotation = Quaternion.identity;
        textRect.localScale = Vector3.one;

        RefreshDimensions();
    }

    void RefreshDimensions()
    {
        if (scrollingText == null) return;
        viewport = (RectTransform)transform;
        textRect = scrollingText.rectTransform;
        textWidth = Mathf.Max(1f, scrollingText.PreferredWidth);
        cycleWidth = scrollingText.GetAdvanceWidth(title + " ");
        textRect.sizeDelta = new Vector2(textWidth, textRect.sizeDelta.y);
    }

    void ResetToLeft()
    {
        if (scrollingText == null) return;
        RefreshDimensions();
        textRect.anchoredPosition = Vector2.zero;
    }
}
