# CPU 100% — API Contract (v1, binding)

This document is the single source of truth for all module authors. Every public API,
object name, coordinate, and convention below is BINDING. Other agents write the other
modules against exactly this contract — do not rename, do not "improve" signatures.
Read `buildPlan.txt` (project root) for full gameplay intent; where this contract is
more specific, this contract wins.

## 1. Global conventions (all files)

- Unity **6000.2.13f1**, 2D URP, C#. **No namespaces** (plan §18).
- **New Input System ONLY** (`activeInputHandler=1`). `using UnityEngine.InputSystem;`
  Poll `Keyboard.current` / `Mouse.current` **with null checks**
  (`var kb = Keyboard.current; if (kb == null) return;`).
  NEVER use `UnityEngine.Input`, `Input.GetKey`, `Input.mousePosition`, or `StandaloneInputModule`.
- Unity 6 APIs: `rb.linearVelocity` (NOT `rb.velocity`), `Object.FindFirstObjectByType<T>()`
  / `FindObjectsByType<T>(FindObjectsSortMode.None)` (NEVER `FindObjectOfType`).
- **Cross-component references**: every reference to another manager/component is a
  `public` Unity-serialized field, with a one-time fallback in `Awake()`:
  `if (cpuManager == null) cpuManager = FindFirstObjectByType<CPUManager>();`
  Never find objects per-frame. Cache everything in Awake/Start.
- Events: C# `event System.Action...`, invoke with `?.Invoke(...)`. Subscribe in
  `OnEnable`, unsubscribe in `OnDisable`.
- No LINQ in Update paths. No per-frame allocations (pre-allocate overlap arrays,
  reuse `Collider2D[]` buffers). Short one-shot coroutines are OK.
- Do NOT touch `Time.timeScale`, `Application.targetFrameRate`, real frame rate.
- UI uses **legacy UGUI** (`UnityEngine.UI.Text`, `Image`, `Button`) — NOT TextMeshPro.
  Font: `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`. Every UI script that
  owns Text fields must, in Awake, assign this font to any Text whose `font == null`.
- World-space labels use **`TextMesh`** (legacy 3D text), never TMP.
- **One MonoBehaviour per file, file name == class name** (Unity scene-serialization
  requirement). Exception: `GlitchZone` (runtime-only relay, never saved into the scene)
  lives inside `GlitchBoundsController.cs`.
- Enums referenced across modules live exactly where §5 says they live.
- Tuning values: implement as public fields with the default values given here.
- Comments/strings in English. Player-visible text in English (jam build).

## 2. World metrics & level layout

- Camera: name `Main Camera`, tag MainCamera, orthographic, `orthographicSize = 5.4`,
  position `(0, 0, -10)`, solid background color `#1E5B8Cff`. Fixed forever. 16:9 target
  → visible world rect x ∈ [-9.6, 9.6], y ∈ [-5.4, 5.4].
- Physics: default gravity (-9.81). Player `gravityScale = 3.5`, `jumpVelocity = 13`,
  `moveSpeed = 6` → single jump reach ≈ 3.4 horizontal, ≈ 2.3 vertical. Keep icon gaps
  below that EXCEPT the final gap (needs abilities).
- Fall-out: player below `y = -7` → respawn at `SpawnPoint`, +5 CPU.
- Icon positions (icon root centers; `iconScale` = root uniform localScale):

| GO name (exact) | iconName | DesktopIconType | pos | scale | flags | softwareData |
|---|---|---|---|---|---|---|
| StartFolder | `Documents` | Folder | (-7.8, -3.4) | 1.4 | platform | — |
| BrowserSoftware | `Browser.exe` | Software | (-6.0, -3.9) | 1.2 | platform, drag, install | Browser |
| PaperPlaneSoftware | `Paper Plane.exe` | Software | (-2.8, -3.7) | 1.2 | platform, drag, install | PaperPlane |
| TextFilePlatform | `New Text Document.txt` | TextFile | (-5.0, -1.8) | 1.3 | platform | — |
| ImageFilePlatform | `photo.png` | ImageFile | (-2.4, -0.4) | 1.3 | platform | — |
| VirusFile | `virus.exe` | Virus | (0.4, -2.6) | 1.3 | platform | — |
| SystemFile | `system32.dll` | SystemFile | (0.6, 1.1) | 1.3 | platform | — |
| ShieldSoftware | `Shield.exe` | Software | (4.2, -1.6) | 1.2 | platform, drag, install | Shield |
| RecycleBin | `Recycle Bin` | RecycleBin | (8.5, -3.9) | 1.5 | platform | — |
| AcceleratorShortcut | `System Booster.lnk` | Accelerator | (7.4, 2.6) | 1.4 | platform, isShortcut | — |

- Player spawn: `(-7.8, -2.3)` (on StartFolder).
- The SystemFile→Accelerator gap (≈6.8) is intentionally impossible without the Browser
  temp-platform or PaperPlane dash.

## 3. Layers / tags / sorting

- Tags: player root GO tag = `Player` (built-in tag). Nothing else tagged.
- Layers (created by the scene builder via TagManager; scripts resolve by NAME with
  fallback): `Player`, `Platform`.
  - Player root GO → layer `Player`. Desktop-icon roots (with colliders) → layer `Platform`.
  - Script-side lookup pattern: `int l = LayerMask.NameToLayer("Platform"); if (l < 0) l = 0;`
- Sorting (all on Default sorting layer, `sortingOrder`):
  wallpaper -100 · icon RunningGlow 8 · SelectionOutline 9 · icon Body 10 ·
  ShortcutArrow 11 · Label 11 · CorruptionOverlay 12 · player 50 · glitch zones 400 ·
  cursor RangeRing 199 · cursor sprite 200.
- ALL full-screen UI overlay Images (GlitchOverlay, WarningOverlay) must have
  `raycastTarget = false` so they never block taskbar clicks. Result panels
  (BlueScreenPanel/VictoryPanel) keep raycastTarget = true (they block on purpose).

## 4. File map & module ownership

```
Assets/CPU100/Scripts/
├── Core/       CPUStage.cs, CPUManager.cs, GameStateManager.cs          [module: core]
├── Player/     PlayerController2D.cs, PlayerGroundChecker.cs,
│               InputInterferenceController.cs                            [module: player]
├── Desktop/    DesktopIconState.cs, DesktopIcon.cs                       [module: desktop-icon]
│               CursorInteractor.cs, SoftwareInstallZone.cs,
│               GlitchBoundsController.cs                                 [module: desktop-interact]
├── Software/   SoftwareData.cs, SoftwareRuntimeItem.cs,
│               SoftwareInventory.cs, SoftwareAbilityExecutor.cs          [module: software]
├── UI/         SoftwareTaskbarUI.cs, TaskbarSlotUI.cs, CPUWindowUI.cs,
│               GameResultUI.cs                                           [module: ui]
├── Art/        PlaceholderSpriteFactory.cs, PlaceholderVisual.cs         [module: art]
└── Editor/     CPU100PrototypeSceneBuilder.cs                            [module: builder]
```

## 5. Class specifications (BINDING public APIs)

### 5.1 `CPUStage.cs`
```csharp
public enum CPUStage { Normal, LightLoad, MediumLoad, HeavyLoad, Critical, Crashed }
```

### 5.2 `CPUManager.cs` (MonoBehaviour)
```csharp
public float baseIncreasePerSecond = 0.35f;
public SoftwareInventory softwareInventory;      // Awake fallback find

public float CurrentCpu { get; }                 // 0..100
public CPUStage CurrentStage { get; }
public bool Frozen { get; set; }                 // true → no growth, no events from growth

public event System.Action<float> OnCpuChanged;          // fired when value changes
public event System.Action<CPUStage> OnCpuStageChanged;  // fired on stage transitions
public event System.Action OnCpuReachedMaximum;          // fired exactly once at 100

public void AddCpu(float amount);                // instant add, clamps 0..100, fires events
public void RelieveCpu(float amount);            // instant subtract, clamps
public void SetHazardLoad(float perSecond);      // set by GlitchBoundsController every frame
public static CPUStage StageForValue(float cpu); // <=30 N, <=50 L, <=70 M, <=85 H, <100 C, else Crashed
```
Update (when !Frozen): `AddCpu((baseIncreasePerSecond + softwareInventory.TotalRunningLoadPerSecond + hazardLoad) * Time.deltaTime)`.
`OnCpuReachedMaximum` fires once; further AddCpu calls do nothing after Crashed.

### 5.3 `GameStateManager.cs` (MonoBehaviour)
```csharp
public enum GameState { Playing, Won, Failed }   // nested at file scope, NOT inside class

public CPUManager cpuManager; public PlayerController2D player;
public InputInterferenceController interference; public GlitchBoundsController glitchBounds;
public GameResultUI resultUI;
public float blueScreenDelay = 0.8f;

public GameState State { get; }
public event System.Action<GameState> OnGameStateChanged;
public void TriggerWin();     // idempotent; freezes everything, resultUI.ShowVictory()
public void TriggerFail();    // idempotent; freezes, waits blueScreenDelay, ShowBlueScreen()
public void RestartGame();    // reload scene: buildIndex >= 0 ? LoadScene(buildIndex) : LoadScene(name)
```
Subscribes `cpuManager.OnCpuReachedMaximum += TriggerFail` (OnEnable/OnDisable).
Freeze = `player.SetWon()/SetDead()`, `cpuManager.Frozen = true`,
`interference.Frozen = true`, `glitchBounds.Frozen = true`.

### 5.4 `PlayerController2D.cs` (MonoBehaviour, RequireComponent(typeof(Rigidbody2D)))
```csharp
public enum PlayerState { Idle, Moving, Jumping, Falling, InputBlocked, Dead, Won } // file scope

public float moveSpeed = 6f, jumpVelocity = 13f, coyoteTime = 0.12f, jumpBufferTime = 0.12f;
public float fallKillY = -7f, respawnCpuPenalty = 5f;
public float airDashSpeed = 14f, airDashDuration = 0.18f;
public InputInterferenceController interference; public CPUManager cpuManager;
public GameStateManager gameState; public PlayerGroundChecker groundChecker;

public PlayerState State { get; }
public bool FacingRight { get; }                 // default true, updated by move input
public bool ShieldActive { get; }
public Vector3 SpawnPoint { get; set; }          // Awake default = transform.position
public void Respawn();                           // teleport to SpawnPoint, zero velocity, cpuManager.AddCpu(respawnCpuPenalty)
public void AirDash();                           // burst toward facing dir, works in air, overrides velocity for airDashDuration
public void ActivateShield(float duration);
public void ApplyKnockback(Vector2 impulse);     // rb.AddForce(impulse, ForceMode2D.Impulse)
public void SetDead(); public void SetWon();     // zero velocity, block all input permanently
```
- Input: A/D + Left/Right arrows, Space jump. Read in Update, apply in FixedUpdate via
  `rb.linearVelocity = new Vector2(h * moveSpeed, rb.linearVelocity.y)` (except during dash).
- Respect `interference.InputBlocked` (ignore all input, State=InputBlocked while Playing)
  and `interference.InputReversed` (negate horizontal).
- Grounded = `groundChecker.IsGrounded && rb.linearVelocity.y <= 0.1f`. Coyote time +
  jump buffer per standard implementation. Jump = set `linearVelocity.y = jumpVelocity`.
- Fall check in Update: `y < fallKillY && State != Dead/Won` → `Respawn()`.
- Rigidbody config is done by the scene builder (freezeRotation, gravityScale 3.5,
  Continuous collision, interpolation) — do not fight it at runtime.
- Sprite flip: `spriteRenderer.flipX = !FacingRight` (SpriteRenderer on same GO).
- Dash: during dash timer, `rb.linearVelocity = new Vector2(dir * airDashSpeed, 0)`.

### 5.5 `PlayerGroundChecker.cs` (MonoBehaviour, on child GO `GroundCheck`)
```csharp
public Vector2 boxSize = new Vector2(0.5f, 0.12f);
public LayerMask groundMask;          // builder sets to Platform; if value == 0 in Awake,
                                      // fallback: mask = 1 << NameToLayer("Platform") (or Default if <0)
public bool IsGrounded { get; }       // updated in FixedUpdate
```
`Physics2D.OverlapBox(transform.position, boxSize, 0f, filter, buffer)` with
`ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = groundMask }`,
pre-allocated `Collider2D[8]` buffer, excludes own parent's colliders.

### 5.6 `InputInterferenceController.cs` (MonoBehaviour)
```csharp
public CPUManager cpuManager; public Transform cameraTransform;   // fallback Camera.main
public GameObject warningOverlay;         // UI GO, SetActive during warnings/reversal
public CanvasGroup glitchOverlayGroup;    // UI CanvasGroup, alpha flicker at Critical
public bool Frozen { get; set; }          // stop all interference + restore camera
public bool InputBlocked { get; }
public bool InputReversed { get; }
```
Stage behavior (from `cpuManager.CurrentStage`, all timer-based in Update, no per-frame GC):
- Normal/LightLoad (≤50): nothing.
- MediumLoad (51–70): every 4–8 s block input for 0.1 s.
- HeavyLoad (71–85): every 8–12 s → warningOverlay ON 0.5 s (pre-warning), then reverse
  input 1.5 s (warning stays on during reversal).
- Critical (86–99): block every 2–4 s for 0.15 s; reversal cycle every 5–8 s; camera
  local jitter ±0.05; glitchOverlayGroup.alpha flickers 0–0.35.
Cache camera base localPosition in Awake; restore whenever calm/Frozen.

### 5.7 `DesktopIconState.cs`
```csharp
public enum DesktopIconType { Folder, TextFile, ImageFile, Shortcut, Software, Virus,
                              RecycleBin, Accelerator, SystemFile }
public enum DesktopIconState { Normal, Selected, Dragging, Installed, Running, Corrupted, Deleted }
```

### 5.8 `DesktopIcon.cs` (MonoBehaviour) — THE central class
```csharp
public static readonly System.Collections.Generic.List<DesktopIcon> All; // OnEnable add / OnDisable remove

// config (set by builder or CreateRuntimeIcon)
public string iconName = "New Icon";
public Sprite iconSprite;                 // null → PlaceholderSpriteFactory.GetIconSprite(iconType)
public DesktopIconType iconType;
public bool isPlatform = true;
public bool canDrag; public bool canInstall; public bool isShortcut;
public SoftwareData softwareData;
public float iconScale = 1.3f;
// virus tuning (used when iconType == Virus)
public float virusCpuSpike = 10f; public float virusKnockback = 8f; public float virusDamageCooldown = 1f;

public DesktopIconState State { get; }    // starts Normal; runtime only
public bool IsCorrupted { get; }
public Vector3 HomePosition { get; }      // captured in Awake

public void EnsureVisuals();              // IDEMPOTENT, callable in edit mode AND play mode (see below)
public void SetState(DesktopIconState s); // drives all visuals; Corrupted is STICKY
                                          // (only Corrupted→Deleted allowed); Deleted → SetActive(false)
public void BeginDrag();                  // State=Dragging, root collider.enabled=false, body alpha 0.6
public void UpdateDrag(Vector2 worldPos); // transform.position = worldPos (z 0)
public void EndDrag(bool installed);      // installed → SetState(Installed)+SetActive(false)
                                          // else teleport back to HomePosition, SetState(Normal), collider on
public void ScheduleExpire(float lifetime); // temp icons: blink during final 1 s, then Destroy(gameObject)
public static DesktopIcon CreateRuntimeIcon(string name, DesktopIconType type, Vector2 position,
       Transform parent, bool isPlatform = true, bool isShortcut = false); // builds GO + EnsureVisuals
```
`EnsureVisuals()` (find-child-by-name-else-create, never duplicates; also called by the
editor builder, so use plain APIs that work in edit mode; destroy nothing):
- root: `transform.localScale = Vector3.one * iconScale`; layer = `Platform` (fallback Default).
- child `Body`: SpriteRenderer, localPos (0, 0.1), order 10, sprite = iconSprite ??
  factory sprite (assign only at runtime OR leave null in edit mode if factory unavailable —
  factory works in edit mode too, but re-assign in Awake because procedural sprites don't
  survive scene save).
- child `ShortcutArrow`: SpriteRenderer order 11, localPos (-0.32, -0.18), localScale 0.45,
  sprite = factory arrow; active only if `isShortcut`.
- child `Label`: TextMesh, text = iconName, localPos (0, -0.62), anchor UpperCenter,
  alignment Center, characterSize 0.07, fontSize 64, color white,
  font = `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` and assign
  `meshRenderer.material = font.material`, meshRenderer sortingOrder 11.
- child `SelectionOutline`: SpriteRenderer order 9, solid white alpha 0.35, localScale 1.25,
  inactive by default.
- child `RunningGlow`: SpriteRenderer order 8, solid `#6FE3A0` alpha 0.5, localScale 1.35, inactive.
- child `CorruptionOverlay`: SpriteRenderer order 12, noise sprite, gray tint alpha 0.75,
  localScale 1.1, inactive.
- if `isPlatform`: root `BoxCollider2D` size (1.1, 0.95) offset (0, 0.05), `usedByEffector = true`
  + `PlatformEffector2D` `useOneWay = true`, `surfaceArc = 170`.
- if Virus: child `DamageTrigger` CircleCollider2D isTrigger radius 0.75 (layer Default).
- if Accelerator: child `WinTrigger` BoxCollider2D isTrigger size (1.3, 1.2) (layer Default).
Runtime behavior (Awake: capture HomePosition, cache `FindFirstObjectByType` CPUManager /
GameStateManager / PlayerController2D refs, call EnsureVisuals, re-assign procedural sprites):
- Virus: `OnTriggerEnter2D/Stay2D` on the child relays via `GetComponentInParent` — implement
  by checking triggers on the root using `OnTriggerEnter2D`/`OnTriggerStay2D` (child colliders
  deliver messages to the root because trigger GO has no own Rigidbody — messages go to the
  collider's attached body/GO: NO rigidbody on icons, so messages arrive on the CHILD GO.
  Therefore: add a tiny relay the same way GlitchZone works? NO — instead put the trigger
  collider ON THE ROOT itself (CircleCollider2D isTrigger on root for Virus, BoxCollider2D
  isTrigger on root for Accelerator, alongside the platform collider). Static colliders on
  the same GO deliver OnTrigger* to that GO directly.) Player detection:
  `other.CompareTag("Player")`, per-hit cooldown `virusDamageCooldown`:
  `cpuManager.AddCpu(virusCpuSpike)`, `player.ApplyKnockback((playerPos-iconPos).normalized
  * virusKnockback)`, flash Body red 3× (coroutine).
- Accelerator: on player trigger → `gameStateManager.TriggerWin()` (once).
- Corrupted: Body color gray `#888888`, CorruptionOverlay on, canDrag/canInstall behavior
  denied (CursorInteractor checks `IsCorrupted`). Sticky even if glitch recedes.

### 5.9 `CursorInteractor.cs` (MonoBehaviour, on child GO `VirtualCursor` under Player)
```csharp
public float maxRadius = 2.5f;
public bool showRangeDebug = true;
public PlayerController2D player;         // fallback GetComponentInParent
public SoftwareInstallZone installZone;   // fallback FindFirstObjectByType
public Camera worldCamera;                // fallback Camera.main
public GameStateManager gameState;        // fallback find

public Vector2 CursorWorldPos { get; }
public DesktopIcon DraggedIcon { get; }
```
- Awake builds own visual children if missing: `CursorSprite` (SpriteRenderer order 200,
  factory cursor sprite), `RangeRing` (SpriteRenderer order 199, factory ring sprite,
  localScale = maxRadius*2, alpha 0.15, active == showRangeDebug). NOTE: VirtualCursor GO
  itself moves; the ring must stay centered on the player → ring is positioned every
  LateUpdate at player.position, i.e. parent the ring visual under the PLAYER via code
  or counter-move it. Simplest binding choice: in LateUpdate set
  `rangeRing.position = player.transform.position`.
- Update (only when `gameState.State == Playing`): read `Mouse.current.position`,
  `worldCamera.ScreenToWorldPoint`, z=0, clamp into circle of `maxRadius` around player,
  set `transform.position`. All world interaction uses this clamped position.
- Skip ALL world interaction while `EventSystem.current != null &&
  EventSystem.current.IsPointerOverGameObject()` (needs `using UnityEngine.EventSystems;`).
- Press LMB: `Physics2D.OverlapPoint(CursorWorldPos)` (buffer overload; any layer,
  triggers included) → topmost `DesktopIcon` (via `GetComponentInParent`; prefer highest
  Body sortingOrder if multiple, first hit acceptable):
  - if `icon.canDrag && !icon.IsCorrupted && icon.State != Installed` → `icon.BeginDrag()`.
  - else → select it: previous selected icon `SetState(Normal)` (only if its state was
    Selected), this icon `SetState(Selected)` (only from Normal).
  - click on empty space → clear selection.
- Hold LMB while dragging: `DraggedIcon.UpdateDrag(CursorWorldPos)`.
- Release LMB while dragging: install if
  `Vector2.Distance(CursorWorldPos, player.transform.position) <= installZone.Radius
   && DraggedIcon.canInstall && installZone.TryInstall(DraggedIcon)` → `EndDrag(true)`
  else `EndDrag(false)`.

### 5.10 `SoftwareInstallZone.cs` (MonoBehaviour, on child GO `InstallZone` under Player)
```csharp
public float radius = 1.6f;
public SoftwareInventory inventory;       // fallback find
public float Radius { get; }              // == radius
public bool TryInstall(DesktopIcon icon); // icon.softwareData != null && inventory.TryInstall(data)
```

### 5.11 `GlitchBoundsController.cs` (MonoBehaviour) — file also contains `GlitchZone`
```csharp
public CPUManager cpuManager; public PlayerController2D player; public GameStateManager gameState;
public Transform glitchLeft, glitchRight, glitchTop, glitchBottom;
       // builder-wired; Awake fallback: find children named GlitchLeft/... ; if still
       // missing, create them as own children with SpriteRenderer(order 400)+BoxCollider2D(isTrigger)
public float encroachStartCpu = 30f;
public float maxHorizontalEncroach = 3.2f, maxVerticalEncroach = 2.0f;
public float hazardCpuPerSecond = 6f, pushImpulse = 5f;
public float corruptionCheckInterval = 0.5f;
public bool Frozen { get; set; }
public void PlayerTouchedZone(Vector2 pushDirection, bool isEnter); // called by GlitchZone relays
```
- Awake: ensure each zone Transform has a runtime-added `GlitchZone` relay
  (`zone.gameObject.AddComponent<GlitchZone>().controller = this;` if not present) and a
  `PlaceholderVisual` is NOT required — assign noise sprite directly from factory at runtime.
- Zone geometry every frame (world edges ±9.6 / ±5.4, sprite = 1×1 unit → use localScale):
  encroach eH/eV = `Mathf.Lerp(0, max, Mathf.Clamp01((cpu - 30) / 70))`, smoothed with
  `Mathf.MoveTowards(current, target, dt * 1.5f)`. Left zone: scale (eH, 10.8, 1),
  pos (-9.6 + eH/2, 0); right mirrored; top: scale (19.2, eV, 1), pos (0, 5.4 - eV/2);
  bottom mirrored (pos y = -5.4 + eV/2). Hide zone (scale x/y ≥ 0.01 floor) when 0.
  Alpha flicker: every ~0.1 s randomize sprite alpha 0.35–0.6.
- Player contact: GlitchZone relays OnTriggerEnter2D/OnTriggerStay2D (filter
  `other.CompareTag("Player")`) to `PlayerTouchedZone(dir, ...)` with push direction =
  toward screen center along the zone axis (left zone → +x, top zone → -y, ...).
  Controller: touched-this-frame flag; in Update, `cpuManager.SetHazardLoad(touched ?
  hazardCpuPerSecond : 0)` (0 while player.ShieldActive), on Enter apply
  `player.ApplyKnockback(dir * pushImpulse)` (× 2.5 when ShieldActive — shield bounces).
- Corruption sweep every `corruptionCheckInterval`: for each `DesktopIcon.All`, if state
  not Deleted/Dragging and icon center is inside any zone rect → `SetState(Corrupted)`.
- Frozen → stop growth, stop hazard (SetHazardLoad(0) once), keep visuals static.
`GlitchZone` (in same file, runtime-added only, NEVER referenced by the builder):
```csharp
public class GlitchZone : MonoBehaviour {
    public GlitchBoundsController controller; public Vector2 pushDirection;
    // OnTriggerEnter2D / OnTriggerStay2D → controller.PlayerTouchedZone(pushDirection, enter?)
}
```

### 5.12 `SoftwareData.cs` (ScriptableObject) — also holds 3 enums at file scope
```csharp
public enum SoftwareAbilityType { None, SpawnTemporaryIcon, AirDash, Glide, ShieldPush, Accelerator }
public enum SoftwareSideEffectType { None, PopupBlock, InputDelay, InputReverse, ScreenObstruction }
public enum SoftwareState { World, Installed, Running, Corrupted, Deleted }

[CreateAssetMenu(fileName = "SoftwareData", menuName = "CPU100/Software Data")]
public class SoftwareData : ScriptableObject {
    public string softwareName;
    public Sprite icon;                       // null → placeholder by iconType Software
    public SoftwareAbilityType abilityType;
    public SoftwareSideEffectType sideEffectType;
    public float startupCpuCost, runningCpuLoadPerSecond, closeCpuRelief, cooldown;
    public bool canUseRepeatedly = true, isSpecialSoftware;
}
```

### 5.13 `SoftwareRuntimeItem.cs` (plain `[System.Serializable]` class, NOT MonoBehaviour)
```csharp
[System.Serializable]
public class SoftwareRuntimeItem {
    public SoftwareData Data;
    public SoftwareState State = SoftwareState.Installed;
    public float CooldownRemaining;
    public bool IsRunning => State == SoftwareState.Running;
    public SoftwareRuntimeItem(SoftwareData data);
}
```

### 5.14 `SoftwareInventory.cs` (MonoBehaviour)
```csharp
public CPUManager cpuManager;
public const int Capacity = 3;
public SoftwareRuntimeItem[] Slots { get; }      // length 3, null = empty
public int SelectedIndex { get; }                // -1 = none
public SoftwareRuntimeItem SelectedItem { get; } // null-safe
public float TotalRunningLoadPerSecond { get; }  // recomputed on launch/close, cached field
public int RunningCount { get; }
public bool HasFreeSlot { get; }
public event System.Action OnInventoryChanged;   // structural changes only (install/launch/close/select-visual refresh)
public event System.Action<int> OnSelectedChanged;
public bool TryInstall(SoftwareData data);       // first free slot, State=Installed; false if full
public void Select(int index);                   // only non-empty slots
public bool TryLaunch(int index);                // Installed→Running: cpuManager.AddCpu(startupCpuCost), recompute load
public void CloseAndDelete(int index);           // if Running → RelieveCpu(closeCpuRelief); slot = null; permanent
```
Update: tick `CooldownRemaining` down for all items (no events; UI polls cooldowns).

### 5.15 `SoftwareAbilityExecutor.cs` (MonoBehaviour)
```csharp
public SoftwareInventory inventory; public PlayerController2D player;
public CursorInteractor cursor; public GameStateManager gameState;
public InputInterferenceController interference;
public Transform temporaryIconsParent;    // fallback: GameObject.Find("TemporaryIcons") in Start, else create root GO
public float tempIconLifetime = 5f;
public float shieldDuration = 2.5f;
```
Update: `Keyboard.current.eKey.wasPressedThisFrame` && Playing && !interference.InputBlocked
→ item = inventory.SelectedItem; require `item != null && item.IsRunning &&
item.CooldownRemaining <= 0` → switch (item.Data.abilityType):
- SpawnTemporaryIcon: pos = cursor.CursorWorldPos clamped to x∈[-9,9], y∈[-4.6,5.0];
  `DesktopIcon.CreateRuntimeIcon("New Tab.url", DesktopIconType.Shortcut, pos,
  temporaryIconsParent, true, true).ScheduleExpire(tempIconLifetime);`
- AirDash / Glide: `player.AirDash();`
- ShieldPush: `player.ActivateShield(shieldDuration);`
- others: no-op.
Then `item.CooldownRemaining = item.Data.cooldown;`.

### 5.16 `SoftwareTaskbarUI.cs` (MonoBehaviour, on UI GO `DesktopTaskbar`)
```csharp
public SoftwareInventory inventory;
public TaskbarSlotUI[] slots;             // 3; fallback GetComponentsInChildren<TaskbarSlotUI>(true)
public UnityEngine.UI.Text clockText;     // system clock "HH:mm", updated once per second
```
Start: `slots[i].Bind(inventory, i)`. Subscribe inventory events → `Refresh()` all slots.

### 5.17 `TaskbarSlotUI.cs` (MonoBehaviour, implements `IPointerClickHandler`)
```csharp
public UnityEngine.UI.Image iconImage;            // software icon (or placeholder), hidden when empty
public UnityEngine.UI.Text nameText;
public UnityEngine.UI.Image selectionHighlight;   // active == selected
public UnityEngine.UI.Image runningIndicator;     // active == running
public UnityEngine.UI.Image cooldownMask;         // type Filled/Vertical, fillAmount = remaining/cooldown
public UnityEngine.UI.Button closeButton;         // active == occupied; onClick → CloseAndDelete
public void Bind(SoftwareInventory inv, int slotIndex);
public void Refresh();
// OnPointerClick: eventData.clickCount >= 2 → inventory.TryLaunch(index) else inventory.Select(index)
// Update: poll cooldown fill only (no allocs). Empty slot → icon hidden, name "-", mask 0.
// Icon sprite: item.Data.icon != null ? that : PlaceholderSpriteFactory.GetIconSprite(DesktopIconType.Software)
```

### 5.18 `CPUWindowUI.cs` (MonoBehaviour, on UI GO `CPUWindow`)
```csharp
public CPUManager cpuManager; public SoftwareInventory inventory;
public RectTransform windowRoot;          // shaken at HeavyLoad+, base pos cached
public UnityEngine.UI.Text percentText;   // "63%"
public UnityEngine.UI.Image fillBar;      // type Filled/Horizontal; color green→yellow→red by cpu
public UnityEngine.UI.Text stageText;     // Normal/Light Load/Medium Load/Heavy Load/CRITICAL/CRASHED
public UnityEngine.UI.Text runningCountText; // "Processes: 2/3"
public UnityEngine.UI.Image thermometerFill; // Filled/Vertical
```
Subscribes OnCpuChanged/OnCpuStageChanged. Shake amplitude: Heavy 1.5 px, Critical 4 px
(anchoredPosition jitter around cached base, restore below Heavy). At Critical, percentText
flickers red/white a few times per second.

### 5.19 `GameResultUI.cs` (MonoBehaviour, on UI GO under Canvas)
```csharp
public GameObject blueScreenPanel, victoryPanel;      // inactive by default
public UnityEngine.UI.Button restartButtonBlue, restartButtonWin;
public GameStateManager gameState;
public void ShowBlueScreen(); public void ShowVictory(); public void HideAll();
// Start: HideAll(); wire both buttons → gameState.RestartGame()
```

### 5.20 `PlaceholderSpriteFactory.cs` (public static class, NOT MonoBehaviour)
```csharp
public static Sprite GetIconSprite(DesktopIconType type);  // 64×64, PPU 64, cached per type
public static Sprite GetShortcutArrow();                   // 24×24 white arrow in blue box, PPU 24
public static Sprite GetSolid(Color color);                // 8×8, PPU 8, cached per color
public static Sprite GetNoise();                           // 64×64 gray noise + alpha jitter, PPU 64, cached
public static Sprite GetRing();                            // 128×128 circle OUTLINE, PPU 128 (=1 unit), cached
public static Sprite GetCursor();                          // 32×32 arrow pointer, PPU 32, pivot (0.15, 0.9)
public static Sprite GetPlayerSprite();                    // ~44×56 friendly antivirus blob w/ shield emblem, PPU 56
public static Sprite GetWallpaper();                       // 192×108 vertical gradient #1E5B8C→#153F63 + faint grid, PPU 10
public static Color IconColor(DesktopIconType type);
```
- All textures: `FilterMode.Point` (except wallpaper Bilinear), `HideFlags.DontSave`,
  cached in static Dictionaries. Must work in BOTH edit mode and play mode.
- Icons must be visually distinct WITHOUT text (plan §20): base rounded square in
  `IconColor(type)` + 2px darker border + type glyph drawn procedurally:
  Folder `#E8B84B` (folder tab silhouette) · TextFile `#ECECEC` (white page, gray text lines)
  · ImageFile `#7FC97F` (white page, sun+mountain) · Shortcut `#6FA8DC` (globe/arrow)
  · Software `#5B9BD5` (window with title bar) · Virus `#D9534F` (jagged X / skull-ish)
  · RecycleBin `#9AA5B1` (bin trapezoid) · Accelerator `#7BD389` (up arrow / rocket)
  · SystemFile `#B39DDB` (gear square). Keep glyphs simple rect/line/circle fills.

### 5.21 `PlaceholderVisual.cs` (MonoBehaviour)
```csharp
public enum PlaceholderVisualKind { Wallpaper, Noise, Solid, Cursor, Ring, Player } // file scope
public class PlaceholderVisual : MonoBehaviour {
    public PlaceholderVisualKind kind;
    public Color tint = Color.white;
    // Awake: if GetComponent<SpriteRenderer>() → assign factory sprite (always re-assign;
    // procedural sprites don't survive scene save) + color = tint.
    // else if GetComponent<UnityEngine.UI.Image>() → assign sprite + color = tint.
}
```

### 5.22 `CPU100PrototypeSceneBuilder.cs` (static editor class, Assets/CPU100/Scripts/Editor/)
`[MenuItem("Tools/CPU 100/Build Prototype Scene")] public static void Build()`
**Fully idempotent** — running twice produces the same scene with no duplicates. Pattern:
`GetOrCreate(parent, name)` finds child by name else creates; `GetOrAdd<T>(go)` for components.
Duties, in order:
1. Folders: ensure `Assets/CPU100/{Scenes,Scripts,ScriptableObjects,Prefabs,Art,UI}` exist
   (`AssetDatabase.IsValidFolder` / `CreateFolder`).
2. Layers: ensure user layers `Player` and `Platform` exist (TagManager via
   `SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0])`,
   first empty slot index ≥ 8; skip if present).
3. Scene: if `Assets/CPU100/Scenes/CPU100_Prototype.unity` exists → `EditorSceneManager.OpenScene`,
   else `NewScene(EmptySceneSetup? no — NewSceneSetup.EmptyScene, NewSceneMode.Single)` then SaveScene to that path.
   Add to `EditorBuildSettings.scenes` if missing.
4. SoftwareData assets in `Assets/CPU100/ScriptableObjects/` (LoadAssetAtPath else
   CreateInstance+CreateAsset; ALWAYS overwrite field values; SetDirty):
   - `Browser.asset`: softwareName "Browser", SpawnTemporaryIcon, startup 5, load 1.2, relief 6, cooldown 4
   - `PaperPlane.asset`: "Paper Plane", AirDash, startup 8, load 0.8, relief 5, cooldown 2.5
   - `Shield.asset`: "Shield", ShieldPush, startup 12, load 1.5, relief 8, cooldown 6
   (sideEffect None, canUseRepeatedly true, isSpecialSoftware false, icon null)
5. `Main Camera`: MainCamera tag, orthographic size 5.4, pos (0,0,-10), solid bg `#1E5B8C`,
   add `UnityEngine.Rendering.Universal.UniversalAdditionalCameraData` handled automatically by URP — just ensure Camera. AudioListener.
6. Hierarchy exactly as plan §17 (names EXACT):
   `GameRoot/{GameStateManager,CPUManager,SoftwareInventory,SoftwareAbilityExecutor,InputInterferenceController,GlitchBoundsController}`
   — each GO carries its same-named component.
   `DesktopWorld/{Wallpaper, DesktopIcons/<10 icons per §2 table>, TemporaryIcons, Hazards/{GlitchLeft,GlitchRight,GlitchTop,GlitchBottom}, Player}`
   plus `DesktopWorld/{LeftWall,RightWall}` (BoxCollider2D (0.5, 22) at x = ∓9.85).
7. Wallpaper: SpriteRenderer order -100 + PlaceholderVisual(kind=Wallpaper).
8. Icons: GO per §2 table, DesktopIcon component with all config fields + softwareData
   assets wired, position/scale set, then call `icon.EnsureVisuals()`.
9. Glitch zones: each GO = SpriteRenderer(order 400, color (1,1,1,0.45)) +
   PlaceholderVisual(kind=Noise) + BoxCollider2D(isTrigger=true, size 1×1), initial
   localScale (0.01, 10.8, 1) sides / (19.2, 0.01, 1) top+bottom, positioned at edges.
   Do NOT add GlitchZone relay (runtime-only).
10. Player: tag `Player`, layer `Player`, pos (-7.8, -2.3): SpriteRenderer(order 50) +
    PlaceholderVisual(kind=Player), Rigidbody2D (gravityScale 3.5, freezeRotation,
    Continuous, Interpolate), CapsuleCollider2D (size (0.55, 0.8), vertical),
    PlayerController2D. Children: `GroundCheck` (0,-0.45) + PlayerGroundChecker
    (groundMask = Platform layer mask); `InstallZone` + SoftwareInstallZone;
    `VirtualCursor` + CursorInteractor.
11. UI: root `UI` (Canvas ScreenSpaceOverlay + CanvasScaler(1920×1080, match 0.5) +
    GraphicRaycaster) with children (§3 raycast rules, layout in §6 of this doc):
    `DesktopTaskbar` (+SoftwareTaskbarUI), `CPUWindow` (+CPUWindowUI), `GlitchOverlay`,
    `WarningOverlay`, `ResultUI` (+GameResultUI) parenting `BlueScreenPanel`+`VictoryPanel`.
    Separate root GO `EventSystem` (EventSystem + `UnityEngine.InputSystem.UI.InputSystemUIInputModule`).
12. Wire EVERY public ref field listed in §5 across all components (direct field
    assignment, then `EditorUtility.SetDirty` on each touched component).
13. `EditorSceneManager.MarkSceneDirty` + `SaveScene`. Log a completion summary with
    `Debug.Log`.

## 6. UI layout numbers (builder)

Canvas 1920×1080 reference.
- **DesktopTaskbar**: anchor bottom-stretch (min(0,0) max(1,0), pivot(0.5,0)), height 80,
  Image `#1F2430F2`. Children: `StartButton` (left 8, 120×64, Image `#2E3646` + child Text
  "Start"); `SoftwareSlot01/02/03` at x = 150/232/314 (anchored left), 76×64 each;
  `ClockText` (right-anchored, 110×40, right margin 12, Text, alignment MiddleRight).
- **Slot** children (all Images no sprite = plain rects): root Image `#2A3140`;
  `IconImage` 44×44 at top-center y -4; `NameText` bottom, height 16, font 11;
  `SelectionHighlight` full-stretch Image `#FFFFFF2E` (inactive); `RunningIndicator`
  bottom-center 48×4 Image `#6FE3A0` (inactive); `CooldownMask` full-stretch Image
  `#000000A0`, type Filled/Vertical/Bottom + PlaceholderVisual(kind=Solid, tint keeps color);
  `CloseButton` top-right 18×18 Image `#C0392B` + child Text "X" (font 12).
- **CPUWindow**: anchor top-left (min/max (0,1), pivot (0,1)), pos (16,-16), 300×160,
  Image `#141A24F0`. Children: `TitleText` "Task Manager - CPU" top (font 15);
  `PercentText` (font 30, bold) left; `FillBarBg` 200×14 Image `#0B0F16` with child
  `FillBar` full-stretch Filled/Horizontal + PlaceholderVisual(Solid); `StageText`;
  `RunningCountText` (font 13); `ThermoBg` right side 14×90 with child `ThermometerFill`
  Filled/Vertical `#D9534F` + PlaceholderVisual(Solid).
- **GlitchOverlay**: full-stretch Image + PlaceholderVisual(kind=Noise) color (1,1,1,0.25),
  CanvasGroup alpha 0, raycastTarget=false, `blocksRaycasts=false`.
- **WarningOverlay**: inactive GO, centered Text "! INPUT INTERFERENCE !" 600×60 font 30
  bold yellow `#FFD34D`, raycastTarget=false.
- **BlueScreenPanel** (inactive): full-stretch Image `#0B3EA8`; Texts ":(" (font 110),
  "Your PC ran into a problem: too many processes." (font 26),
  "Stop code: CPU_OVERLOAD_100" (font 18, `#BFD4F2`); `RestartButtonBlue` 220×56 Image
  `#FFFFFF` + child Text "Restart" (dark text).
- **VictoryPanel** (inactive): full-stretch Image `#0C6B37E0`; Text "System Repaired"
  (font 64 bold), "System Booster installed successfully." (font 24);
  `RestartButtonWin` 220×56 + child Text "Play Again".

## 7. Behavior cross-reference (who calls whom)

- CursorInteractor → DesktopIcon.BeginDrag/UpdateDrag/EndDrag, SoftwareInstallZone.TryInstall
- SoftwareInstallZone → SoftwareInventory.TryInstall → (icon hidden by CursorInteractor via EndDrag(true))
- TaskbarSlotUI → SoftwareInventory.Select/TryLaunch/CloseAndDelete
- SoftwareAbilityExecutor → DesktopIcon.CreateRuntimeIcon + ScheduleExpire, PlayerController2D.AirDash/ActivateShield
- CPUManager ← reads SoftwareInventory.TotalRunningLoadPerSecond; ← SetHazardLoad from GlitchBoundsController
- GameStateManager ← CPUManager.OnCpuReachedMaximum; ← DesktopIcon(Accelerator).TriggerWin
- InputInterferenceController ← CPUManager.CurrentStage; → read by PlayerController2D + SoftwareAbilityExecutor
- GlitchBoundsController → DesktopIcon.SetState(Corrupted), PlayerController2D.ApplyKnockback, CPUManager.SetHazardLoad

## 8. Definition of done (per module)

Code compiles against this contract with zero errors, uses only APIs defined here for
cross-module calls, follows plan constraints (§21 of buildPlan.txt), and every class is
resilient to missing wiring (Awake fallbacks) so the scene works even if the builder
misses a reference.
