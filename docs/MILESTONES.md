# Milestones (spec section 16)

Verification levels used below:
- **Tested** – covered by unit tests in `Assets/Tests/EditMode` (202 tests, all passing with `dotnet test Tools/CoreTests`), plus bot-vs-bot match simulations.
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

## Upgrade v1.4 – milestones 15–28 (`docs/KITEFALL_UPGRADE_SPEC.md`)

> The upgrade spec asks for one milestone at a time with a test in Unity after each. You asked for the whole upgrade, so all of it is in the code now – **test it in the order below** (docs/UPGRADE_TEST_GUIDE.md) and commit after each step that works.

| # | Milestone | What was built | Main files | Verified |
|---|---|---|---|---|
| 15 | Real online smoke test | Checklist for 2 Android phones + fixes that are possible without devices (commands in inputs, setup flags in the start message, snapshot v2) | `docs/ONLINE_SMOKE_TEST.md`, `Online/NetworkMatch` | **Needs your 2 phones** (codec Tested) |
| 16 | W1 characters | Code-generated humanoids (body, face, hair, garments, shoes, accessories, charkhi), procedural animation with two-bone IK, line starts at the left hand (≤ 1.5 m visual offset), name tags, fallback flyer, GPU-skinned bake | `Match/Characters/*`, `Match/KiteView`, `Match/MatchPresenter` | Compiled |
| 17 | W2 shoulder camera | `CameraMode.Shoulder` (behind-right, kite upper-right), FOV widening to 70°, auto KiteCam on approach/contest and back 1.5 s later, settings Auto / Shoulder only / Kite only, HUD camera button (10 s) | `Match/MatchCamera`, `Match/MatchPresenter`, `Services/SettingsService` | Compiled |
| 18 | F1 characters & outfits | 10 characters, 12 outfits, 12 accessories (4 slots), 12 emotes as data; server-checked equip/buy; Wardrobe with 3D turntable + try-on; Home shows your character flying the kite; Shop ▸ Outfits | `Core/Meta/CharacterCatalog`, `Core/Economy/*`, `UI/Screens/UpgradeScreens`, `Match/Showroom` | Tested (rules) / Compiled (UI) |
| 19 | F4 kill feed + F5 spectate | Feed rows with avatars, attack + dor icons, own/team/enemy colours, 5 rows × 5 s, Double/Triple cut banners; spectate the player who cut you, ◀ ▶, teammates first, reactions, LKF "Leave (keep rewards)" | `Core/Match/FeedEntry`, `Core/Match/SpectateRules`, `UI/Hud/MatchHudScreen.Upgrade` | Tested (rules) / Compiled (UI) |
| 20 | F6 emotes | Emote wheel (hold-drag-release or tap), 8 slots set in the Wardrobe, 3 s cooldown + 10/min, never during an active contest, upper body only while flying, auto win/lose emotes; network via the input command channel | `Core/Meta/SocialRules`, `Core/Match/MatchSimulation.Upgrade`, `Match/Characters/CharacterView` | Tested (rules) / Compiled |
| 21 | F8 training ground | `GameMode.Training`: every item unlocked, target dummies (static/slow/fast at 3 heights), sparring bots, wind/gust/phase controls, practice overlay (sweet zone %, last contest factors), no rewards | `Core/Match/MatchFactory.TrainingSetup`, `UI/Screens/UpgradeScreens`, HUD training panel | Tested (rules) / Compiled |
| 22 | F2 squad lobby | Party of 4 (leader, ready ticks, mode, PLAY), code / share link, party = same team in Team Battle, duels need party of 1; offline local party, online Multiplayer Services session | `Services/PartyService`, `Core/Meta/PartyRules`, `Online/OnlinePartyService`, `UI/Screens/UpgradeScreens` | Tested (rules) / Stub-compiled (online) |
| 23 | Movement + roof graph | Roof network per arena (connected, ≤ 18 m links, 2.5 roofs/player, mirror for duels), planks / rope bridges / ladders / stairs / jumps, server-timed traversal, walking with guard rails | `Core/Arena/RoofGraph`, `Core/Arena/WaitingArea`, `Match/PropFactory`, `Match/ArenaBuilder` | Tested |
| 24 | F3 warmup rooftop | Waiting plaza (3–5 roofs + bridges, dummy kites, flags, lights), walking + emotes + quick chat, "Match starts in 0:12 · 7/8", no dor/kite used | `Core/Match/MatchSimulation.Upgrade`, `ArenaBuilder.BuildWaitingArea` | Tested (sim) / Compiled |
| 25 | F9 roof choice + hopping | RoofSelect phase (15 s / 8 s duels) with map, traits on tap, first claim wins, auto-assign; mirrored pairs for duels; pack up → move → short prep; 25 s grounded rule; traits High/Sheltered/Upwind/Crate spot/Edge/Cables; bot roof decisions | `Core/Arena/RoofClaims`, `MatchSimulation.Upgrade`, HUD roof map + hop panel | Tested (sim); trait balance see D51 |
| 26 | F10 launch ritual | Pick kite → kanni (timing ring) → tie line → toss (hold/release, NPC or teammate helper) → tunke (rhythm) → climb; short prep 8–12 s; cable snag + untangle; auto-prep; results drive start height (cap +20 m) and kanni drift | `Core/Match/LaunchPrepController`, `Match/Characters/HelperNpc`, HUD prep widgets | Tested (rules, ≤ 25 s / ≤ 12 s) / Compiled |
| 27 | F7 quick chat + voice | 8 presets with icons (team only in Team Battle, 1 per 2 s, setting to hide), bubbles over characters; Vivox party + team channels, push-to-talk, under-13 off | `Core/Meta/SocialRules`, `Online/VivoxVoiceService`, HUD | Tested (rules) / Stub-compiled (Vivox) |
| 28 | Graphics pass | Gradient sky + clouds, kite paper shader, procedural textures, modelled houses / roofs / props, static batching, skinned characters, URP post-processing by tier, character detail LOD | `Shaders/*`, `Match/PropFactory`, `Match/ProceduralTextures`, `Graphics/UrpGraphics` | Compiled – **must be profiled on a low-end phone** |

### Still open after the upgrade
- Milestone 15 needs two real phones; 22, 24 (online), 27 must be tested with 2–4 devices.
- Upwind roof balance (D51), online warmup during room fill (D48), practice kite in warmup (D49), teamwork XP (D54).
- Performance targets (≤ 150 draw calls, 60 FPS mid-range, 30 FPS on 3 GB) need the Unity Profiler on a device.

## Audit v7 fixes (21 Oct 2026, `KITEFALL_AUDIT_v7.md` section 6)
No new content was added; everything below fixes or prepares existing content.

| Item | What changed | Where | Status |
|---|---|---|---|
| A1 | Main-scene link always refreshes the GameDatabase (sky / kite-paper materials in APKs) | `Editor/GameSceneBuilder.LinkMainScene` | Done (editor) |
| B1 | Sooti only used in Ranked / Last Kite Flying; automatic daily 300 m; low stock offers a one-tap refill, never blocks; 30-match new-profile test | `Core/Economy/EconomyService`, `App/AppFlow`, `Assets/Tests/EditMode` | Tested |
| E1/E2 | Own-kite marker + off-screen edge arrow, rival/teammate markers (colour-blind aware), kite drawn bigger with distance (≤ ×3, visual only), line ≥ ~2 px | `UI/Hud/MatchHudScreen.Markers`, `Match/KiteView` | Compiled – needs screenshots |
| F1 | "Line was cut" 2 s + fade before the spectate bar; HUD zones re-laid (kill feed top-left, buttons under radar, tension bar right edge); white text on tinted rows | `UI/Hud/*`, `App/AppFlow` | Compiled – check 16:9 / 19.5:9 / 20:9 |
| C1 | Neck + head clamp (35° pitch, 45° yaw); whole-body kneel in the kanni step | `Match/Characters/CharacterView` | Compiled |
| D1 | Grey cube silhouettes removed; non-town arenas get hills / ridges / tree lines in the fog | `Match/ArenaBuilder` | Compiled |
| R4 | Character FBX imported as Humanoid; animation files copy rookie_f's avatar | `Editor/CharacterModelSetup` | Compiled (needs FBX files) |
| R2/R3 | Shared clips in `Assets/Art/Characters/Anim/` incl. pull, release, reel, tie, toss, tug; Humanoid clips play through a manual PlayableGraph; fly_hold + IK when a clip is missing | `Match/Characters/CharacterModel`, `CharacterView.Model` | Compiled (needs Mixamo files) |
| R1 | Code-built GLBs are skipped by Setup Models (in-code body used); rookie_f kept. The 18 files are still in the folder – move them out only when you agree | `Editor/CharacterModelSetup.IsCodeBuiltGlb` | Done |
| R6 | Sounds / music load from `Resources/KitefallAudio/<name>`, generated sound only as fallback; home + match music | `Services/AudioService`, `docs/AUDIO_LIST.md` | Compiled (needs audio files) |
| R5 | World models from `Assets/Art/World/` (houses, parapet, tank, charpai, railing, ladder, plank, stairwell …) with generated fallback | `Match/WorldAssetLibrary`, `Editor/WorldAssetSetup`, `docs/WORLD_ASSET_LIST.md` | Compiled (needs models) |
| R9 | One clear error + red DEV banner on Home when URP is not the active pipeline | `Services/KitefallGraphics`, `UI/Screens/HomeScreen` | Compiled |
| R7 | Launch scope: 2 arenas (Old City Rooftops, River Festival), 3 kites (guddi, rokkaku, pipa), 4 characters (rookie_f, rookie_m, zara, rafay); the rest hidden, not deleted (Settings ▸ Developer ▸ Coming soon) | `Core/Meta/LaunchContent` | Tested |
| O3 | Silent fallbacks now log a warning with the reason | `MatchPresenter`, `CharacterModel`, `HapticsService`, `OnlinePartyService` | Done |

## What still needs you (cannot be done from code alone)
1. Open the project in Unity 6 and fix anything the real editor reports (see SETUP.md).
2. Play-test with real people (spec gate before milestone 10, balance targets 7.6).
3. Unity Cloud project, Cloud Code deploy, leaderboard, matchmaker queue (ONLINE_SETUP.md).
4. LevelPlay app keys, Google Play / App Store accounts and product ids, store receipt validation.
5. Google Play Games / Apple sign-in plugins.
6. A host for the Ranked dedicated server.
7. Real art, sound and music (current ones are generated placeholders), localisation (Urdu, Hindi, Portuguese, Indonesian, Japanese).
8. Store listing, privacy policy and terms pages (URLs in BalanceConfig), trademark check for the name.
