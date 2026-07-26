using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu (scene "MainMenu", component on the UI canvas root). Builds the whole
/// menu at runtime, jam-style: wallpaper background, title, Start / Select Level /
/// Quit buttons, a volume slider and a mute toggle (persisted via PlayerPrefs and
/// applied to AudioListener.volume, which survives scene loads). The level-select
/// panel offers the three levels.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public Sprite backgroundSprite;     // WallPaper1
    public AudioClip menuMusic;         // stage1 loop
    public AudioClip clickSfx;          // Mouse_Click
    public AudioClip startSfx;          // MainMenu/start.wav
    public string level1Scene = "Lvl_01";
    public string level2Scene = "Lvl_02";
    public string level3Scene = "Lvl_03";

    const string VolumePref = "cpu100_volume";
    const string MutePref = "cpu100_muted";
    const float TextCrisp = 3f;

    RectTransform root;
    RectTransform levelPanel;
    Slider volumeSlider;
    Text muteLabel;
    AudioSource audioSource;
    bool muted;
    float volume = 0.8f;

    void Awake()
    {
        root = (RectTransform)transform;
        volume = PlayerPrefs.GetFloat(VolumePref, 0.8f);
        muted = PlayerPrefs.GetInt(MutePref, 0) == 1;
        ApplyAudioSettings();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (menuMusic != null)
        {
            audioSource.clip = menuMusic;
            audioSource.loop = true;
            audioSource.volume = 0.55f;
            audioSource.Play();
        }

        BuildMenu();
    }

    // ---------------- UI construction ----------------

    void BuildMenu()
    {
        Image bg = CreateImage("Background", root, backgroundSprite, Color.white);
        Stretch((RectTransform)bg.transform);

        // Left-side dark gradient strip so text pops against the wallpaper.
        Image strip = CreateImage("Shade", root, null, new Color(0.03f, 0.06f, 0.12f, 0.78f));
        RectTransform stripRect = (RectTransform)strip.transform;
        stripRect.anchorMin = new Vector2(0f, 0f);
        stripRect.anchorMax = new Vector2(0.42f, 1f);
        stripRect.offsetMin = stripRect.offsetMax = Vector2.zero;

        Text title = CreateText("Title", root, 64, FontStyle.Bold, Color.white);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(90f, -160f), new Vector2(600f, 90f));
        title.text = "CPU 100%";

        Text subtitle = CreateText("Subtitle", root, 20, FontStyle.Normal, new Color(0.65f, 0.8f, 1f, 1f));
        Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(94f, -238f), new Vector2(600f, 34f));
        subtitle.text = "a desktop survival platformer";

        CreateMenuButton("StartButton", new Vector2(90f, -360f), "START", HandleStartClicked);
        CreateMenuButton("LevelsButton", new Vector2(90f, -432f), "SELECT LEVEL", HandleLevelsClicked);
        CreateMenuButton("QuitButton", new Vector2(90f, -504f), "QUIT", HandleQuitClicked);

        BuildVolumeControls();
        BuildLevelPanel();
    }

    void BuildVolumeControls()
    {
        Text volumeLabel = CreateText("VolumeLabel", root, 16, FontStyle.Normal, new Color(0.8f, 0.86f, 0.95f, 1f));
        Place(volumeLabel.rectTransform, new Vector2(0f, 0f), new Vector2(90f, 118f), new Vector2(300f, 26f));
        volumeLabel.text = "Volume";

        // Slider track.
        RectTransform sliderRect = CreateRect("VolumeSlider", root);
        sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0f, 0f);
        sliderRect.pivot = new Vector2(0f, 0.5f);
        sliderRect.sizeDelta = new Vector2(260f, 20f);
        sliderRect.anchoredPosition = new Vector2(92f, 92f);

        Image track = CreateImage("Track", sliderRect, null, new Color(0.16f, 0.22f, 0.32f, 1f));
        RectTransform trackRect = (RectTransform)track.transform;
        trackRect.anchorMin = new Vector2(0f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.offsetMin = new Vector2(0f, -4f);
        trackRect.offsetMax = new Vector2(0f, 4f);

        RectTransform fillArea = CreateRect("FillArea", sliderRect);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.offsetMin = new Vector2(0f, -4f);
        fillArea.offsetMax = new Vector2(0f, 4f);
        Image fill = CreateImage("Fill", fillArea, null, new Color(0.3f, 0.65f, 1f, 1f));
        RectTransform fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        RectTransform handleArea = CreateRect("HandleArea", sliderRect);
        Stretch(handleArea);
        Image handle = CreateImage("Handle", handleArea, null, Color.white);
        ((RectTransform)handle.transform).sizeDelta = new Vector2(14f, 22f);

        volumeSlider = sliderRect.gameObject.AddComponent<Slider>();
        volumeSlider.targetGraphic = handle;
        volumeSlider.fillRect = fillRect;
        volumeSlider.handleRect = (RectTransform)handle.transform;
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.value = volume;
        volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);

        Button muteButton = CreateButton("MuteButton", root, new Vector2(370f, 82f), new Vector2(140f, 38f),
            new Color(0.16f, 0.22f, 0.32f, 1f), HandleMuteClicked);
        ((RectTransform)muteButton.transform).anchorMin = Vector2.zero;
        ((RectTransform)muteButton.transform).anchorMax = Vector2.zero;
        muteLabel = CreateButtonLabel(muteButton, 15);
        RefreshMuteLabel();
    }

    void BuildLevelPanel()
    {
        levelPanel = CreateRect("LevelPanel", root);
        Stretch(levelPanel);
        Image dim = levelPanel.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        dim.raycastTarget = true;

        Image window = CreateImage("Window", levelPanel, null, new Color(0.93f, 0.94f, 0.96f, 1f));
        RectTransform windowRect = (RectTransform)window.transform;
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(460f, 380f);

        Image bar = CreateImage("TitleBar", windowRect, null, new Color(0.11f, 0.34f, 0.65f, 1f));
        RectTransform barRect = (RectTransform)bar.transform;
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.offsetMin = new Vector2(0f, -36f);
        barRect.offsetMax = Vector2.zero;

        Text barTitle = CreateText("BarTitle", barRect, 16, FontStyle.Bold, Color.white);
        Place(barTitle.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(300f, 26f));
        barTitle.text = "Select Level";

        string[] labels = { "LEVEL 1", "LEVEL 2", "LEVEL 3" };
        string[] scenes = { level1Scene, level2Scene, level3Scene };
        for (int i = 0; i < 3; i++)
        {
            string scene = scenes[i];
            Button b = CreateButton("Level" + (i + 1), windowRect, new Vector2(0f, 96f - i * 66f), new Vector2(300f, 52f),
                new Color(0.16f, 0.45f, 0.8f, 1f), () => LoadLevel(scene));
            RectTransform brt = (RectTransform)b.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            Text lbl = CreateButtonLabel(b, 18);
            lbl.text = labels[i];
        }

        Button back = CreateButton("BackButton", windowRect, new Vector2(0f, -140f), new Vector2(160f, 40f),
            new Color(0.35f, 0.38f, 0.45f, 1f), HandleBackClicked);
        RectTransform backRect = (RectTransform)back.transform;
        backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        Text backLbl = CreateButtonLabel(back, 15);
        backLbl.text = "BACK";

        levelPanel.gameObject.SetActive(false);
    }

    void CreateMenuButton(string goName, Vector2 topLeftPos, string label, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(goName, root, topLeftPos, new Vector2(300f, 56f),
            new Color(0.1f, 0.28f, 0.52f, 0.95f), action);
        RectTransform rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        Text lbl = CreateButtonLabel(button, 20);
        lbl.text = label;
        lbl.alignment = TextAnchor.MiddleCenter;
    }

    Button CreateButton(string goName, RectTransform parent, Vector2 pos, Vector2 size, Color color,
        UnityEngine.Events.UnityAction action)
    {
        RectTransform rt = CreateRect(goName, parent);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;
        button.onClick.AddListener(PlayClick);
        button.onClick.AddListener(action);
        return button;
    }

    Text CreateButtonLabel(Button button, int size)
    {
        Text label = CreateText("Label", (RectTransform)button.transform, size, FontStyle.Bold, Color.white);
        RectTransform rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = ((RectTransform)button.transform).sizeDelta * TextCrisp;
        rt.anchoredPosition = Vector2.zero;
        label.alignment = TextAnchor.MiddleCenter;
        return label;
    }

    static RectTransform CreateRect(string goName, RectTransform parent)
    {
        var go = new GameObject(goName, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    Image CreateImage(string goName, RectTransform parent, Sprite sprite, Color color)
    {
        RectTransform rt = CreateRect(goName, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text CreateText(string goName, RectTransform parent, int size, FontStyle style, Color color)
    {
        RectTransform rt = CreateRect(goName, parent);
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

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size * TextCrisp;
        rt.anchoredPosition = pos;
    }

    // ---------------- Handlers ----------------

    void HandleStartClicked()
    {
        StartCoroutine(LoadAfterSfx(level1Scene, startSfx));
    }

    void HandleLevelsClicked()
    {
        levelPanel.gameObject.SetActive(true);
        levelPanel.SetAsLastSibling();
    }

    void HandleBackClicked()
    {
        levelPanel.gameObject.SetActive(false);
    }

    void HandleQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void LoadLevel(string sceneName)
    {
        StartCoroutine(LoadAfterSfx(sceneName, startSfx));
    }

    IEnumerator LoadAfterSfx(string sceneName, AudioClip sfx)
    {
        if (sfx != null && audioSource != null)
        {
            audioSource.PlayOneShot(sfx, 0.9f);
            yield return new WaitForSecondsRealtime(0.25f);
        }
        SceneManager.LoadScene(sceneName);
    }

    void HandleVolumeChanged(float value)
    {
        volume = value;
        PlayerPrefs.SetFloat(VolumePref, volume);
        ApplyAudioSettings();
    }

    void HandleMuteClicked()
    {
        muted = !muted;
        PlayerPrefs.SetInt(MutePref, muted ? 1 : 0);
        ApplyAudioSettings();
        RefreshMuteLabel();
    }

    void RefreshMuteLabel()
    {
        if (muteLabel != null) muteLabel.text = muted ? "SOUND: OFF" : "SOUND: ON";
    }

    void PlayClick()
    {
        if (audioSource != null && clickSfx != null)
            audioSource.PlayOneShot(clickSfx, 0.8f);
    }

    // AudioListener.volume is a process-wide static, so it carries into every level
    // loaded from here.
    void ApplyAudioSettings()
    {
        AudioListener.volume = muted ? 0f : volume;
    }
}
