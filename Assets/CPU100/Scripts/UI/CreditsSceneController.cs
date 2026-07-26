using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CreditsSceneController : MonoBehaviour
{
    [Header("Sequence")]
    [SerializeField, Min(0.05f)] float sceneFadeDuration = 0.9f;
    [SerializeField, Min(0f)] float titleHoldDuration = 1.2f;
    [SerializeField, Min(1f)] float scrollSpeed = 72f;

    [Header("Editable Credits")]
    [SerializeField, TextArea(12, 30)]
    string creditsText =
        "CREDITS\n\n" +
        "GAME DESIGN\nYOUR NAME\n\n" +
        "PROGRAMMING\nYOUR NAME\n\n" +
        "PIXEL ART\nYOUR NAME\n\n" +
        "MUSIC & SOUND\nYOUR NAME\n\n\n" +
        "SPECIAL THANKS\nGMTK GAME JAM\n\n\n" +
        "THANK YOU FOR PLAYING";

    RectTransform rollContent;
    CanvasGroup fadeOverlay;
    bool isRolling;

    void Awake()
    {
        EnsureVisuals();
    }

    void Start()
    {
        StartCoroutine(PlayOpening());
    }

    void Update()
    {
        if (isRolling && rollContent != null)
            rollContent.anchoredPosition += Vector2.up * (scrollSpeed * Time.unscaledDeltaTime);

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneFadeLoader.LoadScene("MainMenu");
    }

    public void EnsureVisuals()
    {
        RectTransform canvasRect = GetOrCreateRect("Credits Canvas", transform);
        Stretch(canvasRect);
        Canvas canvas = GetOrAdd<Canvas>(canvasRect.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasRect.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform background = GetOrCreateRect("Background", canvasRect);
        Stretch(background);
        Image backgroundImage = GetOrAdd<Image>(background.gameObject);
        backgroundImage.color = new Color32(3, 12, 16, 255);
        backgroundImage.raycastTarget = false;

        RectTransform viewport = GetOrCreateRect("Credits Viewport", canvasRect);
        Stretch(viewport);
        GetOrAdd<RectMask2D>(viewport.gameObject);

        rollContent = GetOrCreateRect("Roll Content", viewport);
        rollContent.anchorMin = rollContent.anchorMax = new Vector2(0.5f, 0.5f);
        rollContent.pivot = new Vector2(0.5f, 0.5f);
        rollContent.sizeDelta = new Vector2(1400f, 2600f);
        rollContent.anchoredPosition = Vector2.zero;

        RectTransform titleRect = GetOrCreateRect("CPU100% Pixel Title", rollContent);
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(1000f, 240f);
        titleRect.anchoredPosition = new Vector2(0f, 170f);
        MainMenuPixelTitleGraphic title = GetOrAdd<MainMenuPixelTitleGraphic>(titleRect.gameObject);
        title.title = "CPU100%";
        title.pixelSize = 24f;
        title.pixelGap = 3f;
        title.color = new Color32(78, 255, 122, 255);
        title.raycastTarget = false;
        title.SetVerticesDirty();

        RectTransform listRect = GetOrCreateRect("Credits List", rollContent);
        listRect.anchorMin = listRect.anchorMax = new Vector2(0.5f, 0.5f);
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.sizeDelta = new Vector2(1100f, 1500f);
        listRect.anchoredPosition = new Vector2(0f, -650f);
        Text list = GetOrAdd<Text>(listRect.gameObject);
        list.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        list.fontStyle = FontStyle.Bold;
        list.fontSize = 38;
        list.lineSpacing = 1.25f;
        list.alignment = TextAnchor.UpperCenter;
        list.horizontalOverflow = HorizontalWrapMode.Wrap;
        list.verticalOverflow = VerticalWrapMode.Overflow;
        list.color = new Color32(190, 255, 207, 255);
        list.raycastTarget = false;
        list.text = creditsText;

        RectTransform hintRect = GetOrCreateRect("Exit Hint", canvasRect);
        hintRect.anchorMin = hintRect.anchorMax = Vector2.zero;
        hintRect.pivot = Vector2.zero;
        hintRect.sizeDelta = new Vector2(420f, 50f);
        hintRect.anchoredPosition = new Vector2(36f, 28f);
        Text hint = GetOrAdd<Text>(hintRect.gameObject);
        hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontStyle = FontStyle.Bold;
        hint.fontSize = 22;
        hint.alignment = TextAnchor.MiddleLeft;
        hint.color = new Color32(78, 255, 122, 180);
        hint.raycastTarget = false;
        hint.text = "ESC  —  MAIN MENU";

        RectTransform overlayRect = GetOrCreateRect("Opening Fade", canvasRect);
        Stretch(overlayRect);
        overlayRect.SetAsLastSibling();
        Image overlayImage = GetOrAdd<Image>(overlayRect.gameObject);
        overlayImage.color = Color.black;
        overlayImage.raycastTarget = true;
        fadeOverlay = GetOrAdd<CanvasGroup>(overlayRect.gameObject);
        fadeOverlay.alpha = 1f;
    }

    IEnumerator PlayOpening()
    {
        isRolling = false;
        rollContent.anchoredPosition = Vector2.zero;
        fadeOverlay.alpha = 1f;

        float elapsed = 0f;
        while (elapsed < sceneFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.alpha = 1f - Mathf.Clamp01(elapsed / sceneFadeDuration);
            yield return null;
        }

        fadeOverlay.alpha = 0f;
        fadeOverlay.blocksRaycasts = false;
        if (titleHoldDuration > 0f) yield return new WaitForSecondsRealtime(titleHoldDuration);
        isRolling = true;
    }

    static RectTransform GetOrCreateRect(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null) return existing as RectTransform;
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
