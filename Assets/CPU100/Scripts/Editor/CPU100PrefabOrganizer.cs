using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Converts the reusable parts of the currently open CPU100 scene into connected
/// prefab instances. The operation is idempotent: existing connected instances
/// are left alone and existing prefab assets are updated through Unity.
/// </summary>
public static class CPU100PrefabOrganizer
{
    const string PrefabRoot = "Assets/CPU100/Prefabs";

    [MenuItem("Tools/CPU 100/Organize Current Scene Into Prefabs")]
    public static void OrganizeCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CPU100 Prefabs] Exit Play Mode before organizing the scene.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[CPU100 Prefabs] No editable scene is open.");
            return;
        }

        EnsureFolderTree();
        int created = 0;
        int skipped = 0;

        Transform icons = Find("DesktopWorld/DesktopIcons");
        if (icons != null)
        {
            // Copy first because SaveAsPrefabAssetAndConnect changes prefab state.
            var iconChildren = new List<Transform>();
            for (int i = 0; i < icons.childCount; i++)
                iconChildren.Add(icons.GetChild(i));

            foreach (Transform icon in iconChildren)
                SaveAndConnect(icon.gameObject, PrefabRoot + "/Desktop/Icons/" + SafeName(icon.name) + ".prefab",
                    ref created, ref skipped);
        }

        SavePath("DesktopWorld/Player", PrefabRoot + "/Characters/Player.prefab", ref created, ref skipped);

        SavePath("DesktopWorld/Hazards/GlitchLeft", PrefabRoot + "/Environment/GlitchLeft.prefab", ref created, ref skipped);
        SavePath("DesktopWorld/Hazards/GlitchRight", PrefabRoot + "/Environment/GlitchRight.prefab", ref created, ref skipped);
        SavePath("DesktopWorld/Hazards/GlitchTop", PrefabRoot + "/Environment/GlitchTop.prefab", ref created, ref skipped);
        SavePath("DesktopWorld/Hazards/GlitchBottom", PrefabRoot + "/Environment/GlitchBottom.prefab", ref created, ref skipped);
        SavePath("DesktopWorld/LeftWall", PrefabRoot + "/Environment/LeftWall.prefab", ref created, ref skipped);
        SavePath("DesktopWorld/RightWall", PrefabRoot + "/Environment/RightWall.prefab", ref created, ref skipped);

        SavePath("UI/DesktopTaskbar", PrefabRoot + "/UI/DesktopTaskbar.prefab", ref created, ref skipped);
        SavePath("UI/CPUWindow", PrefabRoot + "/UI/CPUWindow.prefab", ref created, ref skipped);
        SavePath("UI/GlitchOverlay", PrefabRoot + "/UI/GlitchOverlay.prefab", ref created, ref skipped);
        SavePath("UI/WarningOverlay", PrefabRoot + "/UI/WarningOverlay.prefab", ref created, ref skipped);
        SavePath("UI/ResultUI", PrefabRoot + "/UI/ResultUI.prefab", ref created, ref skipped);

        SavePath("GameRoot", PrefabRoot + "/Systems/GameSystems.prefab", ref created, ref skipped);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CPU100 Prefabs] Organization complete. Connected: " + created +
                  ", already organized: " + skipped + ". Scene: " + scene.path);
    }

    static void SavePath(string hierarchyPath, string assetPath, ref int created, ref int skipped)
    {
        Transform target = Find(hierarchyPath);
        if (target != null)
            SaveAndConnect(target.gameObject, assetPath, ref created, ref skipped);
        else
            Debug.LogWarning("[CPU100 Prefabs] Scene object not found: " + hierarchyPath);
    }

    static void SaveAndConnect(GameObject instance, string assetPath, ref int created, ref int skipped)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(instance) &&
            PrefabUtility.GetOutermostPrefabInstanceRoot(instance) == instance)
        {
            skipped++;
            return;
        }

        GameObject result = PrefabUtility.SaveAsPrefabAssetAndConnect(
            instance, assetPath, InteractionMode.UserAction);

        if (result != null)
            created++;
        else
            Debug.LogError("[CPU100 Prefabs] Could not create prefab: " + assetPath);
    }

    static Transform Find(string hierarchyPath)
    {
        string[] parts = hierarchyPath.Split('/');
        GameObject root = GameObject.Find(parts[0]);
        if (root == null)
            return null;

        Transform current = root.transform;
        for (int i = 1; i < parts.Length && current != null; i++)
            current = current.Find(parts[i]);
        return current;
    }

    static string SafeName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value;
    }

    static void EnsureFolderTree()
    {
        EnsureFolder("Assets/CPU100", "Prefabs");
        EnsureFolder(PrefabRoot, "Characters");
        EnsureFolder(PrefabRoot, "Desktop");
        EnsureFolder(PrefabRoot + "/Desktop", "Icons");
        EnsureFolder(PrefabRoot, "Environment");
        EnsureFolder(PrefabRoot, "Systems");
        EnsureFolder(PrefabRoot, "UI");
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
