## Table of Contents

- [Why HalfBeat Exists](#why-halfbeat-exists)
- [XR Accessibility Manager](#xr-accessibility-manager)
  - [Core Concept](#core-concept)
  - [Dominant Hand Selection](#dominant-hand-selection)
  - [Control Modes](#control-modes)
  - [Flick Modes (Rotation Behaviour)](#flick-modes-rotation-behaviour)
  - [Input Mapping](#input-mapping)
  - [Joystick Fine-Tuning](#joystick-fine-tuning)
  - [Crossover Detection](#crossover-detection)
  - [Integration Guide for Developers](#integration-guide-for-developers)
- [How the Beat Saber Clone Works](#how-the-beat-saber-clone-works)
  - [Architecture Overview](#architecture-overview)
  - [Scene Flow](#scene-flow)
  - [Beatmap System](#beatmap-system)
  - [Cube Spawning & Movement](#cube-spawning--movement)
  - [Saber Hit Detection & Slicing](#saber-hit-detection--slicing)
  - [Scoring System](#scoring-system)
  - [Data Logging (CSV)](#data-logging-csv)
  - [Menu System](#menu-system)
  - [Editor Tooling](#editor-tooling)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Tech Stack](#tech-stack)
- [License](#license)

---

## Why HalfBeat Exists

Games like Beat Saber require the player to hold two controllers and independently swing two sabers. For people with limb differences, amputations, hemiplegia, or any condition that limits them to a single functional arm, this is a barrier that locks them out of an entire genre of VR experiences.

**HalfBeat solves this** by introducing a software-driven accessibility layer that derives the motion of the second (non-dominant) hand entirely from the single controller the player can operate. The result is full dual-saber gameplay controlled by **one hand**.

---

## XR Accessibility Manager

### Core Concept

`XRAccessibilityManager.cs` is a single, self-contained **MonoBehaviour** (~530 lines) that sits between Unity's XR tracking system and your hand/controller GameObjects. It intercepts the `TrackedPoseDriver` on the non-dominant hand and overrides its position and rotation every frame based on configurable math derived from the dominant hand.

**Key design goals:**

- **Zero gameplay code changes** — attach the script, wire up references, and it works.
- **Runtime mode switching** — players can toggle between control modes mid-game using controller buttons.
- **Portable** — no dependency on Beat Saber–specific code. Works with any Unity XR project that uses `TrackedPoseDriver` (Input System package).

### Dominant Hand Selection

Set in the Inspector before play:

| Value | Behaviour |
|-------|-----------|
| `Right` | Right hand is the active controller. Left hand is driven by the accessibility system. |
| `Left` | Left hand is the active controller. Right hand is driven by the accessibility system. |
| `Both` | Standard two-handed play. All accessibility overrides are disabled. |

### Control Modes

The system supports three distinct control modes, toggled at runtime via controller buttons:

#### 1. Position Mirror (Default — "Unfrozen")

The non-dominant hand's `TrackedPoseDriver` is **disabled**. Every frame, the dominant controller's local position is **reflected** across both the X-axis (horizontal) and Y-axis (vertical, around a configurable `mirrorCenterHeight`). The Z-axis (depth) is preserved.

**Effect:** Moving the dominant hand to the upper-right causes the non-dominant saber to appear in the lower-left. This is ideal for levels that place blocks symmetrically.

```
Non-Dominant.x = -Dominant.x
Non-Dominant.y = 2 × mirrorCenterHeight - Dominant.y
Non-Dominant.z =  Dominant.z
```

#### 2. Flick Fixed ("Freeze Mode")

At the moment of activation, the system **snapshots the positional offset** between the two controllers. From that point on, the non-dominant hand maintains that exact rigid offset from the dominant hand — as if the two controllers were connected by an invisible rod.

**Effect:** The player positions both sabers where they want them, presses the grip trigger to "freeze" the arrangement, and then moves both sabers together as a unit. Pressing grip again returns to Position Mirror.

#### 3. Swap

Both sabers are **re-parented to the dominant controller**. The non-dominant controller's GameObject is completely deactivated. The player holds one controller and swings to hit blocks of both colours.

**Effect:** True single-controller play. The primary button toggles Swap on/off.

### Flick Modes (Rotation Behaviour)

Independent of the control mode, the rotation applied to the non-dominant saber can be toggled between:

| Flick Mode | Behaviour |
|------------|-----------|
| `NoInversion` | Non-dominant saber mirrors the dominant rotation directly — sabers swing in parallel. |
| `BothAxisInversion` | Rotation is flipped 180° around the forward axis — sabers swing in opposite arcs, like opening scissors. |

Toggled at runtime via the **secondary button** on the dominant controller.

### Input Mapping

All inputs are wired through Unity's **Input System** (`InputActionProperty`):

| Button | Action |
|--------|--------|
| **Grip Trigger** | Toggle between Position Mirror ↔ Flick Fixed (freeze/unfreeze) |
| **Primary Button** (A/X) | Toggle Swap mode on/off |
| **Secondary Button** (B/Y) | Toggle flick rotation (NoInversion ↔ BothAxisInversion) |
| **Joystick** | Fine-tune the non-dominant hand's position offset in real time |

### Joystick Fine-Tuning

In both Position Mirror and Flick Fixed modes, the dominant controller's **thumbstick** applies a continuous positional offset to the non-dominant hand. This allows the player to nudge the second saber left/right or up/down for precise placement without changing the base mirroring math.

### Crossover Detection

The system tracks whether the dominant hand has crossed the player's body midline (relative to the XR Origin). While not currently used for mode switching, this lays the groundwork for gesture-based controls (e.g., crossing hands to toggle a mode).

### Integration Guide for Developers

To add one-handed accessibility to **any** Unity XR project:

1. **Copy** `XRAccessibilityManager.cs` into your project's `Scripts/` folder.
2. **Attach** the script to a GameObject in your XR scene (e.g., the XR Origin or a dedicated manager object).
3. **Wire up Inspector references:**
   - `xrOrigin` — your XR Origin transform.
   - `leftTrackedDriver` / `rightTrackedDriver` — the `TrackedPoseDriver` components on each hand controller.
   - `leftSaber` / `rightSaber` — the transforms of the held objects (sabers, hands, tools, etc.) if you need Swap mode.
   - Input Action Properties — bind to your project's Input Action Asset for grip, buttons, and joystick.
4. **Set `dominantHand`** to `Right`, `Left`, or `Both` in the Inspector.
5. **Play.** The system self-initialises in `Start()` and begins mirroring automatically.

No changes to your existing gameplay scripts are required.

---

## How the Beat Saber Clone Works

### Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│                        Unity Scenes                          │
│                                                              │
│  MenuScene                         actualLevel               │
│  ┌──────────────────┐              ┌───────────────────────┐ │
│  │ MenuManager      │──LoadScene──▶│ BeatSpawner           │ │
│  │ GameSession (static)            │ BeatCube (per block)  │ │
│  │ BeatmapRegistry  │              │ BeatSaber (per saber) │ │
│  └──────────────────┘              │ ScoreManager          │ │
│                                    │ EndLevelUI            │ │
│                                    │ XRAccessibilityMgr    │ │
│                                    └───────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

### Scene Flow

1. **MenuScene** — The player enters their name, selects a category (Tutorial or Level), and picks a beatmap from a dynamically populated list.
2. The selection is stored in the static `GameSession` class (no MonoBehaviour — just static fields), and `SceneManager.LoadScene("actualLevel")` is called.
3. **actualLevel** — `BeatSpawner.Start()` reads `GameSession.selectedBeatmap` and begins spawning cubes timed to the music.
4. When all cubes have been resolved (hit or missed), `EndLevelUI` displays a completion screen and auto-returns to `MenuScene` after a configurable delay.

### Beatmap System

Beatmaps are authored as **ScriptableObject assets** (`BeatmapAsset`):

| Field | Purpose |
|-------|---------|
| `levelName` | Display name shown in menus |
| `songInfo` | `AudioClip` for the level's music |
| `beatsPerMinute` | BPM metadata |
| `songOffsetSeconds` | Seconds to delay spawning relative to the audio start |
| `cubeMoveSpeed` | How fast cubes travel toward the player (m/s) |
| `blocks` | Ordered list of `BeatData` structs |

Each `BeatData` entry defines:

| Field | Type | Description |
|-------|------|-------------|
| `cubeName` | `string` | Identifier for logging/analytics |
| `spawnTime` | `float` | The exact second at which the cube spawns |
| `color` | `SaberColor` | `Red` or `Blue` — determines which saber can cut it |
| `position` | `GridPos` | One of 12 grid positions (4 columns × 3 rows) |
| `direction` | `CutDirection` | Required swing direction (`Up`, `Down`, `Left`, `Right`, diagonals, or `Any`) |

Beatmaps are organized into **Tutorials** and **Levels** via a `BeatmapRegistry` ScriptableObject.

### Cube Spawning & Movement

`BeatSpawner` owns the entire lifecycle:

1. **Timing** — Every `Update()`, it checks whether the current audio playback time (or elapsed `Time.time` if no audio) has reached the next block's `spawnTime`.
2. **Positioning** — The `GridPos` enum is decoded into X/Y offsets on a 4×3 grid centred at `customSpawnOrigin` (default: `(0, 1.5, 20)`).
3. **Rotation** — `CutDirection` maps to a Z-axis rotation angle (e.g., `Down` → 180°, `Left` → 90°).
4. **Instantiation** — A `BeatCube` prefab is spawned with the correct material (red/blue), speed, and direction settings.
5. **Movement** — Each `BeatCube` travels along the **negative Z-axis** at `moveSpeed` metres per second in its `Update()`.
6. **Miss detection** — If a cube passes `z < -0.1` without being cut, it notifies `BeatSpawner.OnBlockMissed()`, breaks the combo, and self-destructs.

### Saber Hit Detection & Slicing

`BeatSaber.cs` (the primary saber script) runs a **SphereCast** from the hilt along the blade direction every `LateUpdate()`:

1. **Collision** — A thick SphereCast (`bladeRadius = 0.05m`, `saberLength = 1m`) on the sliceable layer detects cubes.
2. **Colour check** — The cube's `requiredColor` must match the saber's `saberColor`.
3. **Direction check** — The angle between the swing velocity vector and the cube's `-transform.up` must exceed 90° (a generous half-circle tolerance). Blocks with `CutDirection.Any` skip this check.
4. **Slicing** — On a valid hit, **EzySlice** computes a procedural cut along a plane perpendicular to the swing velocity. The two halves receive physics forces and are cleaned up after 2 seconds.
5. **Haptic feedback** — A `HapticImpulsePlayer` fires a 0.1s pulse on impact.
6. **Hit-stop** — `Time.timeScale` drops to 0.1 for 50ms of real time, creating a satisfying "punch" feel.

### Scoring System

`ScoreManager` (singleton) tracks score, combo, and multiplier:

| Component | Maximum | Calculation |
|-----------|---------|-------------|
| **Swing score** | 100 pts | `Clamp01(swingSpeed / 8.0) × 100`, floored at 50 |
| **Accuracy score** | 15 pts | `(1 - Clamp01(distToCenter / 0.3)) × 15` |
| **Total per cut** | 115 pts | Swing + Accuracy, capped at 115 |
| **Multiplier** | 8× | Progresses 1×→2×→4×→8× at combo thresholds `[0, 2, 6, 14]` |
| **Combo break** | — | Halves the current multiplier, resets combo count |

A floating `PointsPopup` TextMeshPro prefab displays the score at the hit point and drifts upward.

### Data Logging (CSV)

After every level, `BeatSpawner.SaveMissLogsToExcel()` writes a **player × cube-name miss matrix** to:

```
Application.persistentDataPath/PlayerMissMatrix.csv
```

The CSV dynamically expands columns as new cube names appear and accumulates data across sessions. This supports research and analytics use cases (e.g., tracking which blocks a participant misses most frequently across trials).

### Menu System

| Component | Role |
|-----------|------|
| `MenuManager` | Drives a 3-step flow: Name Entry → Category Select → Beatmap List → Load Game |
| `GameSession` | Static data carrier — stores participant name, selected beatmap, tutorial flag, and computes a unique session label (e.g., `Alice_tutorial_2`) |
| `BeatmapRegistry` | ScriptableObject with arrays for `tutorials[]` and `levels[]` |
| `EndLevelUI` | Shows "Level Finished" and auto-returns to MenuScene after a delay |
| `ReturnToMenu` | Utility attached to back buttons for manual menu return |

The menu canvas uses **World Space** rendering for VR compatibility.

### Editor Tooling

#### Beatmap Editor Window (`Beat Saber > Beatmap Editor`)

A custom `EditorWindow` for authoring beatmaps without leaving the Unity Editor:

- **Transport controls** — Play/Pause audio preview, scrub timeline, nudge ±1s / ±0.1s / ±0.01s.
- **Paint tools** — Select a cube name, colour (Red/Blue), and cut direction.
- **4×3 interactive grid** — Click a cell to place a block at the current scrub time.
- **Block timeline** — Scrollable list of all placed blocks with inline editing, jump-to, and delete.
- **Spacebar hotkey** — During audio playback, tap Space to drop a block at the default grid position in real time.

#### Menu Scene Setup (`Beat Saber > Setup Menu Scene`)

A one-click editor script (`MenuSceneSetup.cs`) that procedurally generates the entire `MenuScene` UI hierarchy:

- World-space Canvas sized for VR
- Three panels (Name Entry, Category Select, Beatmap List) with TMP text, input fields, and styled buttons
- Scroll view with `VerticalLayoutGroup` for dynamic beatmap button spawning
- Saves a `BeatmapButtonPrefab` for runtime instantiation
- Auto-wires all references on the `MenuManager` component

---

## Project Structure

```
Assets/Scripts/
├── XRAccessibilityManager.cs   # One-handed accessibility controller (the toolkit)
├── BeatSpawner.cs              # Level playback — spawns cubes on beat, tracks hits/misses
├── BeatCube.cs                 # Individual cube — movement, collision, EzySlice cutting, scoring
├── BeatSaber.cs                # Primary saber — SphereCast hit detection, haptics, hit-stop
├── saber.cs                    # Legacy/alternative saber script (EzySlice direct)
├── BeatmapAsset.cs             # ScriptableObject — level data, BeatData struct, enums
├── BeatmapRegistry.cs          # ScriptableObject — categorises beatmaps into tutorials/levels
├── NoteData.cs                 # Legacy note data classes (beat, lane, layer)
├── ScoreManager.cs             # Singleton — score, combo, multiplier, UI updates
├── PointsPopup.cs              # Floating score text at hit point
├── GameSession.cs              # Static session carrier between scenes
├── MenuManager.cs              # Menu scene navigation and beatmap selection
├── EndLevelUI.cs               # Level completion screen + auto-return to menu
├── ReturnToMenu.cs             # Manual "back to menu" button handler
├── Slicable.cs                 # Simple trigger-based slicing (early prototype)
├── CubeMovement.cs             # Deprecated stub
├── Spawner.cs                  # Legacy random spawner (pre-beatmap system)
├── Editor/
│   ├── BeatmapWindow.cs        # Custom EditorWindow for beatmap authoring
│   └── MenuSceneSetup.cs       # One-click menu scene UI generation
└── Materials/                  # Red/Blue cube materials
```

---

## Getting Started

### Prerequisites

- **Unity 2022.3+** (or later LTS) with the following packages:
  - XR Plugin Management
  - XR Interaction Toolkit
  - XR Hands (optional, for hand tracking samples)
  - Input System (new)
  - TextMeshPro
  - [EzySlice](https://github.com/DavidArayan/ezy-slice) (for procedural mesh slicing)

### Setup Steps

1. **Clone** this repository and open in Unity.
2. **Build Settings** — Add `MenuScene` (index 0) and `actualLevel` (index 1) to the scene list.
3. **Create a BeatmapRegistry** — `Create > Beat Saber > Beatmap Registry`. Drag your beatmap assets into the `tutorials` and `levels` arrays.
4. **Create Beatmaps** — Open `Beat Saber > Beatmap Editor`, create a new asset, assign a song, and paint blocks on the timeline.
5. **Accessibility Setup** — In `actualLevel`, attach `XRAccessibilityManager` to a GameObject and wire up the XR Origin, TrackedPoseDrivers, sabers, and input actions.
6. **Play** in the headset or use the XR Device Simulator.

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Engine | Unity 2022.3+ |
| XR Framework | XR Interaction Toolkit, XR Plugin Management |
| Input | Unity Input System (new) with `InputActionProperty` bindings |
| Tracking | `TrackedPoseDriver` (Input System XR) |
| Mesh Slicing | EzySlice |
| UI | TextMeshPro, Unity UI (World Space Canvas) |
| Data Export | CSV via `System.IO.StreamWriter` |
| Haptics | `HapticImpulsePlayer` (XR Interaction Toolkit) |
| Language | C# |

---

## License

This project is provided for educational and research purposes.
]]>
