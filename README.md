# Kitefall

Online competitive 3D kite-fighting for mobile — *"Read the wind, win the cross, cut the line."*

Unity 6 LTS · C# · URP (mobile) · New Input System · Android first (iOS-ready)

The full game design lives in [`docs/KITEFALL_GAME_SPEC.md`](docs/KITEFALL_GAME_SPEC.md). That file is the single source of truth.

---

## Current status: Milestone 1 of 14 ✅

| # | Milestone | Status |
|---|-----------|--------|
| 1 | KiteController + WindField + TensionModel (single kite) | **Done – ready for testing** |
| 2 | Tail rope physics | Next |
| 3–14 | Lines & contact, contests, attacks, dor/charkhi feel, rounds, bots, HUD, online, accounts, menus, ranked, ads | Planned |

What milestone 1 gives you:

- A kite you can steer in every direction inside the "wind window".
- PULL / release (dheel) / REEL with the exact tension rules from spec 9.1 and 9.2 (green / yellow / red zones, self-snap after 0.6 s above dor Strength).
- Line out on the charkhi (release speed, reel speed, capacity, dor stock).
- Deterministic wind (same seed = same wind on every phone and, later, on the server).
- Every number in ScriptableObject data assets (kites, dum, dor, charkhi, configs) – nothing hard-coded.
- A one-click test scene with a placeholder rooftop city, follow camera, debug HUD, tension bar, wind compass and touch controls.
- 63 unit tests for the game rules.

---

## Setup (step by step)

### 1. Install Unity
1. Install **Unity Hub** from unity.com.
2. Unity Hub ▸ **Installs ▸ Install Editor** ▸ choose the newest **Unity 6 LTS** (6000.x LTS).
3. Tick these modules: **Android Build Support** (with *OpenJDK* and *Android SDK & NDK Tools*) and, on Windows, **Visual Studio** (or use JetBrains Rider / VS Code).

### 2. Get the code
Either
- **Git:** `git clone https://github.com/tekrontech/kitefall.git` then `git checkout claude/amazing-bardeen-xi1xqo`
- **Zip:** download the zip and extract it, e.g. to `D:\Kitefall`.

You now have a folder with `Assets/`, `docs/`, `Tools/` and this README.

### 3. Add Unity's URP mobile settings (one time only)
This repo contains only *our* code. Unity's own project settings come from the official template, so they always match your exact Unity version:

1. Unity Hub ▸ **New project** ▸ Unity 6 ▸ template **Universal 3D** ▸ name it `KitefallTemplate` ▸ **Create project**.
2. When the editor has opened, **close Unity**.
3. From the `KitefallTemplate` folder, **copy** these into your `Kitefall` folder:
   - the `Packages` folder
   - the `ProjectSettings` folder
   - `Assets/Settings` **and** the file `Assets/Settings.meta` (into `Kitefall/Assets/`)
   - if the template has `Assets/UniversalRenderPipelineGlobalSettings.asset`, copy it and its `.meta` too
   - do **not** copy `SampleScene`, `TutorialInfo` or `Readme.asset`
4. You can delete `KitefallTemplate` afterwards.

### 4. Open the project
1. Unity Hub ▸ **Add ▸ Add project from disk** ▸ select the `Kitefall` folder ▸ open it with Unity 6.
2. **Window ▸ Package Manager ▸ Unity Registry** ▸ make sure **Input System** is installed (the Unity 6 template normally already has it). If Unity asks *"enable the new input backends?"* click **Yes** – Unity restarts.
3. **Edit ▸ Project Settings ▸ Player ▸ Other Settings ▸ Active Input Handling** must be **Input System Package (New)** or **Both**.
4. The Console should show no red errors.

### 5. Build the test scene and play
1. Menu bar ▸ **Kitefall ▸ Milestone 1 ▸ Build Flight Test Scene**. This creates the data assets (`Assets/ScriptableObjects`), materials, kite mesh and the scene `Assets/Scenes/M1_FlightTest.unity`.
2. Menu ▸ **Kitefall ▸ Setup ▸ Apply Mobile Player Settings** (landscape, product name).
3. Press **Play ▶**. Set the Game view to a landscape phone size, e.g. 1920×1080.

| Action | Keyboard | Gamepad | Touch (on screen) |
|---|---|---|---|
| Steer | WASD or arrow keys | Left stick | Joystick (bottom left) |
| PULL (kheench) | Space (hold) | Right trigger | PULL (bottom right) |
| Release (dheel) | Let go of Space | Let go of trigger | Let go of PULL |
| REEL in | Left Shift (hold) | Left trigger | REEL (above PULL) |
| Reset kite (debug) | Backspace | Select | RESET (top right) |

Then go through the checklist in [`docs/MILESTONE_1.md`](docs/MILESTONE_1.md).

### 6. Run the unit tests
- In Unity: **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All** → all green.
- Without Unity (needs the .NET 8 SDK): `dotnet test Tools/CoreTests`

### 7. Play on an Android phone
1. Phone: enable **Developer options** and **USB debugging**, connect with USB.
2. **File ▸ Build Profiles ▸ Android ▸ Switch Platform**.
3. Make sure `Assets/Scenes/M1_FlightTest.unity` is in the scene list (the builder adds it).
4. **Build And Run**.

### 8. Commit to Git
After the first open Unity creates `.meta` files next to every asset. **Commit them** (they hold the links between assets):

```bash
git add .
git commit -m "Add Unity project settings and meta files"
git push
```

---

## Project layout

```text
Assets/
  Scripts/
    Core/        Pure C# game rules – no UnityEngine (runs on the server later)
      Kite/        KiteFlightSim, KiteSimulation, FlightSettings
      Tension/     TensionModel, LineSpool, LineActionResolver
      Wind/        WindModel, WindSettings
      Data/        Plain stat structs + LoadoutStats (modifier stacking)
      Util/        KMath, SmoothNoise
    Data/        ScriptableObjects: KiteClassData, TailData, DorData, CharkhiData, configs
    Kite/        KiteController (MonoBehaviour), KiteMeshFactory
    Wind/        WindField (MonoBehaviour)
    Input/       KiteInputReader (new Input System)
    Camera/      KiteCameraController
    UI/          TensionBarView, WindCompassView, FlightDebugHud
    DebugTools/  DebugTetherLine (temporary), PerformanceBootstrap
    Editor/      Scene builder, default data, project setup menus
  Tests/EditMode/  Unit tests for Core
  ScriptableObjects/, Scenes/, Materials/, Art/   ← generated by the Kitefall menu
docs/          Spec, milestone checklist, design decisions
Tools/CoreTests/  Runs the Core tests with plain .NET (used by CI)
```

**Rule:** gameplay maths stays in `Core` (plain C#, unit tested). Unity components only read input, call Core and draw the result. This is what lets the server run the exact same rules later (spec 14.5, rule 2: server authority).

More detail: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) · Open questions and assumptions: [`docs/DECISIONS.md`](docs/DECISIONS.md)

## Tuning
Select any asset in `Assets/ScriptableObjects` and change values in the Inspector – even during Play mode for configs. After changing a kite / dum / dor / charkhi during Play, right-click the **KiteController** component ▸ **Rebuild Simulation**. Use **Kitefall ▸ Data ▸ Reset All Data Assets To Spec Values** to go back to the spec numbers.

## Safety & fairness (from the spec)
String cutting is a fictional game mechanic. Dor names are gameplay stats only. No pay-to-win, ever.
