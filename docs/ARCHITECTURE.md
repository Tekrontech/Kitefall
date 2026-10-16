# Architecture

```text
 Touch / keys ─► KiteInputReader ─┐
                                  ▼
            ┌──────────── IMatchSession ────────────┐
            │ OfflineMatchSession  (offline, host)   │  20 Hz fixed ticks
            │ HostSession          (online host)     │──► MatchSnapshot ──► MatchPresenter (3D) + MatchHudScreen (UI)
            │ ClientSession        (online client)   │
            └───────────────┬────────────────────────┘
                            ▼
      ┌──────────────── Kitefall.Core (pure C#, no UnityEngine) ────────────────┐
      │ MatchSimulation: phases, rounds, modes, crates, storm, disconnects       │
      │   ├─ KiteSimulation ×N  (LineActionResolver, TensionModel, LineSpool,    │
      │   │                       KiteFlightSim, line points, dive)              │
      │   ├─ ContactDetector  ─► Contest ─► ContestResolver (spec 9.5)           │
      │   ├─ WindModel + RoundPhaseController                                    │
      │   └─ ArenaLayout (deterministic city / coast / …)                        │
      │ BotBrain · ResultsExplainer · NetCodec · MatchResultDto                   │
      │ EconomyService / EconomyCommands · RankRules · AdsPolicy · Catalog       │
      └──────────────────────────────────────────────────────────────────────────┘
                            ▲ same source files
      Server/KitefallEconomy (Cloud Code module) — GetProfile · Run · ReportMatch
```

## Assemblies
| Assembly | Folder | Depends on | Purpose |
|---|---|---|---|
| `Kitefall.Core` | `Assets/Scripts/Core` | nothing | All game rules; deterministic; server-ready |
| `Kitefall.Runtime` | `Assets/Scripts` | Core, Input System, uGUI | Game app, views, UI, services, offline backend |
| `Kitefall.Online` | `Assets/Scripts/Online` | Core, Runtime, UGS/NGO/LevelPlay/IAP (optional) | Online implementations, registered at start-up |
| `Kitefall.Editor` | `Assets/Scripts/Editor` | Core, Runtime | Menus and scene/data builders |
| `Kitefall.Tests.EditMode` | `Assets/Tests/EditMode` | Core | Unit tests |

The Runtime only knows interfaces (`IBackendService`, `IAdsProvider`, `IPurchaseProvider`, `IAnalyticsProvider`, `IRemoteConfigProvider`, `IOnlineMatchmaker`). `Kitefall.Online` fills the registries when its packages exist, otherwise the local / debug versions are used.

## Determinism and authority
- One seed per match drives wind, arena, sweet-zone phases, contest noise, crates and bots (`DeterministicRandom`, never `UnityEngine.Random` in rules).
- Same seed + same inputs = same match (tested).
- Only the simulation owner (offline device, online host, dedicated server) steps `MatchSimulation`; everyone else draws `MatchSnapshot`s.
- Player data changes only through `EconomyCommands.Execute` / `EconomyService.ApplyMatchResult` – locally in offline mode, in the Cloud Code module online.

## Data flow
ScriptableObjects → `GameDatabase.ApplyToCore()` registers stats in `SpecDefaults` → `MatchSetup` (built by `AppFlow` or the host) → `MatchSimulation`.
`BalanceConfig` (+ Remote Config JSON) feeds contest, phases, crates, economy, ads, ranks, matchmaking.

## UI
All UI is created from code (`UI/UiKit`), so there are no prefabs to wire. Screens derive from `UIScreen` / `MenuScreen` and are pushed on `UIManager`. The HUD reads the session's snapshot each frame.

## Coordinates
World +Y up, ground y = 0, downwind ≈ +Z. Wind direction = where the wind blows **to**, degrees clockwise from +Z. Kites live at (azimuth, elevation, radius) around their flyer's hands; heading 0 = up, +90 = right.

## Upgrade v1.4 additions
- **Core (pure C#, tested):** `Arena/RoofGraph` (roof network + traits), `Arena/RoofClaims` (selection rules), `Arena/WaitingArea` (+ `CharacterMotion`),
  `Match/LaunchPrepController` (ritual state machine), `Match/MatchSimulation.Upgrade` (Warmup / RoofSelect / Prep phases, pack-up, traversal,
  commands, trait effects, training wind), `Match/FeedEntry`, `Match/SpectateRules`, `Meta/CharacterCatalog`, `Meta/SocialRules`, `Meta/PartyRules`.
- **Player commands** ride inside `KiteInput` (`Command`, `CommandArg`, `CommandSeq`) so no new network message is needed; the host runs each sequence number once.
- **Snapshot v2** carries the character state (position, yaw, speed, flyer state, prep step + timestamps, traversal progress) – clients animate from it.
- **Views:** `Match/Characters/*` (generated humanoid, procedural IK animation, GPU-skinned bake, NPC helper), `Match/PropFactory`, `Match/ProceduralTextures`,
  `Shaders/*`, `Graphics/UrpGraphics` (optional assembly, URP only).
- **UI:** `UI/Hud/MatchHudScreen.Upgrade` (feed rows, prep widgets, roof map, hop panel, spectate, emote wheel, quick chat, training panel), `UI/Screens/UpgradeScreens`
  (Wardrobe, Squad lobby, Training), `UI/Icons` (vector icons drawn in code).
- **Services:** `PartyService` (local + `Online/OnlinePartyService`), `VoiceRegistry` (+ `Online/VivoxVoiceService`), `KitefallGraphics`.
