# CPU100 Assets structure

## Main folders

- `Art/` — source and imported visual assets, grouped by usage.
- `Data/Software/` — `SoftwareData` assets and balance values.
- `Prefabs/Characters/` — player and reusable character objects.
- `Prefabs/Desktop/Icons/` — one prefab per desktop icon/software.
- `Prefabs/Environment/` — collision walls, glitch bounds, and level pieces.
- `Prefabs/Systems/` — the reusable manager group.
- `Prefabs/UI/` — taskbar, CPU window, overlays, and result screens.
- `Scenes/Prototype/` — the current mechanics test scene.
- `Scripts/` — runtime code grouped by feature; editor-only tools stay in `Scripts/Editor/`.

## Workflow

1. Open `Scenes/Prototype/CPU100_Prototype`.
2. Run `Tools > CPU 100 > Organize Current Scene Into Prefabs` once.
3. Build later levels by dragging prefabs into a new scene.
4. Edit shared visuals/components in Prefab Mode. Keep positions and level-specific
   references as prefab-instance overrides in each scene.

The organizer is safe to run again: already connected top-level prefab instances
are skipped. Runtime-only objects under `TemporaryIcons` are intentionally excluded.
