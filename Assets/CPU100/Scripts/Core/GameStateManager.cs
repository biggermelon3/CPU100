using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// File-scope enum shared across modules (contract §5.3).
public enum GameState
{
    Playing,
    Won,
    Failed
}

/// <summary>
/// Win/lose orchestration. On win or fail it freezes the player, CPU growth,
/// input interference and the glitch bounds, then shows the matching result UI
/// (blue screen after a short delay on fail). Contract §5.3.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public CPUManager cpuManager;
    public PlayerController2D player;
    public InputInterferenceController interference;
    public GlitchBoundsController glitchBounds;
    public GameResultUI resultUI;
    public float blueScreenDelay = 0.8f;

    public GameState State { get { return state; } }

    public event System.Action<GameState> OnGameStateChanged;

    private GameState state = GameState.Playing;

    private void Awake()
    {
        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (interference == null) interference = FindFirstObjectByType<InputInterferenceController>();
        if (glitchBounds == null) glitchBounds = FindFirstObjectByType<GlitchBoundsController>();
        if (resultUI == null) resultUI = FindFirstObjectByType<GameResultUI>();
    }

    private void OnEnable()
    {
        if (cpuManager != null)
            cpuManager.OnCpuReachedMaximum += TriggerFail;
    }

    private void OnDisable()
    {
        if (cpuManager != null)
            cpuManager.OnCpuReachedMaximum -= TriggerFail;
    }

    /// <summary>Idempotent: only acts while Playing.</summary>
    public void TriggerWin()
    {
        if (state != GameState.Playing)
            return;

        state = GameState.Won;
        FreezeWorld(won: true);
        OnGameStateChanged?.Invoke(state);

        if (resultUI != null)
            resultUI.ShowVictory();
    }

    /// <summary>Idempotent: only acts while Playing. Blue screen appears after blueScreenDelay.</summary>
    public void TriggerFail()
    {
        if (state != GameState.Playing)
            return;

        state = GameState.Failed;
        FreezeWorld(won: false);
        OnGameStateChanged?.Invoke(state);

        if (isActiveAndEnabled)
            StartCoroutine(ShowBlueScreenAfterDelay());
        else if (resultUI != null)
            resultUI.ShowBlueScreen(); // can't run a coroutine on a disabled GO; show immediately
    }

    /// <summary>Reloads the current scene whether or not it is in Build Settings.</summary>
    public void RestartGame()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
            SceneManager.LoadScene(scene.buildIndex);
        else
            SceneManager.LoadScene(scene.name);
    }

    private void FreezeWorld(bool won)
    {
        if (player != null)
        {
            if (won) player.SetWon();
            else player.SetDead();
        }

        if (cpuManager != null) cpuManager.Frozen = true;
        if (interference != null) interference.Frozen = true;
        if (glitchBounds != null) glitchBounds.Frozen = true;
    }

    private IEnumerator ShowBlueScreenAfterDelay()
    {
        yield return new WaitForSeconds(blueScreenDelay);
        if (resultUI != null)
            resultUI.ShowBlueScreen();
    }
}
