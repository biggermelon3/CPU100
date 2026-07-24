using UnityEngine;

// File-scope enums shared across modules (contract §5.12).

public enum SoftwareAbilityType
{
    None,
    SpawnTemporaryIcon,
    AirDash,
    Glide,
    ShieldPush,
    Accelerator,
    DoubleJump,
    CpuSlowdown
}

public enum SoftwareSideEffectType { None, PopupBlock, InputDelay, InputReverse, ScreenObstruction }

public enum SoftwareState { World, Installed, Running, Corrupted, Deleted }

/// <summary>
/// Static configuration for one piece of installable software (Browser, Paper Plane, Shield...).
/// Authored as assets by the scene builder; consumed by SoftwareInventory / SoftwareAbilityExecutor.
/// </summary>
[CreateAssetMenu(fileName = "SoftwareData", menuName = "CPU100/Software Data")]
public class SoftwareData : ScriptableObject
{
    public string softwareName;
    public Sprite icon;                       // null → UI falls back to placeholder Software icon
    public SoftwareAbilityType abilityType;
    public SoftwareSideEffectType sideEffectType;

    public float startupCpuCost;
    public float runningCpuLoadPerSecond;
    public float closeCpuRelief;
    public float cooldown;

    public bool canUseRepeatedly = true;
    public bool isSpecialSoftware;
}
