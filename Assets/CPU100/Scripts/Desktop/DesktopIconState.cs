// Shared desktop-icon enums (API contract §5.7).
// This file intentionally contains no MonoBehaviour.

public enum DesktopIconType
{
    Folder = 0,
    TextFile = 1,
    Shortcut = 3,
    Software = 4,
    ErrorFile = 5,
    RecycleBin = 6,
    Accelerator = 7,
    SystemFile = 8
}

public enum DesktopIconState
{
    Normal,
    Selected,
    Dragging,
    Installed,
    Running,
    Corrupted,
    Deleted
}
