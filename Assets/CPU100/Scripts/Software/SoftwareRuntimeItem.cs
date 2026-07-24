/// <summary>
/// Runtime state for one installed software slot. Plain serializable class (NOT a MonoBehaviour);
/// owned by SoftwareInventory, read by the taskbar UI and the ability executor.
/// </summary>
[System.Serializable]
public class SoftwareRuntimeItem
{
    public SoftwareData Data;
    public SoftwareState State = SoftwareState.Installed;
    public float CooldownRemaining;

    public bool IsRunning => State == SoftwareState.Running;

    public SoftwareRuntimeItem(SoftwareData data)
    {
        Data = data;
        State = SoftwareState.Installed;
        CooldownRemaining = 0f;
    }
}
