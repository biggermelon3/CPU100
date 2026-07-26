using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-shot organizer for the hand-authored main menu.</summary>
public static class CPU100MainMenuOrganizer
{
    const string ScenePath = "Assets/CPU100/Scenes/MainMenu.unity";
    const string ArrowPath = "Assets/CPU100/Art/Main_Menu/Select_Arrow.png";
    const string HoverSfxPath = "Assets/CPU100/Audio/SFX/System_interaction/Hover.mp3";
    const string AutoRunKey = "CPU100.MainMenuOrganizer.AutoRun.20260726.v3";

    [InitializeOnLoadMethod]
    static void OrganizeOpenMainMenuOnce()
    {
        if (Application.isBatchMode || SessionState.GetBool(AutoRunKey, false)) return;
        EditorApplication.delayCall += () =>
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            SessionState.SetBool(AutoRunKey, true);
            OrganizeScene(scene);
        };
    }

    [MenuItem("Tools/CPU 100/Organize Current Main Menu")]
    public static void Organize()
    {
        ConfigureArrowImporter();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        OrganizeScene(scene);
    }

    static void OrganizeScene(Scene scene)
    {
        GameObject uiObject = GameObject.Find("UI");
        if (uiObject == null)
            throw new System.InvalidOperationException("MainMenu scene has no UI root.");

        RectTransform root = (RectTransform)uiObject.transform;
        RectTransform backdrop = EnsureFullScreenGroup(root, "01_Backdrop");
        RectTransform buttons = EnsureFullScreenGroup(root, "02_MainButtons");
        RectTransform audio = EnsureFullScreenGroup(root, "03_AudioControls");
        RectTransform foreground = EnsureFullScreenGroup(root, "04_ForegroundArt");
        RectTransform overlays = EnsureFullScreenGroup(root, "05_Overlays");

        MoveIfPresent(root, backdrop, "Background", "SubBackground", "TitleFrame");
        MoveExisting(root, buttons, "StartButton", "LevelsButton", "QuitButton");
        MoveExisting(root, audio, "VolumeLabel", "VolumeSlider", "MuteButton");
        MoveIfPresent(root, foreground, "Figure");
        MoveExisting(root, overlays, "LevelPanel");

        Sprite arrow = AssetDatabase.LoadAllAssetsAtPath(ArrowPath)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name.EndsWith("_1"));
        if (arrow == null)
            throw new System.InvalidOperationException("Could not find the right-arrow slice in " + ArrowPath);

        ConfigureHover(buttons, "StartButton", arrow);
        ConfigureHover(buttons, "LevelsButton", arrow);
        ConfigureHover(buttons, "QuitButton", arrow);
        ConfigureScrollingTitle(backdrop);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[CPU100] Main menu hierarchy and button hover visuals organized.");
    }

    static void ConfigureArrowImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(ArrowPath) as TextureImporter;
        if (importer == null)
            throw new System.InvalidOperationException("Missing arrow texture importer: " + ArrowPath);

        if (importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();
        }
    }

    static RectTransform EnsureFullScreenGroup(RectTransform root, string name)
    {
        Transform existing = root.Find(name);
        RectTransform group;
        if (existing != null)
        {
            group = (RectTransform)existing;
        }
        else
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = root.gameObject.layer;
            group = (RectTransform)go.transform;
            group.SetParent(root, false);
        }

        group.anchorMin = Vector2.zero;
        group.anchorMax = Vector2.one;
        group.pivot = new Vector2(0.5f, 0.5f);
        group.anchoredPosition = Vector2.zero;
        group.sizeDelta = Vector2.zero;
        group.localRotation = Quaternion.identity;
        group.localScale = Vector3.one;
        return group;
    }

    static void MoveExisting(RectTransform root, RectTransform group, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = root.Find(names[i]);
            if (child == null) child = group.Find(names[i]);
            if (child == null)
                throw new System.InvalidOperationException("Missing MainMenu object: " + names[i]);

            child.SetParent(group, true);
            child.SetSiblingIndex(i);
        }
    }

    static void MoveIfPresent(RectTransform root, RectTransform group, params string[] names)
    {
        int siblingIndex = 0;
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = root.Find(names[i]);
            if (child == null) child = group.Find(names[i]);
            if (child == null) continue;

            child.SetParent(group, true);
            child.SetSiblingIndex(siblingIndex++);
        }
    }

    static void ConfigureHover(RectTransform group, string buttonName, Sprite arrowSprite)
    {
        RectTransform buttonRect = (RectTransform)group.Find(buttonName);
        Button button = buttonRect.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        // Only the button background defines the hit area. Large transparent text
        // sprites otherwise make their entire source canvas clickable.
        Graphic buttonGraphic = buttonRect.GetComponent<Graphic>();
        Graphic[] childGraphics = buttonRect.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < childGraphics.Length; i++)
            childGraphics[i].raycastTarget = childGraphics[i] == buttonGraphic;

        RectTransform arrowRect;
        Transform existingArrow = buttonRect.Find("SelectionArrow");
        if (existingArrow == null)
        {
            GameObject arrowObject = new GameObject("SelectionArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            arrowObject.layer = buttonRect.gameObject.layer;
            arrowRect = (RectTransform)arrowObject.transform;
            arrowRect.SetParent(buttonRect, false);
        }
        else
        {
            arrowRect = (RectTransform)existingArrow;
        }

        Image arrowImage = arrowRect.GetComponent<Image>();
        arrowImage.sprite = arrowSprite;
        arrowImage.preserveAspect = true;
        arrowImage.raycastTarget = false;

        float height = Mathf.Max(28f, buttonRect.rect.height * 0.42f);
        arrowRect.anchorMin = new Vector2(0f, 0.5f);
        arrowRect.anchorMax = new Vector2(0f, 0.5f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-14f, 0f);
        arrowRect.sizeDelta = new Vector2(height * arrowSprite.rect.width / arrowSprite.rect.height, height);
        arrowRect.localRotation = Quaternion.identity;
        arrowRect.localScale = Vector3.one;

        MainMenuButtonHover hover = buttonRect.GetComponent<MainMenuButtonHover>();
        if (hover == null) hover = buttonRect.gameObject.AddComponent<MainMenuButtonHover>();
        hover.hoverBrightness = 0.68f;
        hover.selectionArrow = arrowImage;
        hover.hoverSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(HoverSfxPath);
        hover.hoverSfxVolume = 0.8f;
        arrowImage.gameObject.SetActive(false);
        EditorUtility.SetDirty(hover);
    }

    static void ConfigureScrollingTitle(RectTransform backdrop)
    {
        Transform screen = backdrop.Find("TitleFrame/Screen");
        if (screen == null)
            throw new System.InvalidOperationException("Missing MainMenu object: TitleFrame/Screen");

        MainMenuScrollingTitle scrollingTitle = screen.GetComponent<MainMenuScrollingTitle>();
        if (scrollingTitle == null)
            scrollingTitle = screen.gameObject.AddComponent<MainMenuScrollingTitle>();
        scrollingTitle.title = "CPU 100%";
        scrollingTitle.pixelsPerSecond = 105f;
        scrollingTitle.textColor = new Color(0.25f, 1f, 0.38f, 1f);
        scrollingTitle.EnsureVisuals();
        EditorUtility.SetDirty(scrollingTitle);
    }
}
