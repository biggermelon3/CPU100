using UnityEngine;
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

    void Awake()
    {
        if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();

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
        if (restartButtonBlue != null) restartButtonBlue.onClick.AddListener(HandleRestartClicked);
        if (restartButtonWin != null) restartButtonWin.onClick.AddListener(HandleRestartClicked);
    }

    public void ShowBlueScreen()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (blueScreenPanel != null) blueScreenPanel.SetActive(true);
    }

    public void ShowVictory()
    {
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
