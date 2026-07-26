using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneFadeLoader : MonoBehaviour
{
    static bool isLoading;
    CanvasGroup fadeGroup;
    float fadeDuration;
    string targetScene;

    public static void LoadScene(string sceneName, float duration = 0.7f)
    {
        if (isLoading || string.IsNullOrWhiteSpace(sceneName)) return;
        isLoading = true;

        GameObject fadeObject = new GameObject(
            "Scene Fade",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(SceneFadeLoader));
        DontDestroyOnLoad(fadeObject);

        Canvas canvas = fadeObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = fadeObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rect = fadeObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = fadeObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        SceneFadeLoader loader = fadeObject.GetComponent<SceneFadeLoader>();
        loader.fadeGroup = fadeObject.GetComponent<CanvasGroup>();
        loader.fadeGroup.alpha = 0f;
        loader.fadeDuration = Mathf.Max(0.05f, duration);
        loader.targetScene = sceneName;
        loader.StartCoroutine(loader.FadeAndLoad());
    }

    IEnumerator FadeAndLoad()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = 1f;
        SceneManager.LoadScene(targetScene);
        isLoading = false;
        Destroy(gameObject);
    }
}
