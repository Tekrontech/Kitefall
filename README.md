# Kitefall

Online competitive 3D kite-fighting for mobile – *"Read the wind, win the cross, cut the line."*

Unity 6 LTS · C# · URP (mobile) · New Input System · Android first (iOS-ready) · Unity Gaming Services

The game design is in [`docs/KITEFALL_GAME_SPEC.md`](docs/KITEFALL_GAME_SPEC.md) (single source of truth).

---

## What is in this repository

| Area | Status |
|---|---|
| **All 14 milestones** of spec section 16 | Code complete – see [`docs/MILESTONES.md`](docs/MILESTONES.md) for what each one contains and how it was verified |
| **Offline game** (tutorial, duels and FFA vs labelled bots, every menu, local economy, test ads/store) | Playable in the editor and on a phone right after setup |
| **Online** (accounts, server economy, leaderboards, online matches, ads, IAP) | Code complete; needs **your** Unity Cloud project, ad and store accounts → [`docs/ONLINE_SETUP.md`](docs/ONLINE_SETUP.md) |
| **Tests** | 159 unit tests for the game rules (run in Unity or with `dotnet test Tools/CoreTests`) |
| **Server** | Cloud Code C# module `Server/KitefallEconomy` – the same rules run on the server |

Art and audio are original placeholders generated in code (kite shapes per class, skin patterns, rooftop cities, procedural sounds and music). Replace them with real assets whenever you like – nothing depends on files.

---

## Quick start (offline game, ~15 minutes)

Full step-by-step guide with screenshots-in-words: **[`docs/SETUP.md`](docs/SETUP.md)**. Short version:

Poori guide Roman Urdu mein (har cheez, shuru se Play Store tak): **[`docs/COMPLETE_SETUP_GUIDE.md`](docs/COMPLETE_SETUP_GUIDE.md)**

1. Install **Unity 6 LTS** with **Android Build Support** (Unity Hub ▸ Installs).
2. Get the code (git clone, or download the zip and extract).
3. Create a throw-away project from the **Universal 3D** template, close Unity, copy its `Packages/`, `ProjectSettings/` and `Assets/Settings/` (+ `Settings.meta`) into the Kitefall folder.
4. Unity Hub ▸ **Add project from disk** ▸ Kitefall. Make sure the **Input System** package is installed (answer **Yes** to the restart question).
5. Menu **Kitefall ▸ Build Game Scene (complete game)**.
6. Open `Assets/Scenes/Main.unity` and press **Play** ▶.

You go through: splash → login (Guest) → terms/age → name & country → 2-minute tutorial → first duel vs a practice opponent → Home.

| Action | Keyboard | Touch |
|---|---|---|
| Steer | WASD / arrows | Joystick (bottom left) |
| PULL (kheench) | hold Space | PULL |
| Release (dheel) | let go of Space | let go of PULL |
| REEL in | hold Left Shift | REEL |
| DIVE (ghotta) | Q / Left Ctrl | DIVE, or swipe down on the right |
| Menu / back | Esc | II button / Android back |

Run the tests: **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All** (or `dotnet test Tools/CoreTests`).

---

## How it is built

```text
Assets/Scripts/
  Core/        Pure C# rules – no UnityEngine. Flight, tension, wind, tail rope, lines & contact,
               contest formula, attacks, rounds, all modes, bots, results "why", economy, ranks,
               ads policy, season pass, catalog, network codec. Runs unchanged on the server.
  Data/        ScriptableObjects (kites, dum, dor, charkhi, tunings, arenas, configs)
  App/         GameApp bootstrap, GameDatabase, AppFlow (player journey), tutorial
  Match/       Match sessions (offline / online), presenter, arena builder, kite/line/tail views,
               camera, effects, replay, showroom
  UI/          UI kit (built from code), HUD, every screen
  Services/    Backend interface + local backend, settings, audio, haptics, ads, IAP, analytics…
  Online/      Unity Gaming Services, Netcode/Relay sessions, LevelPlay, IAP, dedicated server
               (each part compiles only when its package is installed)
  Editor/      Kitefall menus: build scenes, create/reset data
Server/KitefallEconomy/   Cloud Code module (server economy, match reports, leaderboards)
Tools/CoreTests/          Runs the Core tests with plain .NET (also in GitHub Actions)
docs/                     Spec, setup, online setup, milestones, decisions, architecture, testing
```

Details: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) · Assumptions and open questions: [`docs/DECISIONS.md`](docs/DECISIONS.md) · Play-test checklist: [`docs/TESTING.md`](docs/TESTING.md)

## Non-negotiable rules (spec section 0) – how the code keeps them
- **No pay-to-win:** the shop and pass only contain cosmetics; rarity never changes stats (`Core/Meta/Catalog.cs`).
- **Server authority:** every profile change goes through `EconomyCommands`; online it runs only in the Cloud Code module on protected Cloud Save data. Matches are simulated by the host / server; clients only send inputs.
- **Explainable combat:** every contest keeps its factors; the results screen explains wins and losses. Contest noise is < 5 %.
- **Safety:** string cutting is fictional; the terms screen says so. Dor names are gameplay stats only.
- **Original assets:** everything visual and audio is generated in code.
- **No ads during gameplay:** `Core/Meta/AdsPolicy.cs` blocks ads in match, launch, loadout break, contest, matchmaking, loading and app open.
