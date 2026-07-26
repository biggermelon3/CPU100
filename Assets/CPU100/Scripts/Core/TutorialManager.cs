using UnityEngine;

/// <summary>
/// Drives the four tutorial beats of level 1 through TutorialPopupUI:
///   1. Intro (after a short delay): drag the Folder to build a platform.
///   2. Reaching the first Empty File area (trigger zone): drag Browser onto yourself.
///   3. First software installed: double-click to launch, E to use, CPU prices warning.
///   4. First software launched: the goal is the System Booster.
/// While a popup is open the game is frozen (Time.timeScale = 0) and the static
/// PopupOpen flag blocks player/cursor/ability input; clicking OK resumes.
/// Steps queue up if they trigger while another popup is open, and always fire in
/// order. Installing before reaching the trigger zone silently skips beat 2.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    /// <summary>Checked by PlayerController2D / CursorInteractor / SoftwareAbilityExecutor.</summary>
    public static bool PopupOpen { get; private set; }

    public TutorialPopupUI popup;               // fallback find
    public SoftwareInventory inventory;         // fallback find
    public GameStateManager gameState;          // fallback find
    public TutorialTriggerZone installHintTrigger;
    public float introDelay = 0.7f;

    static readonly string[] Titles =
    {
        "",
        "Welcome - Desktop Basics",
        "New Hardware Found",
        "Software Installed",
        "System Notification"
    };

    static readonly string[] Bodies =
    {
        "",
        "Move with A / D and jump with SPACE.\n\nYour cursor only works inside the ring around you. DRAG the Folder next to you and drop it wherever you need a step - folders make great platforms.\n\nEmpty files are read-only: they cannot be moved.",
        "That Browser icon ahead is installable software.\n\nDrag it ONTO your character to install it into the taskbar at the bottom of the screen.",
        "Software must be launched before it works: DOUBLE-CLICK its icon in the taskbar, then press E to use its ability.\n\nWarning: launching software and using abilities both raise the CPU percentage. Keep an eye on the Task Manager.",
        "Your goal: reach the System Booster shortcut at the top-right and touch it to purge the CPU.\n\nIf the CPU meter ever reaches 100%, the system crashes. Good luck."
    };

    readonly bool[] shown = new bool[5];
    readonly bool[] pending = new bool[5];
    float introTimer;

    void Awake()
    {
        if (popup == null) popup = FindFirstObjectByType<TutorialPopupUI>();
        if (inventory == null) inventory = FindFirstObjectByType<SoftwareInventory>();
        if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
        introTimer = introDelay;
        PopupOpen = false;
    }

    void OnEnable()
    {
        if (inventory != null) inventory.OnInventoryChanged += HandleInventoryChanged;
        if (installHintTrigger != null) installHintTrigger.OnPlayerEntered += HandleInstallHintEntered;
    }

    void OnDisable()
    {
        if (inventory != null) inventory.OnInventoryChanged -= HandleInventoryChanged;
        if (installHintTrigger != null) installHintTrigger.OnPlayerEntered -= HandleInstallHintEntered;

        // Scene unloading (restart/menu) while a popup is open must never leave the
        // global pause behind.
        if (PopupOpen)
        {
            PopupOpen = false;
            Time.timeScale = 1f;
        }
    }

    void Update()
    {
        if (popup == null) return;
        if (PopupOpen) return;
        if (gameState != null && gameState.State != GameState.Playing) return;

        if (!shown[1])
        {
            introTimer -= Time.unscaledDeltaTime;
            if (introTimer <= 0f) ShowStep(1);
            return;
        }

        for (int i = 2; i <= 4; i++)
        {
            if (pending[i] && !shown[i])
            {
                ShowStep(i);
                return;
            }
        }
    }

    void HandleInstallHintEntered()
    {
        if (!shown[2]) pending[2] = true;
    }

    void HandleInventoryChanged()
    {
        if (inventory == null) return;

        bool anyInstalled = false;
        bool anyRunning = false;
        SoftwareRuntimeItem[] slots = inventory.Slots;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].Data == null) continue;
            anyInstalled = true;
            if (slots[i].IsRunning) anyRunning = true;
        }

        if (anyInstalled && !shown[3])
        {
            pending[3] = true;
            shown[2] = true; // they figured out installing on their own; skip beat 2
        }
        if (anyRunning && !shown[4]) pending[4] = true;
    }

    void ShowStep(int step)
    {
        shown[step] = true;
        pending[step] = false;
        PopupOpen = true;
        Time.timeScale = 0f;
        popup.Show(Titles[step], Bodies[step], HandleConfirmed);
    }

    void HandleConfirmed()
    {
        PopupOpen = false;
        Time.timeScale = 1f;
    }
}
