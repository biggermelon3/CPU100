using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal tutorial dialog (GO "TutorialPopup" under the UI canvas), styled like an
/// OS message box: blue title bar, body text, OK button bottom-right. Builds its
/// own hierarchy at runtime. A fullscreen dim Image with raycastTarget=true blocks
/// every click behind it; the game clock is stopped by TutorialManager while open.
/// Works at Time.timeScale = 0 (UI events are unscaled).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TutorialPopupUI : MonoBehaviour
{
    public float windowWidth = 560f;

    // Same supersampling trick as SoftwareTooltipUI: glyphs rasterized 3x and the
    // text node scaled down 3x so text stays crisp on scaled/high-DPI canvases.
    const float TextCrisp = 3f;
    const float TitleBarHeight = 36f;
    const float Pad = 18f;
    const float ButtonWidth = 150f;
    const float ButtonHeight = 40f;

    RectTransform dim;
    RectTransform window;
    Text titleText;
    Text bodyText;
    Button okButton;
    System.Action onConfirm;

    public bool IsOpen { get { return dim != null && dim.gameObject.activeSelf; } }

    void Awake()
    {
        // The scene GO may have been created at the canvas corner with zero size;
        // force a clean fullscreen rect so the dim/window anchors mean the canvas.
        var rt = (RectTransform)transform;
        rt.localScale = Vector3.one;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        BuildUI();
        dim.gameObject.SetActive(false);
    }

    void BuildUI()
    {
        // Fullscreen dim that swallows all pointer events behind the dialog.
        dim = CreateRect("Dim", (RectTransform)transform);
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.offsetMin = dim.offsetMax = Vector2.zero;
        Image dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);
        dimImage.raycastTarget = true;

        window = CreateRect("Window", dim);
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.pivot = new Vector2(0.5f, 0.5f);
        Image windowImage = window.gameObject.AddComponent<Image>();
        windowImage.color = new Color(0.93f, 0.94f, 0.96f, 1f);

        RectTransform titleBar = CreateRect("TitleBar", window);
        titleBar.anchorMin = new Vector2(0f, 1f);
        titleBar.anchorMax = new Vector2(1f, 1f);
        titleBar.pivot = new Vector2(0.5f, 1f);
        titleBar.offsetMin = new Vector2(0f, -TitleBarHeight);
        titleBar.offsetMax = new Vector2(0f, 0f);
        Image titleBarImage = titleBar.gameObject.AddComponent<Image>();
        titleBarImage.color = new Color(0.11f, 0.34f, 0.65f, 1f);
        titleBarImage.raycastTarget = false;

        titleText = CreateText("Title", titleBar, 17, FontStyle.Bold, Color.white);
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(0f, 1f);
        titleText.rectTransform.pivot = new Vector2(0f, 1f);
        titleText.rectTransform.sizeDelta = new Vector2((windowWidth - 24f) * TextCrisp, TitleBarHeight * TextCrisp);
        titleText.rectTransform.anchoredPosition = new Vector2(12f, -8f);

        bodyText = CreateText("Body", window, 16, FontStyle.Normal, new Color(0.1f, 0.12f, 0.16f, 1f));
        bodyText.rectTransform.anchorMin = new Vector2(0f, 1f);
        bodyText.rectTransform.anchorMax = new Vector2(0f, 1f);
        bodyText.rectTransform.pivot = new Vector2(0f, 1f);
        bodyText.rectTransform.sizeDelta = new Vector2((windowWidth - Pad * 2f) * TextCrisp, 100f * TextCrisp);
        bodyText.rectTransform.anchoredPosition = new Vector2(Pad, -(TitleBarHeight + Pad));
        bodyText.lineSpacing = 1.15f;

        okButton = CreateOkButton();
    }

    Button CreateOkButton()
    {
        RectTransform buttonRect = CreateRect("OkButton", window);
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        buttonRect.anchoredPosition = new Vector2(-Pad, Pad * 0.8f);

        Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.16f, 0.45f, 0.8f, 1f);

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        button.colors = colors;
        button.onClick.AddListener(HandleOkClicked);

        Text label = CreateText("Label", buttonRect, 16, FontStyle.Bold, Color.white);
        label.alignment = TextAnchor.MiddleCenter;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(ButtonWidth * TextCrisp, ButtonHeight * TextCrisp);
        label.rectTransform.anchoredPosition = Vector2.zero;
        label.text = "OK";
        return button;
    }

    static RectTransform CreateRect(string childName, RectTransform parent)
    {
        var go = new GameObject(childName, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    Text CreateText(string childName, RectTransform parent, int size, FontStyle style, Color color)
    {
        RectTransform rt = CreateRect(childName, parent);
        rt.localScale = Vector3.one / TextCrisp;
        var text = rt.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = Mathf.RoundToInt(size * TextCrisp);
        text.fontStyle = style;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    public void Show(string title, string body, System.Action confirmCallback)
    {
        onConfirm = confirmCallback;
        titleText.text = title;
        bodyText.text = body;

        float bodyH = Mathf.Max(24f, bodyText.preferredHeight / TextCrisp);
        bodyText.rectTransform.sizeDelta = new Vector2((windowWidth - Pad * 2f) * TextCrisp, bodyH * TextCrisp);
        float windowHeight = TitleBarHeight + Pad + bodyH + Pad + ButtonHeight + Pad;
        window.sizeDelta = new Vector2(windowWidth, windowHeight);

        transform.SetAsLastSibling(); // above every other UI element, ghost cursor included
        dim.gameObject.SetActive(true);
    }

    public void HideImmediate()
    {
        onConfirm = null;
        if (dim != null) dim.gameObject.SetActive(false);
    }

    void HandleOkClicked()
    {
        dim.gameObject.SetActive(false);
        System.Action callback = onConfirm;
        onConfirm = null;
        callback?.Invoke();
    }
}
