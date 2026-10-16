# Setup guide (offline game)

Simple steps for someone new to Unity. Online services come later: [`ONLINE_SETUP.md`](ONLINE_SETUP.md).

## 1. Install
1. Download **Unity Hub** from unity.com and sign in (free Personal licence).
2. Unity Hub ▸ **Installs ▸ Install Editor** ▸ newest **Unity 6 LTS** (6000.x).
3. Tick **Android Build Support** with **OpenJDK** and **Android SDK & NDK Tools**. On Windows also tick **Visual Studio** (or use VS Code / Rider).

## 2. Get the code
- **Git:** `git clone https://github.com/tekrontech/kitefall.git` then `git checkout claude/amazing-bardeen-xi1xqo`
- **Zip:** download and extract, e.g. `D:\Kitefall`.

## 3. Add Unity's URP mobile settings (one time)
The repo contains the game, not Unity's own project settings (they depend on your exact Unity version).
1. Unity Hub ▸ **New project** ▸ Unity 6 ▸ template **Universal 3D** ▸ name `KitefallTemplate` ▸ Create.
2. When the editor is open, **close Unity**.
3. Copy from `KitefallTemplate` into your Kitefall folder:
   - `Packages/` (folder)
   - `ProjectSettings/` (folder)
   - `Assets/Settings/` **and** `Assets/Settings.meta`
   - if present: `Assets/UniversalRenderPipelineGlobalSettings.asset` and its `.meta`
   - do **not** copy `SampleScene`, `TutorialInfo` or `Readme.asset`
4. You can delete `KitefallTemplate`.

## 4. Open the project
1. Unity Hub ▸ **Add ▸ Add project from disk** ▸ choose the Kitefall folder ▸ open with Unity 6.
2. **Window ▸ Package Manager ▸ Unity Registry**: make sure **Input System** is installed. If Unity asks to enable the new input backends, click **Yes** (Unity restarts).
3. **Edit ▸ Project Settings ▸ Player ▸ Other Settings ▸ Active Input Handling** = *Input System Package (New)* or *Both*.
4. The Console must show no red errors. (Yellow warnings are fine.)

## 5. Build the game scene
Menu bar ▸ **Kitefall ▸ Build Game Scene (complete game)**. This creates:
- all data assets in `Assets/ScriptableObjects` (7 kites, 5 dum, 6 dor, 5 charkhi, 6 tunings, 6 arenas, configs, BalanceConfig, GameDatabase)
- material templates in `Assets/Materials/Runtime`
- the scene `Assets/Scenes/Main.unity` (first in Build Settings) and landscape player settings.

The old milestone-1 flight lab is still there: **Kitefall ▸ Milestone 1 ▸ Build Flight Test Scene**.

## 6. Play
Open `Assets/Scenes/Main.unity` ▸ **Play**. Set the Game view to a landscape size such as 1920×1080 (or *Simulator* ▸ a phone).
Offline mode saves your profile to a file on this computer (Settings ▸ Account ▸ Delete local save to start over).

## 7. Tests
**Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All** → 159 green tests.
Without Unity (needs the .NET 8 SDK): `dotnet test Tools/CoreTests`.

## 8. Android phone
1. Phone: enable *Developer options* and *USB debugging*, connect with USB.
2. **File ▸ Build Profiles ▸ Android ▸ Switch Platform**.
3. **Player Settings**: company name, package name (e.g. `com.tekrontech.kitefall`), *Minimum API Level* 24+, *Scripting Backend* IL2CPP, *Target Architectures* ARM64 (required by Google Play).
4. **Build And Run**.

## 9. Commit to Git
Unity creates `.meta` files next to every asset on first open – they hold the links between assets. Commit them together with `Packages/` and `ProjectSettings/`:
```bash
git add .
git commit -m "Unity project settings, meta files and generated data"
git push
```

## Tuning
All numbers live in `Assets/ScriptableObjects` (select an asset, change values in the Inspector). `BalanceConfig` holds contest extras, sweet zone, round phases, crates, economy, ads caps, ranks and matchmaking. **Kitefall ▸ Data ▸ Reset All Data Assets To Spec Values** restores the spec numbers.

## If something goes wrong
| Problem | Fix |
|---|---|
| Pink objects | URP settings were not copied (step 3) or Build Game Scene was run before them – copy the settings, then run Build Game Scene again. |
| `The type or namespace 'InputSystem' could not be found` | Install the Input System package (step 4.2). |
| No touch on the on-screen buttons | Active Input Handling must include the new Input System (step 4.3). |
| Errors mentioning `Unity.Services…` / `Unity.Netcode…` after installing a package | See ONLINE_SETUP.md ▸ Troubleshooting. Removing that package makes the game compile again (online parts switch off automatically). |
