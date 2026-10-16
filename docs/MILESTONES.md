# Milestones (spec section 16)

Verification levels used below:
- **Tested** – covered by unit tests in `Assets/Tests/EditMode` (159 tests, all passing with `dotnet test Tools/CoreTests`), plus bot-vs-bot match simulations.
- **Compiled** – Unity code compile-checked against Unity reference assemblies and stubs of uGUI / Input System; not yet run inside the Unity editor.
- **Stub-compiled** – written from the package docs and compile-checked against hand-written API stubs only.
- **Package-compiled** – compiled against the real NuGet packages.

> The spec says *"do not start milestone 10 until 5 testers ask for one more match against bots"*. You asked for everything at once, so online code exists already. The gate still makes sense **before launch**: play-test the offline duels first.

| # | Milestone | What was built | Main files | Verified |
|---|---|---|---|---|
| 1 | Kite + wind + tension | Wind-window flight, tension 9.1/9.2, self-snap, line spool, deterministic wind | `Core/Kite`, `Core/Tension`, `Core/Wind`, `Kite/KiteController` | Tested |
| 2 | Tail rope physics | Verlet dum rope (6/10/14 segments by quality), reacts to turns, dives, gusts; ribbons/tassel/long tail visuals | `Core/Kite/TailRope`, `Match/KiteView` | Tested |
| 3 | Lines + ContactDetector | Sagging line shape shared by server and client, 3D segment distance at 20 Hz, sweep-crossing detection, approach ring, cooldown | `Core/Combat/LineGeometry`, `ContactDetector` | Tested |
| 4 | ContestResolver + meter | Full 9.5 formula incl. worked example, outcomes, late wins, neutral, durability loss; contest meter UI | `Core/Combat/ContestResolver`, `UI/Hud/HudWidgets` | Tested |
| 5 | Attacks, counters, sweet zone | Kheench / Dheel / Dive, 0.3 s lock, counter triangle, Lachakdaar 1.20, moving sweet zone, Brake floor 55, dive manoeuvre, haptic pulse | `Core/Combat/AttackType`, `SweetZone`, `Match/MatchSimulation` | Tested |
| 6 | Dor / charkhi data | All 6 dor, 5 charkhi, 7 kite classes, 5 dum, 6 tunings as data; modifier stacking in one place | `Core/Data`, `Data/*`, `Editor/KitefallDefaultData` | Tested |
| 7 | Round phases + best of 5 | Calm/Gust/Storm, gusts 15–20 s with warning, storm power and sweet-zone penalty, birds / sea gust events, launch with perfect window, loadout break, timer tiebreak; FFA, Private, Team (captain), Last Kite Flying (storm zone), crates | `Core/Match/*` | Tested |
| 8 | BotBrain | Patrol / Hunt / Attack / Retreat, sweet-zone accuracy 50/70/85 %, counter picking, counter builds (7.5), labelled bots | `Core/Bots` | Tested (hard beats easy in tests) |
| 9 | HUD + results + ledger | Full HUD (spec 10), touch controls + layout editing, results with "why", rewards ledger, slow-motion replay of the cut | `UI/Hud`, `UI/Screens/MatchScreens`, `Core/Combat/ResultsExplainer` | Compiled (explainer Tested) |
| 10 | Online | Multiplayer Services sessions + Relay, host-authoritative simulation, 20 Hz inputs/snapshots, reconnects, dedicated server mode | `Online/SessionMatchmaker`, `Online/NetworkMatch`, `Online/DedicatedServer`, `Core/Match/NetCodec` | Stub-compiled (codec Tested) |
| 11 | Accounts + server inventory | Guest/Google/Apple + linking, Cloud Code module on protected Cloud Save, match reports with validation, leaderboards | `Online/UgsBackendService`, `Server/KitefallEconomy` | Package-compiled (module) / Stub-compiled (client); rules Tested |
| 12 | Home, Hangar, Workshop, tuning, Settings | Every screen of spec 4, onboarding flow 3.1, tutorial, daily login, missions, inbox, friends, profile | `UI/Screens/*`, `App/AppFlow`, `App/TutorialDirector` | Compiled (rules Tested) |
| 13 | Matchmaking, Ranked, quit penalty | Region + tier rules, widening, bot fill, ranked unlock, rank points, streaks, quit penalty & bans, ranked queue | `Core/Meta/RankRules`, `MatchmakingRules`, `Online/SessionMatchmaker` | Tested / Stub-compiled |
| 14 | Ads, Shop, Season Pass, clip share | Ads policy 13.x, LevelPlay provider, IAP v5 provider, cosmetic shop, 50-level pass, rewarded placements, share (screenshot + text) | `Core/Meta/AdsPolicy`, `Catalog`, `Online/LevelPlayAdsProvider`, `Online/UnityIapProvider`, `Services/ShareService` | Tested (policy) / Stub-compiled (SDKs) |

## What still needs you (cannot be done from code alone)
1. Open the project in Unity 6 and fix anything the real editor reports (see SETUP.md).
2. Play-test with real people (spec gate before milestone 10, balance targets 7.6).
3. Unity Cloud project, Cloud Code deploy, leaderboard, matchmaker queue (ONLINE_SETUP.md).
4. LevelPlay app keys, Google Play / App Store accounts and product ids, store receipt validation.
5. Google Play Games / Apple sign-in plugins.
6. A host for the Ranked dedicated server.
7. Real art, sound and music (current ones are generated placeholders), localisation (Urdu, Hindi, Portuguese, Indonesian, Japanese).
8. Store listing, privacy policy and terms pages (URLs in BalanceConfig), trademark check for the name.
