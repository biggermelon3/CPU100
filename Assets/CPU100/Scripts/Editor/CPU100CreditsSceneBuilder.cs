#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CPU100CreditsSceneBuilder
{
    const string ScenePath = "Assets/CPU100/Scenes/Credits.unity";

    [InitializeOnLoadMethod]
    static void QueueAutomaticBuild()
    {
        EditorApplication.delayCall += BuildIfPossible;
    }

    [MenuItem("CPU100/Build Credits Scene")]
    public static void BuildCreditsScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before building the Credits scene.");
            return;
        }

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene creditsScene = SceneManager.GetSceneByPath(ScenePath);
        bool wasAlreadyLoaded = creditsScene.IsValid() && creditsScene.isLoaded;
        if (!wasAlreadyLoaded)
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            creditsScene = sceneAsset == null
                ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive)
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        GameObject root = creditsScene.GetRootGameObjects().FirstOrDefault(go => go.name == "Credits Scene");
        if (root == null)
        {
            root = new GameObject("Credits Scene");
            SceneManager.MoveGameObjectToScene(root, creditsScene);
        }

        CreditsSceneController controller = root.GetComponent<CreditsSceneController>();
        if (controller == null) controller = root.AddComponent<CreditsSceneController>();
        controller.EnsureVisuals();
        EditorSceneManager.MarkSceneDirty(creditsScene);
        EditorSceneManager.SaveScene(creditsScene, ScenePath);

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded &&
            previousActiveScene != creditsScene)
            SceneManager.SetActiveScene(previousActiveScene);

        if (!wasAlreadyLoaded) EditorSceneManager.CloseScene(creditsScene, true);
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("Credits scene created and added to Build Settings.");
    }

    static void BuildIfPossible()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        BuildCreditsScene();
    }

    static void EnsureBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        EditorBuildSettingsScene existing = scenes.FirstOrDefault(scene => scene.path == ScenePath);
        if (existing == null) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        else existing.enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
