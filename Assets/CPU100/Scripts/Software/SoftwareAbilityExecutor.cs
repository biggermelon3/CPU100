using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Listens for the E key and executes the currently selected running software's ability:
/// Browser spawns a temporary "New Tab.url" platform icon, Paper Plane air-dashes,
/// Shield temporarily overrides and freezes CPU. Starts the item's cooldown after each use.
/// </summary>
public class SoftwareAbilityExecutor : MonoBehaviour
{
    public SoftwareInventory inventory;
    public PlayerController2D player;
    public CursorInteractor cursor;
    public GameStateManager gameState;
    public InputInterferenceController interference;
    public CPUManager cpuManager;
    public Transform temporaryIconsParent;

    public float tempIconLifetime = 5f;
    public float shieldDuration = 5f;
    public float hourglassDuration = 5f;
    [Range(0f, 1f)] public float hourglassGrowthMultiplier = 0.3f;
    PlayerAnimationController playerAnimation;

    private void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<SoftwareInventory>();
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (cursor == null) cursor = FindFirstObjectByType<CursorInteractor>();
        if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
        if (interference == null) interference = FindFirstObjectByType<InputInterferenceController>();
        if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();
        if (player != null) playerAnimation = player.GetComponent<PlayerAnimationController>();
    }

    private void Start()
    {
        // Temp icons live under DesktopWorld/TemporaryIcons; if the builder did not wire it,
        // find it by name, else create a root container so spawning always works.
        if (temporaryIconsParent == null)
        {
            GameObject found = GameObject.Find("TemporaryIcons");
            if (found == null) found = new GameObject("TemporaryIcons");
            temporaryIconsParent = found.transform;
        }
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        // Gate chain: game must be running and input not interference-blocked.
        if (gameState != null && gameState.State != GameState.Playing) return;
        if (interference != null && interference.InputBlocked) return;
        if (inventory == null) return;
        if (player != null && player.AbilityMovementLocked) return;

        SoftwareRuntimeItem item = inventory.SelectedItem;
        if (item == null || !item.IsRunning || item.CooldownRemaining > 0f) return;
        if (item.Data == null) return;

        switch (item.Data.abilityType)
        {
            case SoftwareAbilityType.SpawnTemporaryIcon:
                if (cursor == null || !cursor.CanInteractWithWorld)
                    return;
                SpawnTemporaryIcon();
                break;

            case SoftwareAbilityType.AirDash:
            case SoftwareAbilityType.Glide:
                if (player == null || cursor == null)
                    return;
                player.AirDashTowards(cursor.PointerWorldPos);
                break;

            case SoftwareAbilityType.ShieldPush:
                if (cpuManager == null) return;
                cpuManager.SetTemporaryValueAndFreeze(5f, shieldDuration);
                break;

            case SoftwareAbilityType.CpuSlowdown:
                if (cpuManager == null) return;
                cpuManager.ActivateGrowthSlowdown(hourglassDuration, hourglassGrowthMultiplier);
                break;

            default:
                // None / Accelerator: no active effect.
                break;
        }

        // Every ability use has an instant CPU price (shown in the taskbar tooltip).
        if (cpuManager != null && item.Data.usageCpuCost > 0f)
            cpuManager.AddCpu(item.Data.usageCpuCost);

        item.CooldownRemaining = item.Data.cooldown;

        if (playerAnimation != null &&
            (item.Data.abilityType == SoftwareAbilityType.SpawnTemporaryIcon ||
             item.Data.abilityType == SoftwareAbilityType.ShieldPush ||
             item.Data.abilityType == SoftwareAbilityType.CpuSlowdown))
            playerAnimation.TryPlayAbility(0.5f);
    }

    private void SpawnTemporaryIcon()
    {
        Vector2 pos;
        if (cursor != null) pos = cursor.CursorWorldPos;
        else if (player != null) pos = player.transform.position;
        else pos = transform.position;

        // Keep spawned platforms inside the playable desktop area.
        pos.x = Mathf.Clamp(pos.x, -9f, 9f);
        pos.y = Mathf.Clamp(pos.y, -4.6f, 5.0f);

        DesktopIcon icon = DesktopIcon.CreateRuntimeIcon(
            "New Tab.url", DesktopIconType.Shortcut, pos, temporaryIconsParent, true, true);
        if (icon != null) icon.ScheduleExpire(tempIconLifetime);
    }
}
