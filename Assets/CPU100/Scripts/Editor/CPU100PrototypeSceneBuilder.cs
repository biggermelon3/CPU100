using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One-click prototype scene generator for "CPU 100%".
// Menu: Tools/CPU 100/Build Prototype Scene.
// Fully idempotent: running it twice produces the identical scene with zero duplicated
// objects/components/assets. Every lookup is find-by-name-else-create.
public static class CPU100PrototypeSceneBuilder
{
    const string RootFolder = "Assets/CPU100";
    const string ScenePath = RootFolder + "/Scenes/Prototype/CPU100_Prototype.unity";
    const string SoFolder = RootFolder + "/Data/Software";

    // Created-vs-reused bookkeeping for the completion summary.
    static int createdObjects, reusedObjects, createdComponents, createdAssets, reusedAssets;

    // Everything the wiring pass (duty 12) needs, collected while building.
    class Refs
    {
        public Camera camera;
        public SoftwareData browserData, paperPlaneData, shieldData, gameConsoleData, hourglassData;

        public GameStateManager gameState;
        public CPUManager cpuManager;
        public CpuAdaptiveMusic adaptiveMusic;
        public SystemInteractionAudio interactionAudio;
        public SoftwareInventory inventory;
        public SoftwareAbilityExecutor abilityExecutor;
        public InputInterferenceController interference;
        public GlitchBoundsController glitchBounds;
        public Transform acceleratorIcon;

        public Transform temporaryIcons;
        public Transform glitchLeft, glitchRight, glitchTop, glitchBottom;

        public PlayerController2D player;
        public PlayerGroundChecker groundChecker;
        public SoftwareInstallZone installZone;
        public CursorInteractor cursor;

        public SoftwareTaskbarUI taskbar;
        public TaskbarSlotUI[] slots;
        public Text clockText;
        public CPUWindowUI cpuWindow;
        public GameObject warningOverlay;
        public CanvasGroup glitchOverlayGroup;
        public GameResultUI resultUI;
        public GameObject blueScreenPanel, victoryPanel;
        public Button restartButtonBlue, restartButtonWin;
    }

    struct IconSpec
    {
        public string goName, iconName;
        public DesktopIconType type;
        public Vector2 pos;
        public float scale;
        public bool canDrag, canInstall, isShortcut;
        public SoftwareData data;

        public IconSpec(string goName, string iconName, DesktopIconType type, Vector2 pos,
                        float scale, bool canDrag, bool canInstall, bool isShortcut, SoftwareData data)
        {
            this.goName = goName;
            this.iconName = iconName;
            this.type = type;
            this.pos = pos;
            this.scale = scale;
            this.canDrag = canDrag;
            this.canInstall = canInstall;
            this.isShortcut = isShortcut;
            this.data = data;
        }
    }

    [MenuItem("Tools/CPU 100/Build Prototype Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CPU100 Builder] Cannot build the scene while entering/inside Play Mode.");
            return;
        }

        createdObjects = reusedObjects = createdComponents = createdAssets = reusedAssets = 0;

        // Keep the game (and editor play mode) running when the window loses
        // focus — jam builds and MCP-driven testing both want this.
        PlayerSettings.runInBackground = true;

        EnsureFolders();                 // duty 1
        EnsureLayers();                  // duty 2
        if (!EnsureScene())              // duty 3
            return;

        Refs r = new Refs();
        CreateSoftwareDataAssets(r);     // duty 4
        EnsureCamera(r);                 // duty 5
        EnsureGameRoot(r);               // duty 6 (manager hierarchy)
        EnsureDesktopWorld(r);           // duties 6-10 (world, wallpaper, icons, hazards, player, walls)
        EnsureUI(r);                     // duty 11 (canvas + event system)
        WireReferences(r);               // duty 12
        SaveAll();                       // duty 13
    }

    // ------------------------------------------------------------------ duty 1: folders

    static void EnsureFolders()
    {
        EnsureFolder("Assets", "CPU100");
        EnsureFolder(RootFolder, "Scenes");
        EnsureFolder(RootFolder + "/Scenes", "Prototype");
        EnsureFolder(RootFolder, "Scripts");
        EnsureFolder(RootFolder, "Data");
        EnsureFolder(RootFolder + "/Data", "Software");
        EnsureFolder(RootFolder, "Prefabs");
        EnsureFolder(RootFolder + "/Prefabs", "Characters");
        EnsureFolder(RootFolder + "/Prefabs", "Desktop");
        EnsureFolder(RootFolder + "/Prefabs/Desktop", "Icons");
        EnsureFolder(RootFolder + "/Prefabs", "Environment");
        EnsureFolder(RootFolder + "/Prefabs", "Systems");
        EnsureFolder(RootFolder + "/Prefabs", "UI");
        EnsureFolder(RootFolder, "Art");
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    // ------------------------------------------------------------------ duty 2: layers

    static void EnsureLayers()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning("[CPU100 Builder] Could not load TagManager.asset; layers not created.");
            return;
        }

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray)
        {
            Debug.LogWarning("[CPU100 Builder] TagManager 'layers' property not found.");
            return;
        }

        EnsureLayer(layers, "Player");
        EnsureLayer(layers, "Platform");
        tagManager.ApplyModifiedProperties();
    }

    static void EnsureLayer(SerializedProperty layers, string layerName)
    {
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                return; // already present
        }
        // First empty user slot (index >= 8).
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = layerName;
                return;
            }
        }
        Debug.LogWarning("[CPU100 Builder] No free layer slot for '" + layerName + "'.");
    }

    // ------------------------------------------------------------------ duty 3: scene

    static bool EnsureScene()
    {
        bool sceneExists = System.IO.File.Exists(ScenePath) ||
                           AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;

        if (sceneExists)
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[CPU100 Builder] Build cancelled (unsaved scene).");
                    return false;
                }
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }
        else
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[CPU100 Builder] Build cancelled (unsaved scene).");
                return false;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // Register in build settings once.
        EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i].path == ScenePath)
                return true;
        }
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(existing);
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        return true;
    }

    // ------------------------------------------------------------------ duty 4: SoftwareData assets

    static void CreateSoftwareDataAssets(Refs r)
    {
        r.browserData = EnsureSoftwareAsset(SoFolder + "/Browser.asset", "Browser",
            SoftwareAbilityType.SpawnTemporaryIcon, 5f, 1.2f, 6f, 4f);
        r.paperPlaneData = EnsureSoftwareAsset(SoFolder + "/PaperPlane.asset", "Paper Plane",
            SoftwareAbilityType.AirDash, 12f, 1.2f, 12f, 2.5f);
        r.shieldData = EnsureSoftwareAsset(SoFolder + "/Shield.asset", "Shield",
            SoftwareAbilityType.ShieldPush, 12f, 1.5f, 8f, 15f);
        r.shieldData.description =
            "Temporarily rolls CPU usage back to 5% and freezes it for 5 seconds, " +
            "then restores the CPU level from when it was activated.";
        EditorUtility.SetDirty(r.shieldData);
        r.gameConsoleData = EnsureSoftwareAsset(SoFolder + "/GameConsole.asset", "Game Console",
            SoftwareAbilityType.DoubleJump, 10f, 0.5f, 10f, 0f);
        r.hourglassData = EnsureSoftwareAsset(SoFolder + "/Hourglass.asset", "Hourglass",
            SoftwareAbilityType.CpuSlowdown, 6f, 0.4f, 6f, 12f);
    }

    static SoftwareData EnsureSoftwareAsset(string path, string softwareName,
        SoftwareAbilityType ability, float startupCost, float runningLoad, float closeRelief, float cooldown)
    {
        SoftwareData data = AssetDatabase.LoadAssetAtPath<SoftwareData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<SoftwareData>();
            AssetDatabase.CreateAsset(data, path);
            createdAssets++;
        }
        else
        {
            reusedAssets++;
        }

        // Always (re)assign every field so re-running restores canonical values.
        data.softwareName = softwareName;
        data.icon = null;
        data.abilityType = ability;
        data.sideEffectType = SoftwareSideEffectType.None;
        data.startupCpuCost = startupCost;
        data.runningCpuLoadPerSecond = runningLoad;
        data.closeCpuRelief = closeRelief;
        data.cooldown = cooldown;
        data.canUseRepeatedly = true;
        data.isSpecialSoftware = false;
        EditorUtility.SetDirty(data);
        return data;
    }

    // ------------------------------------------------------------------ duty 5: camera

    static void EnsureCamera(Refs r)
    {
        GameObject go = GetOrCreateRoot("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0f, -10f);

        Camera cam = GetOrAddComponent<Camera>(go);
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#1E5B8C");
        GetOrAddComponent<AudioListener>(go);
        // URP requires the additional camera data component; without it the render
        // pipeline logs an error on entering play mode.
        GetOrAddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(go);

        r.camera = cam;
        Dirty(cam);
    }

    // ------------------------------------------------------------------ duty 6: GameRoot managers

    static void EnsureGameRoot(Refs r)
    {
        GameObject root = GetOrCreateRoot("GameRoot");
        root.transform.position = Vector3.zero;

        r.gameState = GetOrAddComponent<GameStateManager>(GetOrCreateChild(root.transform, "GameStateManager"));
        GameObject cpuManagerObject = GetOrCreateChild(root.transform, "CPUManager");
        r.cpuManager = GetOrAddComponent<CPUManager>(cpuManagerObject);
        r.adaptiveMusic = GetOrAddComponent<CpuAdaptiveMusic>(cpuManagerObject);
        r.adaptiveMusic.cpuManager = r.cpuManager;
        r.adaptiveMusic.baseTrack = AssetDatabase.LoadAssetAtPath<AudioClip>(RootFolder + "/Audio/BGM/stage1.wav");
        r.adaptiveMusic.pulseTrack = AssetDatabase.LoadAssetAtPath<AudioClip>(RootFolder + "/Audio/BGM/stage2.wav");
        r.adaptiveMusic.dangerTrack = AssetDatabase.LoadAssetAtPath<AudioClip>(RootFolder + "/Audio/BGM/stage3.wav");
        r.adaptiveMusic.glitchTrack = AssetDatabase.LoadAssetAtPath<AudioClip>(RootFolder + "/Audio/BGM/stage4.wav");
        r.adaptiveMusic.stageGlitchSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/Character/Glitch_Hit.wav");
        r.adaptiveMusic.hazardGlitchLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/Glitch/SFX_Hazard_Glitch_Noise_Loop.wav");
        r.adaptiveMusic.blueScreenEndingSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/Ending/Bluescreen_NEW.mp3");
        r.adaptiveMusic.repairSuccessSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/Ending/Cute_Happy_Ending.mp3");
        Dirty(r.adaptiveMusic);
        GameObject inventoryObject = GetOrCreateChild(root.transform, "SoftwareInventory");
        r.inventory = GetOrAddComponent<SoftwareInventory>(inventoryObject);
        r.interactionAudio = GetOrAddComponent<SystemInteractionAudio>(inventoryObject);
        r.interactionAudio.deleteSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/System_interaction/delete.wav");
        r.interactionAudio.hoverSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/System_interaction/Hover.mp3");
        r.interactionAudio.installSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/System_interaction/install.wav");
        r.interactionAudio.mouseClickSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/System_interaction/Mouse_Click.mp3");
        r.interactionAudio.pickupSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/System_interaction/Pickup_04.wav");
        Dirty(r.interactionAudio);
        r.abilityExecutor = GetOrAddComponent<SoftwareAbilityExecutor>(GetOrCreateChild(root.transform, "SoftwareAbilityExecutor"));
        r.interference = GetOrAddComponent<InputInterferenceController>(GetOrCreateChild(root.transform, "InputInterferenceController"));
        r.glitchBounds = GetOrAddComponent<GlitchBoundsController>(GetOrCreateChild(root.transform, "GlitchBoundsController"));
    }

    // ------------------------------------------------------------------ duties 6-10: desktop world

    static void EnsureDesktopWorld(Refs r)
    {
        GameObject world = GetOrCreateRoot("DesktopWorld");
        world.transform.position = Vector3.zero;

        // Duty 7: wallpaper.
        GameObject wallpaper = GetOrCreateChild(world.transform, "Wallpaper");
        wallpaper.transform.localPosition = Vector3.zero;
        SpriteRenderer wallpaperSr = GetOrAddComponent<SpriteRenderer>(wallpaper);
        wallpaperSr.sortingOrder = -100;
        Sprite importedWallpaper = LoadLargestSpriteAtPath(
            "Assets/CPU100/Art/Wallpaper/WallPaper1.png");
        if (importedWallpaper != null)
        {
            wallpaperSr.sprite = importedWallpaper;
            float coverScale = Mathf.Max(
                19.2f / Mathf.Max(0.001f, importedWallpaper.bounds.size.x),
                10.8f / Mathf.Max(0.001f, importedWallpaper.bounds.size.y));
            wallpaper.transform.localScale = Vector3.one * coverScale;
        }
        ConfigurePlaceholder(wallpaper, PlaceholderVisualKind.Wallpaper, Color.white);
        Dirty(wallpaperSr);

        // Duty 8: desktop icons.
        GameObject icons = GetOrCreateChild(world.transform, "DesktopIcons");
        icons.transform.localPosition = Vector3.zero;
        BuildIcons(icons.transform, r);

        GameObject temp = GetOrCreateChild(world.transform, "TemporaryIcons");
        temp.transform.localPosition = Vector3.zero;
        r.temporaryIcons = temp.transform;

        // Duty 9: glitch hazard zones (NO GlitchZone relay component - runtime-only).
        GameObject hazards = GetOrCreateChild(world.transform, "Hazards");
        hazards.transform.localPosition = Vector3.zero;
        r.glitchLeft = BuildGlitchZone(hazards.transform, "GlitchLeft", new Vector3(-9.6f, 0f, 0f), new Vector3(0.01f, 10.8f, 1f));
        r.glitchRight = BuildGlitchZone(hazards.transform, "GlitchRight", new Vector3(9.6f, 0f, 0f), new Vector3(0.01f, 10.8f, 1f));
        r.glitchTop = BuildGlitchZone(hazards.transform, "GlitchTop", new Vector3(0f, 5.4f, 0f), new Vector3(19.2f, 0.01f, 1f));
        r.glitchBottom = BuildGlitchZone(hazards.transform, "GlitchBottom", new Vector3(0f, -5.4f, 0f), new Vector3(19.2f, 0.01f, 1f));

        // Duty 10: player.
        BuildPlayer(world.transform, r);

        // Duty 6: screen-edge walls.
        BuildWall(world.transform, "LeftWall", -9.85f);
        BuildWall(world.transform, "RightWall", 9.85f);
    }

    static void BuildIcons(Transform parent, Refs r)
    {
        IconSpec[] specs = new IconSpec[]
        {
            new IconSpec("StartFolder",         "Folder",                DesktopIconType.Folder,      new Vector2(-7.8f, -3.4f), 1.4f, true,  false, false, null),
            new IconSpec("BrowserSoftware",     "Browser.exe",           DesktopIconType.Software,    new Vector2(-6.0f, -3.9f), 1.2f, true,  true,  false, r.browserData),
            new IconSpec("PaperPlaneSoftware",  "Paper Plane.exe",       DesktopIconType.Software,    new Vector2(-2.8f, -3.7f), 1.2f, true,  true,  false, r.paperPlaneData),
            new IconSpec("TextFilePlatform",    "Empty File",            DesktopIconType.TextFile,    new Vector2(-5.0f, -1.8f), 1.3f, false, false, false, null),
            new IconSpec("GameConsoleSoftware", "Game Console.exe",      DesktopIconType.Software,    new Vector2(-2.4f, -0.4f), 1.3f, true,  true,  false, r.gameConsoleData),
            new IconSpec("ErrorFile",           "Error File",            DesktopIconType.ErrorFile,   new Vector2(0.4f, -2.6f),  1.3f, false, false, false, null),
            new IconSpec("HourglassSoftware",   "Hourglass.exe",         DesktopIconType.Software,    new Vector2(0.6f, 1.1f),   1.3f, true,  true,  false, r.hourglassData),
            new IconSpec("SystemFile",          "system32.dll",          DesktopIconType.SystemFile,  new Vector2(2.42f, -3.97f), 1.5f, false, false, false, null),
            new IconSpec("ShieldSoftware",      "Shield.exe",            DesktopIconType.Software,    new Vector2(4.2f, -1.6f),  1.2f, true,  true,  false, r.shieldData),
            new IconSpec("RecycleBin",          "Recycle Bin",           DesktopIconType.RecycleBin,  new Vector2(8.5f, -3.9f),  1.5f, false, false, false, null),
            new IconSpec("AcceleratorShortcut", "System Booster.lnk",    DesktopIconType.Accelerator, new Vector2(7.4f, 2.6f),   1.4f, false, false, true,  null),
        };

        for (int i = 0; i < specs.Length; i++)
        {
            IconSpec spec = specs[i];
            GameObject go = GetOrCreateChild(parent, spec.goName);
            go.transform.localPosition = new Vector3(spec.pos.x, spec.pos.y, 0f);
            go.transform.localScale = Vector3.one * spec.scale;
            if (spec.type == DesktopIconType.Accelerator) r.acceleratorIcon = go.transform;

            DesktopIcon icon = GetOrAddComponent<DesktopIcon>(go);
            icon.iconName = spec.iconName;
            icon.iconType = spec.type;
            icon.isPlatform = true;
            icon.canDrag = spec.canDrag;
            icon.canInstall = spec.canInstall;
            icon.isShortcut = spec.isShortcut;
            icon.softwareData = spec.data;
            icon.iconScale = spec.scale;
            if (spec.type == DesktopIconType.RecycleBin)
                icon.iconSprite = LoadLargestSpriteAtPath(
                    RootFolder + "/Art/Icons/Recycle Bin.png");
            else if (spec.type == DesktopIconType.SystemFile)
                icon.iconSprite = LoadLargestSpriteAtPath(
                    RootFolder + "/Art/Icons/SystemSetting.png");

            try
            {
                icon.EnsureVisuals();
                if ((spec.type == DesktopIconType.RecycleBin ||
                     spec.type == DesktopIconType.SystemFile) &&
                    icon.iconSprite != null)
                {
                    Transform body = go.transform.Find("Body");
                    float largestSide = Mathf.Max(
                        icon.iconSprite.bounds.size.x,
                        icon.iconSprite.bounds.size.y);
                    if (body != null && largestSide > 0.0001f)
                        body.localScale = Vector3.one / largestSide;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CPU100 Builder] EnsureVisuals failed on '" + spec.goName + "': " + e.Message);
            }
            Dirty(icon);
        }
    }

    static Transform BuildGlitchZone(Transform hazards, string zoneName, Vector3 pos, Vector3 scale)
    {
        GameObject go = GetOrCreateChild(hazards, zoneName);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;

        SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(go);
        sr.sortingOrder = 400;
        sr.color = new Color(1f, 1f, 1f, 0.45f);
        ConfigurePlaceholder(go, PlaceholderVisualKind.Noise, new Color(1f, 1f, 1f, 0.45f));

        BoxCollider2D box = GetOrAddComponent<BoxCollider2D>(go);
        box.isTrigger = true;
        box.size = Vector2.one;

        Dirty(sr);
        return go.transform;
    }

    static void BuildPlayer(Transform world, Refs r)
    {
        GameObject player = GetOrCreateChild(world, "Player");
        player.tag = "Player";
        int playerLayer = LayerMask.NameToLayer("Player");
        player.layer = playerLayer >= 0 ? playerLayer : 0;
        player.transform.localPosition = new Vector3(-7.8f, -2.3f, 0f);

        SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(player);
        sr.sortingOrder = 50;
        RuntimeAnimatorController playerController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/CPU100/Animations/Player/Player.controller");
        if (playerController != null)
        {
            PlaceholderVisual placeholder = player.GetComponent<PlaceholderVisual>();
            if (placeholder != null)
                Object.DestroyImmediate(placeholder);
            GameObject visual = GetOrCreateChild(player.transform, "Visual");
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 1.6f;
            SpriteRenderer visualRenderer = GetOrAddComponent<SpriteRenderer>(visual);
            visualRenderer.sortingOrder = 50;
            if (sr != null)
            {
                visualRenderer.sharedMaterials = sr.sharedMaterials;
                Object.DestroyImmediate(sr);
                sr = visualRenderer;
            }
            if (visualRenderer.sprite != null)
            {
                CapsuleCollider2D existingCapsule = player.GetComponent<CapsuleCollider2D>();
                float colliderBottom = existingCapsule != null
                    ? existingCapsule.offset.y - existingCapsule.size.y * 0.5f
                    : -0.4f;
                float spriteBottom = visualRenderer.sprite.bounds.min.y * visual.transform.localScale.y;
                visual.transform.localPosition =
                    new Vector3(0f, colliderBottom - spriteBottom, 0f);
            }
            Animator animator = GetOrAddComponent<Animator>(player);
            animator.runtimeAnimatorController = playerController;
            animator.applyRootMotion = false;
            GetOrAddComponent<PlayerAnimationController>(player);
            Dirty(animator);
        }
        else
        {
            ConfigurePlaceholder(player, PlaceholderVisualKind.Player, Color.white);
        }

        Rigidbody2D rb = GetOrAddComponent<Rigidbody2D>(player);
        rb.gravityScale = 3.5f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        // A sleeping body stops receiving OnTriggerStay2D (virus re-hits, glitch hazard).
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

        CapsuleCollider2D capsule = GetOrAddComponent<CapsuleCollider2D>(player);
        capsule.size = new Vector2(0.55f, 0.8f);
        capsule.direction = CapsuleDirection2D.Vertical;
        Transform playerVisual = player.transform.Find("Visual");
        if (playerVisual != null)
        {
            SpriteRenderer playerVisualRenderer = playerVisual.GetComponent<SpriteRenderer>();
            if (playerVisualRenderer != null && playerVisualRenderer.sprite != null)
            {
                float colliderBottom = capsule.offset.y - capsule.size.y * 0.5f;
                float spriteBottom =
                    playerVisualRenderer.sprite.bounds.min.y * playerVisual.localScale.y;
                playerVisual.localPosition =
                    new Vector3(0f, colliderBottom - spriteBottom, 0f);
            }
        }

        r.player = GetOrAddComponent<PlayerController2D>(player);

        GameObject groundCheck = GetOrCreateChild(player.transform, "GroundCheck");
        groundCheck.transform.localPosition = new Vector3(0f, -0.45f, 0f);
        r.groundChecker = GetOrAddComponent<PlayerGroundChecker>(groundCheck);
        int platformLayer = LayerMask.NameToLayer("Platform");
        r.groundChecker.groundMask = platformLayer >= 0 ? (1 << platformLayer) : 1;
        Dirty(r.groundChecker);

        GameObject installZone = GetOrCreateChild(player.transform, "InstallZone");
        installZone.transform.localPosition = Vector3.zero;
        r.installZone = GetOrAddComponent<SoftwareInstallZone>(installZone);

        GameObject cursor = GetOrCreateChild(player.transform, "VirtualCursor");
        cursor.transform.localPosition = Vector3.zero;
        r.cursor = GetOrAddComponent<CursorInteractor>(cursor);

        Dirty(sr);
        Dirty(rb);
    }

    static void BuildWall(Transform world, string wallName, float x)
    {
        GameObject wall = GetOrCreateChild(world, wallName);
        wall.transform.localPosition = new Vector3(x, 0f, 0f);
        BoxCollider2D box = GetOrAddComponent<BoxCollider2D>(wall);
        box.isTrigger = false;
        box.size = new Vector2(0.5f, 22f);
    }

    // ------------------------------------------------------------------ duty 11: UI

    static void EnsureUI(Refs r)
    {
        GameObject uiRoot = GetOrCreateRoot("UI");
        uiRoot.layer = 5; // built-in UI layer

        Canvas canvas = GetOrAddComponent<Canvas>(uiRoot);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(uiRoot);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GetOrAddComponent<GraphicRaycaster>(uiRoot);

        BuildTaskbar(uiRoot.transform, r);
        BuildCPUWindow(uiRoot.transform, r);
        BuildGlitchOverlay(uiRoot.transform, r);
        BuildWarningOverlay(uiRoot.transform, r);
        BuildResultUI(uiRoot.transform, r);
        EnsureEventSystem();

        Dirty(canvas);
        Dirty(scaler);
    }

    static void BuildTaskbar(Transform canvas, Refs r)
    {
        RectTransform bar = GetOrCreateUIChild(canvas, "DesktopTaskbar");
        SetRect(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 80f));
        // Opaque panel must swallow clicks, or they fall through to world icons.
        ConfigureImage(bar.gameObject, Hex("#1F2430F2"), true);

        // Start button is a decorative placeholder (no Button behaviour in the prototype).
        RectTransform start = GetOrCreateUIChild(bar, "StartButton");
        SetRect(start, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(120f, 64f));
        ConfigureImage(start.gameObject, Hex("#2E3646"), true);
        RectTransform startText = GetOrCreateUIChild(start, "Text");
        SetStretch(startText);
        ConfigureText(startText.gameObject, "Start", 20, Color.white, TextAnchor.MiddleCenter);

        r.slots = new TaskbarSlotUI[3];
        r.slots[0] = BuildTaskbarSlot(bar, "SoftwareSlot01", 150f);
        r.slots[1] = BuildTaskbarSlot(bar, "SoftwareSlot02", 232f);
        r.slots[2] = BuildTaskbarSlot(bar, "SoftwareSlot03", 314f);

        RectTransform clock = GetOrCreateUIChild(bar, "ClockText");
        SetRect(clock, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(110f, 40f));
        r.clockText = ConfigureText(clock.gameObject, "00:00", 20, Color.white, TextAnchor.MiddleRight);

        r.taskbar = GetOrAddComponent<SoftwareTaskbarUI>(bar.gameObject);
    }

    static TaskbarSlotUI BuildTaskbarSlot(Transform bar, string slotName, float x)
    {
        RectTransform root = GetOrCreateUIChild(bar, slotName);
        SetRect(root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(76f, 64f));
        // Slot root receives pointer clicks (TaskbarSlotUI implements IPointerClickHandler).
        ConfigureImage(root.gameObject, Hex("#2A3140"), true);

        RectTransform iconRt = GetOrCreateUIChild(root, "IconImage");
        SetRect(iconRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(44f, 44f));
        Image iconImage = ConfigureImage(iconRt.gameObject, Color.white, false);

        RectTransform nameRt = GetOrCreateUIChild(root, "NameText");
        SetRect(nameRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 16f));
        Text nameText = ConfigureText(nameRt.gameObject, "-", 11, Color.white, TextAnchor.MiddleCenter);

        RectTransform selRt = GetOrCreateUIChild(root, "SelectionHighlight");
        SetStretch(selRt);
        Image selectionHighlight = ConfigureImage(selRt.gameObject, Hex("#FFFFFF2E"), false);
        selRt.gameObject.SetActive(false);

        RectTransform runRt = GetOrCreateUIChild(root, "RunningIndicator");
        SetRect(runRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(48f, 4f));
        Image runningIndicator = ConfigureImage(runRt.gameObject, Hex("#6FE3A0"), false);
        runRt.gameObject.SetActive(false);

        RectTransform maskRt = GetOrCreateUIChild(root, "CooldownMask");
        SetStretch(maskRt);
        Image cooldownMask = ConfigureImage(maskRt.gameObject, Hex("#000000A0"), false);
        cooldownMask.type = Image.Type.Filled;
        cooldownMask.fillMethod = Image.FillMethod.Vertical;
        cooldownMask.fillOrigin = (int)Image.OriginVertical.Bottom;
        cooldownMask.fillAmount = 0f;
        // Filled rendering needs a sprite at runtime; PlaceholderVisual supplies it.
        ConfigurePlaceholder(maskRt.gameObject, PlaceholderVisualKind.Solid, Hex("#000000A0"));

        RectTransform closeRt = GetOrCreateUIChild(root, "CloseButton");
        SetRect(closeRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(18f, 18f));
        Image closeImage = ConfigureImage(closeRt.gameObject, Hex("#C0392B"), true);
        Button closeButton = GetOrAddComponent<Button>(closeRt.gameObject);
        closeButton.targetGraphic = closeImage;
        RectTransform closeText = GetOrCreateUIChild(closeRt, "Text");
        SetStretch(closeText);
        ConfigureText(closeText.gameObject, "X", 12, Color.white, TextAnchor.MiddleCenter);

        TaskbarSlotUI slot = GetOrAddComponent<TaskbarSlotUI>(root.gameObject);
        slot.iconImage = iconImage;
        slot.nameText = nameText;
        slot.selectionHighlight = selectionHighlight;
        slot.runningIndicator = runningIndicator;
        slot.cooldownMask = cooldownMask;
        slot.closeButton = closeButton;
        Dirty(slot);
        return slot;
    }

    static void BuildCPUWindow(Transform canvas, Refs r)
    {
        RectTransform win = GetOrCreateUIChild(canvas, "CPUWindow");
        SetRect(win, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(300f, 160f));
        // Opaque panel must swallow clicks, or they fall through to world icons.
        ConfigureImage(win.gameObject, Hex("#141A24F0"), true);

        Vector2 tl = new Vector2(0f, 1f);

        RectTransform title = GetOrCreateUIChild(win, "TitleText");
        SetRect(title, tl, tl, tl, new Vector2(8f, -6f), new Vector2(284f, 18f));
        ConfigureText(title.gameObject, "Task Manager - CPU", 15, Color.white, TextAnchor.MiddleLeft);

        RectTransform percent = GetOrCreateUIChild(win, "PercentText");
        SetRect(percent, tl, tl, tl, new Vector2(12f, -28f), new Vector2(140f, 40f));
        Text percentText = ConfigureText(percent.gameObject, "0%", 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);

        RectTransform fillBg = GetOrCreateUIChild(win, "FillBarBg");
        SetRect(fillBg, tl, tl, tl, new Vector2(12f, -74f), new Vector2(200f, 14f));
        ConfigureImage(fillBg.gameObject, Hex("#0B0F16"), false);

        RectTransform fillRt = GetOrCreateUIChild(fillBg, "FillBar");
        SetStretch(fillRt);
        Image fillBar = ConfigureImage(fillRt.gameObject, Hex("#6FE3A0"), false);
        fillBar.type = Image.Type.Filled;
        fillBar.fillMethod = Image.FillMethod.Horizontal;
        fillBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillBar.fillAmount = 0f;
        ConfigurePlaceholder(fillRt.gameObject, PlaceholderVisualKind.Solid, Hex("#6FE3A0"));

        RectTransform stage = GetOrCreateUIChild(win, "StageText");
        SetRect(stage, tl, tl, tl, new Vector2(12f, -92f), new Vector2(200f, 18f));
        Text stageText = ConfigureText(stage.gameObject, "Normal", 14, Color.white, TextAnchor.MiddleLeft);

        RectTransform running = GetOrCreateUIChild(win, "RunningCountText");
        SetRect(running, tl, tl, tl, new Vector2(12f, -112f), new Vector2(200f, 18f));
        Text runningCountText = ConfigureText(running.gameObject, "Processes: 0/3", 13, Color.white, TextAnchor.MiddleLeft);

        RectTransform thermoBg = GetOrCreateUIChild(win, "ThermoBg");
        SetRect(thermoBg, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -40f), new Vector2(14f, 90f));
        ConfigureImage(thermoBg.gameObject, Hex("#0B0F16"), false);

        RectTransform thermoRt = GetOrCreateUIChild(thermoBg, "ThermometerFill");
        SetStretch(thermoRt);
        Image thermometerFill = ConfigureImage(thermoRt.gameObject, Hex("#D9534F"), false);
        thermometerFill.type = Image.Type.Filled;
        thermometerFill.fillMethod = Image.FillMethod.Vertical;
        thermometerFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        thermometerFill.fillAmount = 0f;
        ConfigurePlaceholder(thermoRt.gameObject, PlaceholderVisualKind.Solid, Hex("#D9534F"));

        r.cpuWindow = GetOrAddComponent<CPUWindowUI>(win.gameObject);
        r.cpuWindow.windowRoot = win;
        r.cpuWindow.percentText = percentText;
        r.cpuWindow.fillBar = fillBar;
        r.cpuWindow.stageText = stageText;
        r.cpuWindow.runningCountText = runningCountText;
        r.cpuWindow.thermometerFill = thermometerFill;
        Dirty(r.cpuWindow);
    }

    static void BuildGlitchOverlay(Transform canvas, Refs r)
    {
        RectTransform overlay = GetOrCreateUIChild(canvas, "GlitchOverlay");
        SetStretch(overlay);
        Image img = ConfigureImage(overlay.gameObject, new Color(1f, 1f, 1f, 0.25f), false);
        ConfigurePlaceholder(overlay.gameObject, PlaceholderVisualKind.Noise, new Color(1f, 1f, 1f, 0.25f));

        CanvasGroup group = GetOrAddComponent<CanvasGroup>(overlay.gameObject);
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        overlay.gameObject.SetActive(true);
        r.glitchOverlayGroup = group;
        Dirty(img);
    }

    static void BuildWarningOverlay(Transform canvas, Refs r)
    {
        RectTransform warning = GetOrCreateUIChild(canvas, "WarningOverlay");
        Vector2 c = new Vector2(0.5f, 0.5f);
        SetRect(warning, c, c, c, Vector2.zero, new Vector2(600f, 60f));
        ConfigureText(warning.gameObject, "! INPUT INTERFERENCE !", 30, Hex("#FFD34D"), TextAnchor.MiddleCenter, FontStyle.Bold);
        warning.gameObject.SetActive(false);
        r.warningOverlay = warning.gameObject;
    }

    static void BuildResultUI(Transform canvas, Refs r)
    {
        RectTransform result = GetOrCreateUIChild(canvas, "ResultUI");
        SetStretch(result);
        r.resultUI = GetOrAddComponent<GameResultUI>(result.gameObject);

        Vector2 c = new Vector2(0.5f, 0.5f);

        // Blue screen (fail).
        RectTransform blue = GetOrCreateUIChild(result, "BlueScreenPanel");
        SetStretch(blue);
        ConfigureImage(blue.gameObject, Hex("#0B3EA8"), true); // blocks clicks on purpose

        RectTransform sad = GetOrCreateUIChild(blue, "SadFaceText");
        SetRect(sad, c, c, c, new Vector2(0f, 170f), new Vector2(400f, 140f));
        ConfigureText(sad.gameObject, ":(", 110, Color.white, TextAnchor.MiddleCenter);

        RectTransform msg = GetOrCreateUIChild(blue, "MessageText");
        SetRect(msg, c, c, c, new Vector2(0f, 40f), new Vector2(1000f, 40f));
        ConfigureText(msg.gameObject, "Your PC ran into a problem: too many processes.", 26, Color.white, TextAnchor.MiddleCenter);

        RectTransform stop = GetOrCreateUIChild(blue, "StopCodeText");
        SetRect(stop, c, c, c, new Vector2(0f, -8f), new Vector2(1000f, 30f));
        ConfigureText(stop.gameObject, "Stop code: CPU_OVERLOAD_100", 18, Hex("#BFD4F2"), TextAnchor.MiddleCenter);

        r.restartButtonBlue = BuildRestartButton(blue, "RestartButtonBlue", new Vector2(0f, -110f), "Restart", Hex("#0B3EA8"));
        blue.gameObject.SetActive(false);
        r.blueScreenPanel = blue.gameObject;

        // Victory.
        RectTransform win = GetOrCreateUIChild(result, "VictoryPanel");
        SetStretch(win);
        ConfigureImage(win.gameObject, Hex("#0C6B37E0"), true); // blocks clicks on purpose

        RectTransform vTitle = GetOrCreateUIChild(win, "TitleText");
        SetRect(vTitle, c, c, c, new Vector2(0f, 110f), new Vector2(1000f, 80f));
        ConfigureText(vTitle.gameObject, "System Repaired", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

        RectTransform vSub = GetOrCreateUIChild(win, "SubtitleText");
        SetRect(vSub, c, c, c, new Vector2(0f, 30f), new Vector2(1000f, 40f));
        ConfigureText(vSub.gameObject, "System Booster installed successfully.", 24, Color.white, TextAnchor.MiddleCenter);

        r.restartButtonWin = BuildRestartButton(win, "RestartButtonWin", new Vector2(0f, -90f), "Play Again", Hex("#0C6B37"));
        win.gameObject.SetActive(false);
        r.victoryPanel = win.gameObject;

        // GameResultUI wires the button onClicks itself at runtime; builder wires references.
        r.resultUI.blueScreenPanel = r.blueScreenPanel;
        r.resultUI.victoryPanel = r.victoryPanel;
        r.resultUI.restartButtonBlue = r.restartButtonBlue;
        r.resultUI.restartButtonWin = r.restartButtonWin;
        Dirty(r.resultUI);
    }

    static Button BuildRestartButton(Transform parent, string buttonName, Vector2 pos, string label, Color textColor)
    {
        RectTransform rt = GetOrCreateUIChild(parent, buttonName);
        Vector2 c = new Vector2(0.5f, 0.5f);
        SetRect(rt, c, c, c, pos, new Vector2(220f, 56f));
        Image img = ConfigureImage(rt.gameObject, Color.white, true);
        Button button = GetOrAddComponent<Button>(rt.gameObject);
        button.targetGraphic = img;

        RectTransform text = GetOrCreateUIChild(rt, "Text");
        SetStretch(text);
        ConfigureText(text.gameObject, label, 24, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        return button;
    }

    static void EnsureEventSystem()
    {
        // Separate ROOT GameObject - never inside the UI canvas.
        GameObject es = GetOrCreateRoot("EventSystem");
        GetOrAddComponent<EventSystem>(es);

        // New Input System only: kill any legacy module, ensure the InputSystem module.
        StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null)
            Object.DestroyImmediate(legacy);
        GetOrAddComponent<InputSystemUIInputModule>(es);
    }

    // ------------------------------------------------------------------ duty 12: wiring

    static void WireReferences(Refs r)
    {
        r.cpuManager.softwareInventory = r.inventory;
        Dirty(r.cpuManager);

        r.inventory.cpuManager = r.cpuManager;
        r.inventory.player = r.player;
        r.inventory.interactionAudio = r.interactionAudio;
        Dirty(r.inventory);

        r.gameState.cpuManager = r.cpuManager;
        r.gameState.player = r.player;
        r.gameState.interference = r.interference;
        r.gameState.glitchBounds = r.glitchBounds;
        r.gameState.resultUI = r.resultUI;
        Dirty(r.gameState);

        r.abilityExecutor.inventory = r.inventory;
        r.abilityExecutor.player = r.player;
        r.abilityExecutor.cursor = r.cursor;
        r.abilityExecutor.gameState = r.gameState;
        r.abilityExecutor.interference = r.interference;
        r.abilityExecutor.cpuManager = r.cpuManager;
        r.abilityExecutor.temporaryIconsParent = r.temporaryIcons;
        Dirty(r.abilityExecutor);

        r.interference.cpuManager = r.cpuManager;
        r.interference.cameraTransform = r.camera != null ? r.camera.transform : null;
        r.interference.warningOverlay = r.warningOverlay;
        r.interference.glitchOverlayGroup = r.glitchOverlayGroup;
        Dirty(r.interference);

        r.glitchBounds.cpuManager = r.cpuManager;
        r.glitchBounds.player = r.player;
        r.glitchBounds.gameState = r.gameState;
        r.glitchBounds.interactionAudio = r.interactionAudio;
        r.glitchBounds.glitchLeft = r.glitchLeft;
        r.glitchBounds.glitchRight = r.glitchRight;
        r.glitchBounds.glitchTop = r.glitchTop;
        r.glitchBounds.glitchBottom = r.glitchBottom;
        r.glitchBounds.shrinkTarget = r.acceleratorIcon;
        Dirty(r.glitchBounds);

        r.player.interference = r.interference;
        r.player.cpuManager = r.cpuManager;
        r.player.gameState = r.gameState;
        r.player.groundChecker = r.groundChecker;
        r.player.jumpSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/Character/Jump_03.wav");
        r.player.landingSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
            RootFolder + "/Audio/SFX/Character/landing_02.wav");
        Dirty(r.player);

        r.installZone.inventory = r.inventory;
        Dirty(r.installZone);

        r.cursor.player = r.player;
        r.cursor.installZone = r.installZone;
        r.cursor.worldCamera = r.camera;
        r.cursor.gameState = r.gameState;
        Dirty(r.cursor);

        r.taskbar.inventory = r.inventory;
        r.taskbar.slots = r.slots;
        r.taskbar.clockText = r.clockText;
        Dirty(r.taskbar);

        r.cpuWindow.cpuManager = r.cpuManager;
        r.cpuWindow.inventory = r.inventory;
        Dirty(r.cpuWindow);

        r.resultUI.gameState = r.gameState;
        Dirty(r.resultUI);
    }

    // ------------------------------------------------------------------ duty 13: save + summary

    static void SaveAll()
    {
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[CPU100 Builder] Build complete. GameObjects created: " + createdObjects +
                  ", reused: " + reusedObjects +
                  ". Components added: " + createdComponents +
                  ". Assets created: " + createdAssets +
                  ", reused: " + reusedAssets +
                  ". Scene: " + ScenePath);
    }

    // ------------------------------------------------------------------ generic helpers

    static GameObject GetOrCreateRoot(string rootName)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == rootName)
            {
                reusedObjects++;
                return roots[i];
            }
        }
        GameObject go = new GameObject(rootName);
        createdObjects++;
        return go;
    }

    static GameObject GetOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent != null ? parent.Find(childName) : null;
        if (existing != null)
        {
            reusedObjects++;
            return existing.gameObject;
        }
        GameObject go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        createdObjects++;
        return go;
    }

    static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
        {
            component = go.AddComponent<T>();
            createdComponents++;
        }
        return component;
    }

    static RectTransform GetOrCreateUIChild(Transform parent, string childName)
    {
        GameObject go = GetOrCreateChild(parent, childName);
        return GetOrAddComponent<RectTransform>(go);
    }

    static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    static void SetStretch(RectTransform rt)
    {
        SetRect(rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    static Image ConfigureImage(GameObject go, Color color, bool raycastTarget)
    {
        Image img = GetOrAddComponent<Image>(go);
        img.sprite = null; // sprite-less colored rect (PlaceholderVisual re-assigns at runtime where attached)
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = raycastTarget;
        Dirty(img);
        return img;
    }

    static Text ConfigureText(GameObject go, string content, int fontSize, Color color,
                              TextAnchor alignment, FontStyle style = FontStyle.Normal)
    {
        Text text = GetOrAddComponent<Text>(go);
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false; // no Text in this UI is interactive
        Dirty(text);
        return text;
    }

    static void ConfigurePlaceholder(GameObject go, PlaceholderVisualKind kind, Color tint)
    {
        PlaceholderVisual visual = GetOrAddComponent<PlaceholderVisual>(go);
        visual.kind = kind;
        visual.tint = tint;
        // AddComponent already ran Awake/Apply with default field values ([ExecuteAlways]);
        // re-apply so the saved scene matches the configured kind/tint on the first run too.
        visual.Apply();
        Dirty(visual);
    }

    static Sprite LoadLargestSpriteAtPath(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        Sprite largest = null;
        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i] as Sprite;
            if (sprite != null &&
                (largest == null || sprite.rect.width * sprite.rect.height >
                 largest.rect.width * largest.rect.height))
                largest = sprite;
        }
        return largest;
    }

    static Color Hex(string hex)
    {
        Color color;
        if (ColorUtility.TryParseHtmlString(hex, out color))
            return color;
        Debug.LogWarning("[CPU100 Builder] Failed to parse color '" + hex + "'.");
        return Color.magenta;
    }

    static void Dirty(Object obj)
    {
        if (obj != null)
            EditorUtility.SetDirty(obj);
    }
}
