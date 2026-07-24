using UnityEngine;

// Distance-based install range around the player (child GO "InstallZone").
// CursorInteractor checks the release distance against Radius, then calls TryInstall.
public class SoftwareInstallZone : MonoBehaviour
{
    public float radius = 1.6f;
    public SoftwareInventory inventory;       // fallback find

    public float Radius { get { return radius; } }

    void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<SoftwareInventory>();
    }

    public bool TryInstall(DesktopIcon icon)
    {
        if (icon == null || icon.softwareData == null) return false;
        if (inventory == null) return false;
        return inventory.TryInstall(icon.softwareData);
    }
}
