# Design decisions & open questions

Spec rule 17.7: *"If something is unclear, contradictory or risky, say so and ask."* These are the places where the code had to choose something the spec does not fix. Every number is in a data asset (mostly **BalanceConfig**), so any choice can be changed without code.

## Flight & tension (milestone 1)
| # | Topic | Spec | Implemented |
|---|---|---|---|
| D1 | Idle drift speed | "drifts toward 40" | 20 points/s (CombatConfig) |
| D2 | REEL and tension | not defined | Reel = idle drift |
| D3 | When dheel stops | "release = dheel", "REEL cancels Dheel" | Letting go of PULL starts dheel until REEL, PULL or empty charkhi (switchable) |
| D4 | Which rises get multipliers | "rises 10 % faster · then dor/charkhi multipliers" | Dor + strong wind on every rise; charkhi pull-rise only when pulling; charkhi release-drop only when releasing |
| D5 | Strong wind threshold | – | ≥ 9 m/s |
| D6 | Turn rate numbers | Fast / Medium / Slow | 280 / 240 / 180 / 130 / 100 °/s |
| D7 | Flight model | – | Arcade wind-window sphere; nose steering; pull faster; dheel slower, sinks, flutters |
| D8 | Zone edges | 0–60 / 60–85 / 85–100 | 60 is yellow, 85 is red |
| D11 | Kite size | – | Drawn 1.6× larger than life to read on phones |

## Combat (milestones 3–5)
| # | Topic | Implemented |
|---|---|---|
| D14 | Contact at 20 Hz | Lines can pass through each other between ticks, so a contact also counts when the closest-approach direction flips while both lines are within 4 m (`ContactSettings.sweepRange`). |
| D15 | Dheel Cut from idle | Dheel locks only if your line is already releasing at contact (spec: "Release + no reel at contact"). From idle you get Kheench (pull), Dive (DIVE or steer-down + pull) or Hold. |
| D16 | Wind factor | +5 % for the kite on the upwind side, −5 % downwind; during a gust the upwind side gets ×1.08 ("contesting with the gust"). |
| D17 | Angle factor | Uses the angle between the two line segments; < 30° is also checked for nearly-opposite lines (> 150°). |
| D18 | Durability per contest | 3–8 % by time spent in the red; Dheel = 3 %; neutral = 5 % (spec). Quick Duel (no kite loss): a cut kite also loses 10 %. |
| D19 | Cut line loss | Dor lost = line out beyond the contact point. |
| D20 | Dor consumption | 25 % of the longest line flown, clamped to 60–150 m per match, only in modes with kite/dor loss. |
| D21 | Crash | Ground and buildings (arena blocks) count; −15 % durability; 0 % = kite destroyed. |
| D22 | Sweet zone motion | Centre moves between 38 and 80 over 2.6 s; storm −3 width. |

## Modes (milestone 7)
| # | Topic | Implemented |
|---|---|---|
| D23 | Phases in long modes | FFA / Team / LKF repeat Calm → Gust → Storm every 3 minutes. |
| D24 | Launch perfect window | Hold 1.2–1.6 s then release = +10 m; holding past 2.4 s overfills. |
| D25 | Team captain | First player of each team is captain (cut worth 2). Team mates cannot contest each other. |
| D26 | Last Kite Flying size | Spec 20 players; default 12 (offline CPU and online room size). Storm radius 420 m shrinking ×0.75 every 2 min, −5 %/s outside. |
| D27 | Private room | No kite/dor loss and no durability loss. |
| D28 | Draw rounds | No point; play continues; safety cap 9 rounds. |

## Economy & meta (milestones 11–14)
| # | Topic | Implemented |
|---|---|---|
| D29 | Crafting / repair costs | Kite: 3 sail + 2 sticks + 200 coins · Repair: 3 coins per missing % · Spool: 200 m for (2 + level/5) line parts + 80 coins · Charkhi: 3 wood + 2 gears + 250 coins · Salvage: 3 parts |
| D30 | Kite unlock levels | Launch kites level 1; Pakpao 6, Gudiparan 12, Chula 16, Wau 22 ("Update" classes) |
| D31 | Player level | XP per level = 400 + 150 × (level − 1); win 120 XP, loss 60 XP |
| D32 | Rank tiers | Bronze 0, Silver 300, Gold 700, Platinum 1200, Diamond 1800, Master 2500, Sky Legend = Master + global top 500 |
| D33 | Win streak bonus | "+5 per win (max 5)" read as +5 on every win from the 3rd in a row |
| D34 | Tunings | Percent modifiers add (Waxed coat + Double twist = DorSpeed ±0). |
| D35 | Season pass | 1000 XP per level; premium 950 gems or store purchase; contents generated (cosmetics, gems, parts) |
| D36 | Daily login / missions / chest | 7-day cycle (coins, parts, gems); 3 daily missions from 8 templates; daily chest + rewarded double |
| D37 | Prices | Store prices are placeholders – the store shows the real localised price. |
| D38 | Bots in Ranked | Never online. Offline "Bot Ranked" exists only as a developer toggle and gives no rank points. |
| D39 | Offline matchmaking | 2.5 s then labelled bots (no humans offline). Online follows the 20 s rule. |

## Online (milestone 10) – known simplifications
| # | Topic | Status |
|---|---|---|
| D40 | Client prediction (spec 14.1) | Clients interpolate the host's 20 Hz snapshots; no local prediction or 150 ms rewind yet. Fine on good connections; add prediction if testers feel lag. |
| D41 | Player-hosted matches | The host is a player, so Quick/FFA/Private results are checked for plausibility, not proven. Ranked needs the dedicated server. |
| D42 | Loadouts in online matches | Stats are rebuilt from item ids by the host; durability/stock come from the client's hello (dedicated server should read Cloud Save instead). |
| D43 | Clip share | Screenshot + share sheet text. A 10-second video needs a native recorder plugin. |
| D44 | Receipt validation | Placeholder in the module; production purchases are refused until implemented. |

## Upgrade v1.4 (milestones 15–28)
| # | Topic | Decision |
|---|---|---|
| D45 | Humanoid art (0.1, W1) | The spec suggests Unity's *Starter Assets* humanoid + Animation Rigging. Asset Store downloads cannot be done from code, so every character is **generated in code** (`Match/Characters/CharacterBuilder`): body, face, 8 hair styles, 6 garment styles, shoes, 12 accessories, a charkhi per type. Animation is **procedural with analytic two-bone IK** (`CharacterView`) – hands stay on the string and the charkhi, the head aims at the kite – instead of an Animator controller + clips (`Editor/CharacterAnimatorBuilder` was not needed). Parts are baked into one GPU-skinned mesh per character (~10 draw calls). If you later import Starter Assets, `CharacterView` is the only class to change. |
| D46 | Shaders (0.1, 12) | Two original shaders: `Kitefall/GradientSky` (gradient, sun disc, animated clouds, golden hour) and `Kitefall/KitePaper` (translucency against the sun, vertex flutter). Written as plain CG so they work in URP and Built-in; light comes from globals the game sets. Shader Graph files need the editor, so textures (brick, plaster, concrete, wood, tiles) are generated in C# instead. |
| D47 | Post-processing (12) | `Kitefall.Graphics` (only compiles when URP is installed) builds a Volume at runtime: Bloom, warm Color Adjustments, ACES, light Vignette by tier; Depth of Field on High in the wardrobe. Shadow distance / MSAA / render scale per tier. Addressables were not added – all art is procedural, so the install stays small without them. |
| D48 | Online warmup (F3) | The match roster is fixed when the match starts, so the online warmup runs for 10 s **after** the room is full (players wait in the matchmaking screen before that). Offline: 10 s warmup vs bots, skipped in duels and the tutorial. Spec wanted "while the room fills (max 45 s)" – needs late-join support. |
| D49 | Practice kite in warmup (F3) | Warmup = walking, emotes and quick chat on the plaza (no kite flying there yet). No dor/kite is ever used in warmup. |
| D50 | Party matchmaking (F2) | PLAY opens a **private** match sized for the mode; party members auto-join by code; the host starts when the whole party is connected and fills the rest with labelled bots. Strangers do not join party matches yet. Team Battle: whole party on one team (`MatchFactory.AssignPartyTeams`). |
| D51 | Roof trait balance (10.2, 10.5) | Measured with `Tools/RoofBalance` (120 equal-skill bot FFA matches): High 48–51 %, Sheltered 49–57 %, Edge 44–49 %, Crate spot 47–58 %, Cables 32–64 % (tiny sample), **Upwind 34–39 % (too low)**. Defaults tuned: Upwind wind bonus 8 % for 120 s + 6 m start help, rows 16.5 m apart and staggered. Upwind still needs work – the numbers are in `BalanceConfig ▸ Roofs` / Remote Config. Spec target 45–55 % is **not met for Upwind yet**. |
| D52 | Upwind approach ring (10.2) | The "approach range 20 m instead of 15 m" cost is a HUD/camera effect only; not implemented yet (the wind bonus and start help are). |
| D53 | Cosmetic ownership online (F1) | Equip commands are validated by the Cloud Code module (same `EconomyCommands`). In a player-hosted match the host only checks that ids exist in the catalogue; a dedicated server should read the profile from Cloud Save. Cosmetics never change stats either way. |
| D54 | Teamwork XP (11.3) | "Help launch" works (+2 m on a teammate's Perfect toss); the small XP bonus is not added to the reward yet. |
| D55 | Voice (F7) | Vivox code is behind the `com.unity.services.vivox` package (party channel + Team Battle team channel, push-to-talk, mic permission on first tap, under-13 off). Stub-compiled only. Check Vivox pricing/free tier before launch. |
| D56 | Kill feed avatars (F4) | Round avatar = outfit colour + initial (no portrait renders). |

## Questions for you
1. **D3 / D15** – Should letting go of PULL keep feeding line (current), and should Dheel need line already releasing at contact?
2. **D26** – Last Kite Flying with 12 or 20 players?
3. **D33** – Is the streak bonus +5 per win (current) or a growing bonus up to +5?
4. **D29–D31** – Are the crafting prices and XP curve OK for the ≈ PKR 15,000 budget plan (ads-first economy)?
5. **D45** – Are code-generated characters OK for now, or do you want me to switch to the Starter Assets humanoid once you import it?
6. **D48 / D50** – Should party matches also accept strangers (public quick-join), and should the online warmup start while the room fills (needs late join)?
7. **D51** – Upwind roofs are still weak (≈ 38 %). Prefer a bigger wind bonus, or a different benefit?
