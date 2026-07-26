using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot, idempotent builder that authors the main-menu UI hierarchy directly
/// into Assets/CPU100/Scenes/MainMenu.unity so artists can restyle every element in
/// the editor (no runtime UI construction). Re-running it repairs missing objects
/// without duplicating or destroying anything, and re-wires the MainMenuUI refs.
/// Texts use the 3x supersampling trick (fontSize x3, localScale 1/3) so they stay
/// crisp on scaled/high-DPI canvases.
/// </summary>
public static class CPU100MainMenuBuilder
{
    const string ScenePath = "Assets/CPU100/Scenes/MainMenu.unity";
    const string WallpaperPath = "Assets/CPU100/Art/Wallpaper/WallPaper1.png";
    const float TextCrisp = 3f;

    static readonly Color WindowBg = new Color(0.93f, 0.94f, 0.96f, 1f);
    static readonly Color TitleBarBlue = new Color(0.11f, 0.34f, 0.65f, 1f);
    static readonly Color ButtonBlue = new Color(0.16f, 0.45f, 0.8f, 1f);
    static readonly Color MenuButtonBlue = new Color(0.1f, 0.28f, 0.52f, 0.95f);
    static readonly Color TrackGray = new Color(0.16f, 0.22f, 0.32f, 1f);

    [MenuItem("Tools/CPU 100/Build Main Menu Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject uiGo = GameObject.Find("UI");
        if (uiGo == null)
        {
            uiGo = new GameObject("UI", typeof(RectTransform));
        }
        var canvas = GetOrAdd<Canvas>(uiGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = GetOrAdd<CanvasScaler>(uiGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        GetOrAdd<GraphicRaycaster>(uiGo);
        var menu = GetOrAdd<MainMenuUI>(uiGo);
        RectTransform root = (RectTransform)uiGo.transform;

        // ---- Background & shade ----
        Sprite wallpaper = AssetDatabase.LoadAssetAtPath<Sprite>(WallpaperPath);
        Image bg = EnsureImage(root, "Background", wallpaper, Color.white, false);
        StretchFull((RectTransform)bg.transform);

        Image shade = EnsureImage(root, "Shade", null, new Color(0.03f, 0.06f, 0.12f, 0.78f), false);
        RectTransform shadeRect = (RectTransform)shade.transform;
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = new Vector2(0.42f, 1f);
        shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;

        // ---- Title block ----
        Text title = EnsureText(root, "Title", "CPU 100%", 64, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
        PlaceTopLeft(title.rectTransform, new Vector2(0f, 1f), new Vector2(90f, -160f), new Vector2(600f, 90f));

        Text subtitle = EnsureText(root, "Subtitle", "a desktop survival platformer", 20, FontStyle.Normal,
            new Color(0.65f, 0.8f, 1f, 1f), TextAnchor.UpperLeft);
        PlaceTopLeft(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(94f, -238f), new Vector2(600f, 34f));

        // ---- Main buttons ----
        menu.startButton = EnsureMenuButton(root, "StartButton", "START", new Vector2(90f, -360f));
        menu.levelsButton = EnsureMenuButton(root, "LevelsButton", "SELECT LEVEL", new Vector2(90f, -432f));
        menu.quitButton = EnsureMenuButton(root, "QuitButton", "QUIT", new Vector2(90f, -504f));

        // ---- Volume controls ----
        Text volumeLabel = EnsureText(root, "VolumeLabel", "Volume", 16, FontStyle.Normal,
            new Color(0.8f, 0.86f, 0.95f, 1f), TextAnchor.UpperLeft);
        PlaceTopLeft(volumeLabel.rectTransform, new Vector2(0f, 0f), new Vector2(90f, 118f), new Vector2(300f, 26f));

        menu.volumeSlider = EnsureVolumeSlider(root);

        RectTransform muteRect = GetOrCreateRect(root, "MuteButton");
        muteRect.anchorMin = muteRect.anchorMax = Vector2.zero;
        muteRect.pivot = new Vector2(0.5f, 0.5f);
        muteRect.sizeDelta = new Vector2(140f, 38f);
        muteRect.anchoredPosition = new Vector2(370f, 82f);
        Image muteImage = GetOrAdd<Image>(muteRect.gameObject);
        muteImage.color = TrackGray;
        menu.muteButton = EnsureButtonBehaviour(muteRect.gameObject, muteImage);
        menu.muteLabel = EnsureButtonLabel(muteRect, "SOUND: ON", 15);

        // ---- Level select panel (authored inactive) ----
        RectTransform panel = GetOrCreateRect(root, "LevelPanel");
        StretchFull(panel);
        Image dim = GetOrAdd<Image>(panel.gameObject);
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        dim.raycastTarget = true;
        menu.levelPanel = panel.gameObject;

        RectTransform window = GetOrCreateRect(panel, "Window");
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(460f, 380f);
        window.anchoredPosition = Vector2.zero;
        GetOrAdd<Image>(window.gameObject).color = WindowBg;

        RectTransform titleBar = GetOrCreateRect(window, "TitleBar");
        titleBar.anchorMin = new Vector2(0f, 1f);
        titleBar.anchorMax = new Vector2(1f, 1f);
        titleBar.pivot = new Vector2(0.5f, 1f);
        titleBar.offsetMin = new Vector2(0f, -36f);
        titleBar.offsetMax = Vector2.zero;
        Image barImage = GetOrAdd<Image>(titleBar.gameObject);
        barImage.color = TitleBarBlue;
        barImage.raycastTarget = false;

        Text barTitle = EnsureText(titleBar, "BarTitle", "Select Level", 16, FontStyle.Bold, Color.white,
            TextAnchor.UpperLeft);
        PlaceTopLeft(barTitle.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(300f, 26f));

        menu.level1Button = EnsureLevelButton(window, "Level1", "LEVEL 1", new Vector2(0f, 96f));
        menu.level2Button = EnsureLevelButton(window, "Level2", "LEVEL 2", new Vector2(0f, 30f));
        menu.level3Button = EnsureLevelButton(window, "Level3", "LEVEL 3", new Vector2(0f, -36f));

        RectTransform backRect = GetOrCreateRect(window, "BackButton");
        backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.sizeDelta = new Vector2(160f, 40f);
        backRect.anchoredPosition = new Vector2(0f, -140f);
        Image backImage = GetOrAdd<Image>(backRect.gameObject);
        backImage.color = new Color(0.35f, 0.38f, 0.45f, 1f);
        menu.backButton = EnsureButtonBehaviour(backRect.gameObject, backImage);
        EnsureButtonLabel(backRect, "BACK", 15);

        panel.gameObject.SetActive(false);

        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[CPU100] Main menu scene built/repaired at " + ScenePath);
    }

    // ---------------- Element helpers (all idempotent) ----------------

    static Button EnsureMenuButton(RectTransform parent, string goName, string label, Vector2 topLeftPos)
    {
        RectTransform rt = GetOrCreateRect(parent, goName);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(300f, 56f);
        rt.anchoredPosition = topLeftPos;
        Image image = GetOrAdd<Image>(rt.gameObject);
        image.color = MenuButtonBlue;
        Button button = EnsureButtonBehaviour(rt.gameObject, image);
        EnsureButtonLabel(rt, label, 20);
        return button;
    }

    static Button EnsureLevelButton(RectTransform parent, string goName, string label, Vector2 centerPos)
    {
        RectTransform rt = GetOrCreateRect(parent, goName);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 52f);
        rt.anchoredPosition = centerPos;
        Image image = GetOrAdd<Image>(rt.gameObject);
        image.color = ButtonBlue;
        Button button = EnsureButtonBehaviour(rt.gameObject, image);
        EnsureButtonLabel(rt, label, 18);
        return button;
    }

    static Button EnsureButtonBehaviour(GameObject go, Image targetGraphic)
    {
        targetGraphic.raycastTarget = true;
        Button button = GetOrAdd<Button>(go);
        button.targetGraphic = targetGraphic;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;
        return button;
    }

    static Text EnsureButtonLabel(RectTransform buttonRect, string content, int size)
    {
        Text label = EnsureText(buttonRect, "Label", content, size, FontStyle.Bold, Color.white,
            TextAnchor.MiddleCenter);
        RectTransform rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = buttonRect.sizeDelta * TextCrisp;
        rt.anchoredPosition = Vector2.zero;
        return label;
    }

    static Slider EnsureVolumeSlider(RectTransform root)
    {
        RectTransform sliderRect = GetOrCreateRect(root, "VolumeSlider");
        sliderRect.anchorMin = sliderRect.anchorMax = Vector2.zero;
        sliderRect.pivot = new Vector2(0f, 0.5f);
        sliderRect.sizeDelta = new Vector2(260f, 20f);
        sliderRect.anchoredPosition = new Vector2(92f, 92f);

        RectTransform track = GetOrCreateRect(sliderRect, "Track");
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.pivot = new Vector2(0.5f, 0.5f);
        track.offsetMin = new Vector2(0f, -4f);
        track.offsetMax = new Vector2(0f, 4f);
        Image trackImage = GetOrAdd<Image>(track.gameObject);
        trackImage.color = TrackGray;
        trackImage.raycastTarget = false;

        RectTransform fillArea = GetOrCreateRect(sliderRect, "FillArea");
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.offsetMin = new Vector2(0f, -4f);
        fillArea.offsetMax = new Vector2(0f, 4f);

        RectTransform fill = GetOrCreateRect(fillArea, "Fill");
        StretchFull(fill);
        Image fillImage = GetOrAdd<Image>(fill.gameObject);
        fillImage.color = new Color(0.3f, 0.65f, 1f, 1f);
        fillImage.raycastTarget = false;

        RectTransform handleArea = GetOrCreateRect(sliderRect, "HandleArea");
        StretchFull(handleArea);

        RectTransform handle = GetOrCreateRect(handleArea, "Handle");
        handle.sizeDelta = new Vector2(14f, 22f);
        Image handleImage = GetOrAdd<Image>(handle.gameObject);
        handleImage.color = Color.white;
        handleImage.raycastTarget = true;

        Slider slider = GetOrAdd<Slider>(sliderRect.gameObject);
        slider.targetGraphic = handleImage;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }

    // ---------------- Primitive helpers ----------------

    static Image EnsureImage(RectTransform parent, string goName, Sprite sprite, Color color, bool raycast)
    {
        RectTransform rt = GetOrCreateRect(parent, goName);
        Image image = GetOrAdd<Image>(rt.gameObject);
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    static Text EnsureText(RectTransform parent, string goName, string content, int size, FontStyle style,
        Color color, TextAnchor alignment)
    {
        RectTransform rt = GetOrCreateRect(parent, goName);
        rt.localScale = Vector3.one / TextCrisp;
        Text text = GetOrAdd<Text>(rt.gameObject);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = Mathf.RoundToInt(size * TextCrisp);
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static void PlaceTopLeft(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size * TextCrisp;
        rt.anchoredPosition = pos;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static RectTransform GetOrCreateRect(RectTransform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null) return (RectTransform)existing;
        var go = new GameObject(childName, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null) component = go.AddComponent<T>();
        return component;
    }
}
