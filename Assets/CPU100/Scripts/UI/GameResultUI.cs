using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Win / fail result screens (UI GO "ResultUI" under Canvas). Both panels start
// hidden; GameStateManager calls ShowVictory / ShowBlueScreen. Both restart
// buttons reload the scene through gameState.RestartGame().
public class GameResultUI : MonoBehaviour
{
    public GameObject blueScreenPanel;        // inactive by default
    public GameObject victoryPanel;           // inactive by default
    public Button restartButtonBlue;
    public Button restartButtonWin;
    public GameStateManager gameState;
    CpuAdaptiveMusic adaptiveMusic;

    void Awake()
    {
        if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
        adaptiveMusic = FindFirstObjectByType<CpuAdaptiveMusic>();

        // Fallback wiring by builder child names (see contract section 6).
        if (blueScreenPanel == null) blueScreenPanel = FindChildObject(transform, "BlueScreenPanel");
        if (victoryPanel == null) victoryPanel = FindChildObject(transform, "VictoryPanel");
        if (restartButtonBlue == null) restartButtonBlue = FindButton(blueScreenPanel, "RestartButtonBlue");
        if (restartButtonWin == null) restartButtonWin = FindButton(victoryPanel, "RestartButtonWin");

        // The panels start inactive, so fix fonts on their (inactive) child Texts here.
        FixFonts(blueScreenPanel);
        FixFonts(victoryPanel);
    }

    void Start()
    {
        HideAll();
        // Clone menu buttons BEFORE wiring the restart listeners: runtime listeners
        // are not serialized, so the clones start with clean onClick events.
        TryAddMenuButton(restartButtonBlue);
        TryAddMenuButton(restartButtonWin);
        if (restartButtonBlue != null) restartButtonBlue.onClick.AddListener(HandleRestartClicked);
        if (restartButtonWin != null) restartButtonWin.onClick.AddListener(HandleRestartClicked);
    }

    // Adds a "MAIN MENU" button below the given restart button (skipped when no
    // MainMenu scene is available, e.g. a level launched directly in the editor).
    void TryAddMenuButton(Button restartButton)
    {
        if (restartButton == null) return;
        if (!Application.CanStreamedLevelBeLoaded("MainMenu")) return;

        Button menuButton = Instantiate(restartButton, restartButton.transform.parent);
        menuButton.name = restartButton.name + "_Menu";
        RectTransform rt = (RectTransform)menuButton.transform;
        RectTransform src = (RectTransform)restartButton.transform;
        rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, -(src.sizeDelta.y + 14f));

        Text label = menuButton.GetComponentInChildren<Text>(true);
        if (label != null) label.text = "MAIN MENU";

        menuButton.onClick.RemoveAllListeners();
        menuButton.onClick.AddListener(HandleMenuClicked);
    }

    void HandleMenuClicked()
    {
        Time.timeScale = 1f; // safety: never carry a paused clock into the menu
        SceneManager.LoadScene("MainMenu");
    }

    public void ShowBlueScreen()
    {
        PlayBlueScreenAudio();
        CpuGlitchController glitch = FindFirstObjectByType<CpuGlitchController>();
        if (glitch != null)
            glitch.StopGlitch();
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (blueScreenPanel != null) blueScreenPanel.SetActive(true);
    }

    public void PlayBlueScreenAudio()
    {
        if (adaptiveMusic != null) adaptiveMusic.PlayBlueScreenEnding();
    }

    public void ShowVictory()
    {
        if (adaptiveMusic != null) adaptiveMusic.PlayRepairSuccess();
        if (blueScreenPanel != null) blueScreenPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(true);
    }

    public void HideAll()
    {
        if (blueScreenPanel != null) blueScreenPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    void HandleRestartClicked()
    {
        if (gameState != null) gameState.RestartGame();
    }

    static GameObject FindChildObject(Transform root, string childName)
    {
        Transform found = FindRecursive(root, childName);
        return found != null ? found.gameObject : null;
    }

    static Button FindButton(GameObject panel, string childName)
    {
        if (panel == null) return null;
        Transform found = FindRecursive(panel.transform, childName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    // Depth-first name search that also visits inactive children.
    static Transform FindRecursive(Transform root, string childName)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName) return child;
            Transform deep = FindRecursive(child, childName);
            if (deep != null) return deep;
        }
        return null;
    }

    static void FixFonts(GameObject root)
    {
        if (root == null) return;
        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].font == null)
                texts[i].font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
