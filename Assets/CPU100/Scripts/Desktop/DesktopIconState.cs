// Shared desktop-icon enums (API contract §5.7).
// This file intentionally contains no MonoBehaviour.

public enum DesktopIconType
{
    Folder,
    TextFile,
    ImageFile,
    Shortcut,
    Software,
    Virus,
    RecycleBin,
    Accelerator,
    SystemFile
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
