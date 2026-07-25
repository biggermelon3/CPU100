using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CPU100IconArtBinder
{
    const string IconArt = "Assets/CPU100/Art/Icons/";
    const string IconPrefabs = "Assets/CPU100/Prefabs/Desktop/Icons/";
    const string SoftwareDataFolder = "Assets/CPU100/Data/Software/";
    const string SessionKey = "CPU100.IconArtBinder.Applied";
    const float TargetBodySize = 1f;

    struct Binding
    {
        public string prefab;
        public string art;
        public string data;

        public Binding(string prefab, string art, string data = null)
        {
            this.prefab = prefab;
            this.art = art;
            this.data = data;
        }
    }

    static readonly Binding[] Bindings =
    {
        new Binding("BrowserSoftware", "Browser.png", "Browser.asset"),
        new Binding("HourglassSoftware", "Hourglass.png", "Hourglass.asset"),
        new Binding("StartFolder", "Folder.png"),
        new Binding("PaperPlaneSoftware", "Plane.png", "PaperPlane.asset"),
        new Binding("TextFilePlatform", "NewTextDoc.png"),
        new Binding("ShieldSoftware", "Shield.png", "Shield.asset"),
        new Binding("RecycleBin", "Recycle Bin.png"),
        new Binding("SystemFile", "SystemSetting.png")
    };

    [InitializeOnLoadMethod]
    static void BindAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SessionState.GetBool(SessionKey, false) ||
                !AllArtExists())
                return;

            SessionState.SetBool(SessionKey, true);
            Apply();
        };
    }

    [MenuItem("Tools/CPU 100/Apply Imported Icon Art")]
    public static void Apply()
    {
        var spritesByPrefab = new Dictionary<string, Sprite>();
        int applied = 0;

        foreach (Binding binding in Bindings)
        {
            Sprite sprite = LoadLargestSprite(IconArt + binding.art);
            if (sprite == null)
            {
                Debug.LogError("[CPU100 Icons] Could not load: " + binding.art);
                continue;
            }

            string prefabPath = IconPrefabs + binding.prefab + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                continue;

            ApplyToIcon(root, sprite);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            spritesByPrefab[binding.prefab] = sprite;

            if (!string.IsNullOrEmpty(binding.data))
            {
                SoftwareData data = AssetDatabase.LoadAssetAtPath<SoftwareData>(
                    SoftwareDataFolder + binding.data);
                if (data != null)
                {
                    data.icon = sprite;
                    EditorUtility.SetDirty(data);
                }
            }
            applied++;
        }

        ApplyToOpenScene(spritesByPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CPU100 Icons] Applied imported art to " + applied +
                  " desktop icon prefabs and their SoftwareData assets.");
    }

    static void ApplyToIcon(GameObject root, Sprite sprite)
    {
        DesktopIcon icon = root.GetComponent<DesktopIcon>();
        if (icon != null)
        {
            icon.iconSprite = sprite;
            EditorUtility.SetDirty(icon);
        }

        Transform body = root.transform.Find("Body");
        if (body == null)
            return;

        SpriteRenderer renderer = body.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        renderer.sprite = sprite;
        NormalizeBodyScale(body, sprite);
        EditorUtility.SetDirty(renderer);
    }

    static void ApplyToOpenScene(Dictionary<string, Sprite> spritesByPrefab)
    {
        DesktopIcon[] icons = Object.FindObjectsByType<DesktopIcon>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool changed = false;

        foreach (DesktopIcon icon in icons)
        {
            if (icon == null || !spritesByPrefab.TryGetValue(icon.name, out Sprite sprite))
                continue;
            icon.iconSprite = sprite;
            Transform body = icon.transform.Find("Body");
            if (body != null)
            {
                SpriteRenderer renderer = body.GetComponent<SpriteRenderer>();
                if (renderer != null)
                    renderer.sprite = sprite;
                NormalizeBodyScale(body, sprite);
            }
            EditorUtility.SetDirty(icon);
            changed = true;
        }

        if (changed && UnityEngine.SceneManagement.SceneManager.GetActiveScene().IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
    }

    static void NormalizeBodyScale(Transform body, Sprite sprite)
    {
        float largestSide = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float scale = largestSide > 0.0001f ? TargetBodySize / largestSide : 1f;
        body.localScale = Vector3.one * scale;
    }

    static Sprite LoadLargestSprite(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        Sprite largest = null;
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite &&
                (largest == null || sprite.rect.width * sprite.rect.height >
                 largest.rect.width * largest.rect.height))
                largest = sprite;
        }
        return largest;
    }

    static bool AllArtExists()
    {
        foreach (Binding binding in Bindings)
        {
            if (LoadLargestSprite(IconArt + binding.art) == null)
                return false;
        }
        return true;
    }
}
