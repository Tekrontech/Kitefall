# KITEFALL — Game Design & Technical Specification (v1.2)

> **For the AI assistant reading this file:** This is the single source of truth for the game *Kitefall*. Read the whole file before writing any code. Follow section 17 (*How to work with me*) strictly: build **one milestone at a time**, keep all tunable numbers in data assets (never hard-coded), and ask before changing any rule written here. Every number in this file is a **starting value** to be tuned by playtesting.

---

## 0. Project facts

| Item | Value |
| --- | --- |
| Working title | Kitefall (final name to be checked for trademarks / store conflicts) |
| Genre | Online competitive 3D kite-fighting (PvP), mobile |
| Platforms | Android first; iOS later (architecture must stay iOS-compatible) |
| Engine | Unity 6 LTS, C#, URP (mobile) |
| Online | Online-only; only the 2-minute tutorial runs offline |
| Team | Solo developer + AI coding assistant |
| Budget | Very small (≈ PKR 15,000 for development). Use free tiers first; paid servers only when ad income covers them |
| Audience | International (Pakistan, India, Afghanistan, Japan, Thailand, Brazil, Indonesia, Malaysia, worldwide) |
| Language | UI in English; localization later (Urdu, Hindi, Portuguese, Indonesian, Japanese) |
| Target performance | ≤ 150 MB install, 60 FPS mid-range, 30 FPS low-end (3 GB RAM), match load ≤ 10 s |

### Non-negotiable rules
1. **No pay-to-win.** Money buys only cosmetics (skins, effects), Season Pass cosmetics, gems for cosmetics, and no-ads. Never stats, never ranked advantage.
2. **Server-authoritative economy and combat.** Kite counts, dor meters, durability, parts, rank and cut results are decided and stored on the server. The client only sends inputs.
3. **Explainable combat.** Every lost contest must be explainable to the player (results screen shows why). Randomness ≤ 5% of outcome.
4. **Safety.** String cutting is a fictional game mechanic. Never mention, reward or depict glass-coated, metal, chemical or nylon ("Chinese") string. Line names like "Tez Dhaar" are gameplay stats only.
5. **Original assets only.** Do not copy PUBG, Free Fire or any other game's assets, UI, sounds or maps.
6. **No ads during gameplay** (see section 13).

---

## 1. Vision

Kitefall brings the world's kite-fighting traditions into one global online arena. Players pick a kite, a tail (*dum*), a string (*dor*) and a spool (*charkhi*), read the wind, win the cross, and cut the opponent's line.

**Pitch:** "Real-time kite duels against players worldwide. Read the wind, win the cross, cut the line."

**Design principle:** Easy to start (1 minute), hard to master (months), fair to compete in.

**Loadout analogy (like shooters):**

| Kitefall | Shooter equivalent |
| --- | --- |
| Kite class | Character / body (movement) |
| Dum (tail) | Stability perk |
| Dor (string) | Weapon |
| Charkhi (spool) | Weapon attachment |
| Tuning | Attachment mods (sidegrades) |
| Sky crates | Air drops / loot |

---

## 2. Glossary

| Term | Meaning |
| --- | --- |
| Dor | The fighting string/line. Has types (section 7). Stock is measured in meters |
| Charkhi | Spool that holds the dor. Controls capacity and reel/release speed |
| Dum | Kite tail. More tail = more stability, less agility |
| Kheench | Pull — reeling in hard. Also the name of an attack |
| Dheel | Release — letting line out. Also the name of an attack |
| Dive (Ghotta) | A fast downward attack from above |
| Contact | Two lines touching (distance < 0.3 m) |
| Cut contest | The 3-second duel that starts on contact |
| Contest meter (M) | Value from −100 to +100 deciding the contest |
| Tension (T) | 0–100; core skill resource |
| Sweet zone | Moving band on the tension bar where input power is maximal |
| Self-snap | Your own line breaks from over-tension |
| Sky crate | Loot dropped mid-match in FFA / Last Kite Flying |

---

## 3. Player flow

### 3.1 First launch (target: first real match within ~3 minutes)
1. **Splash** (2–3 s): logo, wind sound, preload. Force-update screen if required.
2. **Login:** Continue with Google (Android) · Sign in with Apple (iOS) · Play as Guest. Guests get a warning that progress may be lost until they link an account.
3. **Terms + age gate:** ToS, Privacy Policy, age confirmation (store policy).
4. **Region + name:** region auto-detected (Asia, Middle East, Europe, Americas), editable. Display name 3–16 chars, profanity filter. Country flag auto.
5. **Tutorial** (2 min, offline): (a) launch, (b) steer + pull/release, (c) read wind indicator, (d) cut a dummy kite's line. 20–30 s per step.
6. **First Quick Duel** vs an Easy bot labelled "Practice opponent".
7. **Home + welcome rewards:** 3 Guddi, 1 Rokkaku, 500 coins, starter parts, 600 m Sooti dor, Lakri charkhi.

### 3.2 Returning player (target: Home in < 10 s)
Auto-login → daily login reward (7-day cycle) → event banner once if active → Home. Last mode and loadout are remembered.

### 3.3 No connection
Home shows "No connection — retry". Only Tutorial and Hangar viewing allowed.

---

## 4. Screens

### 4.1 Home (landscape)
| Position | Element | Action |
| --- | --- | --- |
| Bottom right | **PLAY** (large) | Start matchmaking in selected mode |
| Above PLAY | Mode selector | Ranked Duel, Quick Duel, Free-for-All, Private Room (+ Team Battle, Last Kite Flying later) |
| Bottom centre | Loadout chip | Shows kite · dum · dor · charkhi; opens Hangar |
| Left menu | Hangar, Workshop, Shop, Season Pass, Leaderboard, Friends | Open screens |
| Top left | Avatar + rank | Profile |
| Top right | Coins, Gems, Parts, Inbox, Settings | — |
| Right | Daily missions (3), event card | — |
| Centre | Selected kite flying in 3D with its tail | Showroom |

### 4.2 Hangar (loadout)
| Slot | Options | Shows |
| --- | --- | --- |
| Kite | Class + skin | Count (e.g. ×4), durability %, stat bars, 3D preview |
| Dum | None, Short ribbon, Double ribbon, Long tail, Tassel | Stability/agility change, live preview |
| Dor | 6 types + skin | Meters left per type, stats, tuning |
| Charkhi | 5 types + skin | Capacity, reel speed, special, tuning |
| Stack (FFA/Team) | Order of 3 kites | Next kite used on respawn |

Rules: kite with count 0 is greyed out (free Guddi always available). Durability < 30% → warning (contest power −10%). Dor < 100 m of a type → that type can't be taken into a match; free Sooti refill of 300 m daily. "Recommended for this arena" suggestion based on wind.

### 4.3 Workshop (no timers, everything instant)
| Action | Cost |
| --- | --- |
| Craft Common kite | Sail ×3, Sticks ×2, coins |
| Craft Rare/Epic look | Gems or event parts (cosmetic only) |
| Repair kite | Coins (by missing durability); free via rewarded ad 3/day |
| Make dor spool | Line parts + coins → 200 m (part type depends on dor) |
| Craft charkhi | Wood + gear parts + coins |
| Apply tuning | Unlocked by mastery (section 7.4) |
| Salvage | Duplicate skin → parts |

### 4.4 Other screens
- **Shop:** Featured (daily), Kite skins, Dum effects, Cut effects, Dor skins, Charkhi skins, Gems, No-ads.
- **Season Pass:** 50 levels, free + premium track, cosmetics and parts only.
- **Leaderboard:** Global top 500, Country, Friends.
- **Profile:** rank, matches, win %, cuts, best streak, favourite kite/dor, last 20 matches, account linking, name change (once / 30 days), block list.
- **Settings:** Controls (one/two-thumb, left-hand, joystick size, draggable buttons, sensitivity, dive gesture), Camera (distance, follow, shake), Graphics (Auto/Low/Med/High, FPS 30/60/120, battery saver), Audio, Haptics, Gameplay (contest meter style, sweet-zone highlight, colour-blind mode), Account.

---

## 5. Game modes

| Mode | Players | Match length | Kite/dor loss | Ranked | Release |
| --- | --- | --- | --- | --- | --- |
| Ranked Duel | 1v1 | 10–15 min, best of 5 | Yes | Yes | Launch |
| Quick Duel | 1v1 | 5–8 min, best of 3, rounds max 2 min, no Storm | Durability only | No | Launch |
| Free-for-All | 8 | 12 min | Yes | No (XP) | Launch |
| Private Room | 2–8 | Custom 5–20 min | No | No | Launch |
| Team Battle | 4v4 | 12–15 min | Yes | Team rank | Update 1 |
| Last Kite Flying | 20 | 15–20 min | Yes | Separate rank | Update 2 |

- Ranked Duel unlocks at Silver rank or after 10 matches.
- Empty-lobby rule: after 20 s, Quick Duel and FFA fill with labelled bots ("Bot" tag). Ranked never uses bots.

### 5.1 Win conditions
- **Ranked Duel:** first to 3 rounds. Round max 3 min. 20 s **loadout break** between rounds (may change dum and charkhi; not kite class or dor). Extra round on draw.
- **Quick Duel:** first to 2 rounds.
- **FFA:** 12 min. Cut +100, assist +40 (you also contested that kite within 3 s), self-snap −50. Respawn after 5 s with next kite in stack; empty stack → free Guddi. Sky crate every 3 min.
- **Team Battle:** first team to 25 cuts or 15 min. Each team's "Captain kite" is worth +2.
- **Last Kite Flying:** no respawn. Everyone starts with Sooti dor + Lakri charkhi. Better gear only from sky crates. Storm zone shrinks the sky every 2 min. Last kite flying wins.

---

## 6. Match flow

| Phase | Time | Behaviour |
| --- | --- | --- |
| Matchmaking | ≤ 20 s | Same region, rank ±1 tier; widen to ±2 after 10 s; bot fill at 20 s (non-ranked) |
| Ready / load | ≤ 8 s | Show names, flags, ranks, kite classes. Failure to load before start = no penalty |
| Arena intro | 5 s | Camera fly-over, this match's wind pattern shown |
| Launch | 5 s | "HOLD to launch"; release in perfect window = start 10 m higher |
| Round | max 3 min | Phases below; ends on cut, self-snap, crash or timer |
| Round end | 4 s | Slow-motion replay of the cut, score update, reset |
| Results | — | Win/loss, *why*, rewards, ledger, Rematch / Next / Share clip |

### 6.1 Round phases (keep long matches interesting)
| Time | Phase | Effects |
| --- | --- | --- |
| 0:00–1:00 | Calm | Light wind. Radar shows direction only, not height |
| 1:00–2:00 | Gust | A gust every 15–20 s (1 s warning). Contesting with the gust → Wind ×1.08. Sky crates in FFA/LKF |
| 2:00–3:00 | Storm | Strong wind, sweet zone −3, all contest Power ×1.10 (prevents stalling) |

Arena events: Old City — flock of birds shakes lines for 2 s; Coastal Cliffs — sea gust.

### 6.2 Round end reasons
| Reason | Result |
| --- | --- |
| Cut | Winner gets the round point; loser's kite is cut (count −1 in loss modes) |
| Self-snap | Opponent gets the point |
| Crash (stall into roof/ground/building) | Opponent gets the point; crashed kite durability −15%, count unchanged |
| Timer (3 min) | Tiebreak: higher kite → more durability → draw (no point) |

---

## 7. Dor & Charkhi system (weapon system)

### 7.1 Rules
- Each dor type has its own stock in meters (like ammo). Line used in a match is consumed; a cut line loses the cut portion.
- All dor and charkhi types unlock by **player level** (play only). Rarity tiers change visuals only — identical stats.
- Balance updated per season via Remote Config.
- New dor types reach Ranked 2 weeks after release (tested in Quick/FFA first).

### 7.2 Dor types
| Dor | Role | DorCut (power) | DorSpeed | Strength (snap limit) | Special | Unlock |
| --- | --- | --- | --- | --- | --- | --- |
| Sooti | AR — balanced | 1.00 | 1.00 | 95 | No weakness; free daily refill | Start |
| Reshmi | SMG — fast | 0.95 | 1.20 | 93 | Kite agility +5%; tension rises 10% faster | Level 5 |
| Tez Dhaar | Shotgun — power | 1.10 | 1.00 | 92 | Tension rises 15% faster; dor wears 30% faster | Level 10 |
| Mazboot | LMG — tank | 1.00 | 0.85 | 98 | Survives red zone longer; kite agility −8% | Level 15 |
| Lambi | Sniper — reach | 0.95 | 0.95 | 94 | Angle bonus 1.15 (instead of 1.10) when line out > 500 m | Level 20 |
| Lachakdaar | Defence | 0.97 | 0.95 | 96 | Sweet zone +4; Dheel counter bonus 1.20 (instead of 1.15) | Level 25 |

### 7.3 Charkhi types
| Charkhi | Role | Capacity | Reel in | Release | Special | Unlock |
| --- | --- | --- | --- | --- | --- | --- |
| Lakri | Standard | 600 m | 8 m/s | 10 m/s | None | Start |
| Speed | Quickdraw | 450 m | 14 m/s | 14 m/s | Tension falls 20% faster on release | Level 3 |
| Bari | Scope | 1,000 m | 6 m/s | 9 m/s | Long reach; launch 1 s slower | Level 8 |
| Brake | Stabilizer | 600 m | 8 m/s | 7 m/s | During Dheel Cut tension never drops below 55 | Level 12 |
| Gear | Compensator | 550 m | 10 m/s | 9 m/s | Kheench Cut Power ×1.05; pull tension rises 15% faster | Level 18 |

### 7.4 Tuning (2 slots per dor and per charkhi, unlocked at mastery 3 and 7)
All tunings are sidegrades.

| Tuning | Applies to | Plus | Minus |
| --- | --- | --- | --- |
| Waxed coat | Dor | DorSpeed +5% | Strength −1 |
| Double twist | Dor | Strength +2 | DorSpeed −5% |
| Light thread | Dor | Kite agility +3% | DorCut −2% |
| Oiled axle | Charkhi | Reel speed +20% | Capacity −50 m |
| Wide drum | Charkhi | Capacity +100 m | Reel speed −15% |
| Soft grip | Charkhi | Sweet zone +2 | Kheench power −3% |

Mastery 1–10 per item: XP from matches; rewards are skins, trail colours and tuning slots only.

### 7.5 Reference builds
| Build | Kite + Dum | Dor + Charkhi | Playstyle | Weak against |
| --- | --- | --- | --- | --- |
| All-rounder | Guddi + Short ribbon | Sooti + Lakri | Solid everywhere | Specialists in their niche |
| Rusher | Pipa + None | Reshmi + Speed | Fast dives, quick cuts | Tank, long contests |
| Tank | Chula + Long tail | Mazboot + Brake | Long Dheel contests | Sniper angles |
| Sniper | Rokkaku + Double ribbon | Lambi + Bari | Straight-angle cuts from range | Rushers up close |
| Berserker | Gudiparan + Tassel | Tez Dhaar + Gear | Huge Kheench power | Lachakdaar Dheel (overpull) |
| Wall | Wau + Long tail | Lachakdaar + Brake | Counter-play | Dive users |

### 7.6 Balance targets
- Win-rate per dor (same skill bracket): 47–53%. Outside → tune ±2–5% next patch.
- Pick-rate > 35% for one dor = meta problem → nerf it or buff its counter.
- All values in ScriptableObjects, overridable by Remote Config.

### 7.7 Sky crates (FFA, Last Kite Flying)
- Crates hang from balloons; circle within range for 2 s to collect.
- Contents (match-only, never added to inventory): better dor, better charkhi, Patch kit (+25% durability), Dor refill (+200 m).
- Crate location is visible to everyone on radar.

---

## 8. Kites and tails

### 8.1 Kite classes (stats 1–5; total budget equal)
| Class | Inspired by | Speed | Agility | Stability | ClassBase | Turn | Sweet zone | Style | Release |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Guddi | Pakistan/India fighter | 3 | 4 | 3 | 1.00 | Medium | 15 | Balanced | Launch |
| Pipa | Brazil | 5 | 4 | 2 | 0.92 | Fast | 12 | Dive, hit-and-run | Launch |
| Rokkaku | Japan | 2 | 2 | 5 | 1.05 | Slow | 18 | Strong wind, Dheel | Launch |
| Pakpao | Thailand | 3 | 5 | 2 | 0.92 | Very fast | 12 | Dodge, timed Kheench | Update |
| Gudiparan | Afghanistan | 2 | 2 | 4 | 1.08 | Slow | 16 | Long contests | Update |
| Chula | Thailand | 1 | 1 | 5 | 1.10 | Very slow | 18 | Tank | Update |
| Wau | Malaysia | 3 | 3 | 4 | 1.00 | Medium | 16 | Fast stall recovery | Update |

Designs are original, only *inspired by* real traditions.

### 8.2 Dum (tail)
| Dum | Sweet zone | Turn speed | Stability |
| --- | --- | --- | --- |
| None | −5 | +15% | −2 |
| Short ribbon | 0 | 0 | +1 |
| Double ribbon | +3 | −5% | +2 |
| Long tail | +5 | −15% | +3 |
| Tassel | +2 | −5% | +1 |

Tail physics: rope simulation, 6/10/14 segments (Low/Med/High quality), reacts to wind, turns and dives.

### 8.3 Kite inventory (kites can run out)
- Each kite class has a **count**. Cut in a loss mode → count −1 (server-side).
- **Durability** 0–100%: each contest −3 to −8%, crash −15%, 0% = kite destroyed. Repair with coins.
- Crafting from parts earned in matches.
- Free basic Guddi is always unlimited so no player is ever locked out.

### 8.4 Animations
- Kite: launch (helper throws), idle flutter, banking turn, gust jolt + recovery, dive and pull-up, stall spin, visible tears from damage, cut fall (spins, drifts, lands on roof/tree).
- Line: real sag/curve by tension, vibration + sparks on contact, snap into two pieces.
- Player character on rooftop: pull/release hand animations, spinning charkhi, win/lose reactions.
- Kite flight and tail = procedural (code-driven), not keyframed.

---

## 9. Fighting mechanism

### 9.1 Per-kite state
| Variable | Range | Update rule |
| --- | --- | --- |
| Tension T | 0–100 | Pull: +35/s · Release: −40/s · Idle: drifts toward 40 · Strong wind: rises 10% faster · then dor/charkhi multipliers |
| Height h | m | Up with pull + wind; down with release + stall |
| Speed v | m/s | Class speed + wind + pull |
| Line out L | 0 – charkhi capacity | Release adds (release speed), Reel removes (reel speed) |
| Durability D | 0–100% | See 8.3 |

### 9.2 Tension zones
| Zone | Range | Effect |
| --- | --- | --- |
| Safe (green) | 0–60 | Stable, low power |
| Strong (yellow) | 60–85 | Higher power |
| Danger (red) | 85–100 | Max power; above dor **Strength** for > 0.6 s → **self-snap** |

**Sweet zone:** during a contest a band (base 15 points + class/dum/dor/tuning modifiers) moves up and down the tension bar. Keeping T inside it is the core skill (rhythm-game feel, haptic pulse when inside).

### 9.3 Kite states
`FLY → APPROACH (lines < 15 m) → CONTACT (lines < 0.3 m) → CUT_CONTEST (max 3 s) → { CUT | NEUTRAL | SELF_SNAP }`
`NEUTRAL → FLY` after 1 s cooldown (no re-contact between the same pair).

- Server checks 3D segment-to-segment distance between every pair of lines at 20 ticks/s.
- APPROACH shows a ring around your kite and highlights the enemy line.
- Attack type locks from the first input within **0.3 s** of contact; no input = neutral "Hold".

### 9.4 Attacks and counters
| Attack | Input | Mechanic | Strength | Risk |
| --- | --- | --- | --- | --- |
| Kheench Cut | Hold PULL at contact | Saw the line by pulling hard | Fastest meter movement | Tension spikes; self-snap |
| Dheel Cut | Release + no reel at contact | Feed line out continuously | Safe tension, saves durability | Slow; weak if opponent is above |
| Dive Cut | DIVE (or steer down + pull) ≤ 1 s before contact | Drop onto opponent's line from above | +25% Power in first 1 s | Missing costs height; crash risk |

Counter triangle: **Kheench > Dive**, **Dive > Dheel**, **Dheel > Kheench**.
Winner of the matchup ×1.15, loser ×0.87, same attack ×1.00. (Lachakdaar: Dheel bonus 1.20.)

### 9.5 Contest resolution (server-side, every tick = 1/20 s)

```text
Power = ClassBase × DorCut × Input × Position × Angle × Wind × Counter × Charkhi × Condition × PhaseMult × DiveBurst

ClassBase = 0.90–1.10            // kite class
DorCut    = 0.95–1.10            // dor type (+ tuning)
Input     = 1.00 if T inside sweet zone
            0.55 if outside but T < 85
            0.25 if T ≥ 85 (red)   // snap timer runs while T > Strength
Position  = clamp(1 + 0.02 × (myHeight − theirHeight), 0.85, 1.15)
Angle     = 1.10 if crossing angle 60°–120°
            0.90 if crossing angle < 30°
            1.00 otherwise
            (Lambi dor with line out > 500 m: 1.15 instead of 1.10)
Wind      = 0.95–1.05            // favouring the side the wind comes from; ×1.08 during a gust
Counter   = 1.15 / 0.87 / 1.00
Charkhi   = 1.05 for Gear charkhi on Kheench Cut, else 1.00
Condition = 0.90 if durability < 30%, else 1.00
PhaseMult = 1.10 during Storm phase, else 1.00
DiveBurst = 1.25 during the first 1 s of a Dive Cut, else 1.00
Multi     = 0.80 if this kite is in two contests at once

ΔM per tick = K × DorSpeed_leading × (Power_A − Power_B) + noise
K         = 6                    // equal players → contest ~3 s
DorSpeed  = dor speed of the side currently pushing M toward its end (0.85–1.20)
noise     = uniform(−0.5, +0.5) from server seed  // total effect < 5%
M starts at 0, positive = toward A winning.
```

**Outcome**
| Condition | Result |
| --- | --- |
| M reaches +100 | B's line is cut |
| M reaches −100 | A's line is cut |
| 3 s elapsed, M > +40 | A wins (late cut) |
| 3 s elapsed, M < −40 | B wins |
| 3 s elapsed, −40 ≤ M ≤ +40 | NEUTRAL: both −5% durability, 1 s cooldown |
| T above dor Strength for > 0.6 s | SELF_SNAP for that player (overrides M) |
| Both cut on same tick | Higher \|M\| wins; equal → both cut (Duel: round draw) |

**Worked example** (both on Sooti + Lakri, all dor/charkhi modifiers 1.00):
A = Guddi (1.00), 5 m above, Dive Cut, sweet zone. B = Rokkaku (1.05), Dheel Cut. Angle 80°.
Power A = 1.00 × 1.10 (pos) × 1.10 (angle) × 1.15 (counter) × 1.25 (dive) = **1.739**
Power B = 1.05 × 0.90 × 1.10 × 0.87 = **0.904**
ΔM ≈ 6 × (1.739 − 0.904) ≈ +5 per tick → A wins in ~1 s. If A panics into red (Input 0.25) A's Power ≈ 0.43 and B pulls the meter back.

### 9.6 After a cut
1. Server confirms → 0.5 s slow-motion on both clients, camera on the cut kite.
2. Cut kite spins, drifts 3 s, falls into the arena; leftover line dangles.
3. Loser: kite count −1 and cut dor meters removed (loss modes).
4. Winner: +parts, cut XP, 10-second vertical clip auto-saved for sharing.
5. Duel → round end. FFA → loser spectates 5 s then launches next kite.

### 9.7 Results "why" breakdown
Show top factors, e.g. *"You won: height advantage + counter attack + sweet zone 82%"* / *"Opponent was above you and Dive countered your Dheel — try Kheench next time."*

---

## 10. Controls & HUD

| Element | Position | Behaviour |
| --- | --- | --- |
| Steer joystick | Bottom left (floating) | Steers kite |
| PULL | Bottom right, large | Hold = pull, release = dheel |
| DIVE | Left of PULL | Dive Cut (or gesture) |
| REEL | Above PULL | Reel in; cancels Dheel |
| Tension bar | Right edge, vertical | Green/yellow/red + sweet zone during contest |
| Score + timer | Top centre | — |
| Phase banner | Top centre | Calm / Gust / Storm + next gust |
| Wind compass | Below score | Direction + speed; flashes 1 s before gust |
| Radar | Top right | Enemy direction + height arrows, crate markers |
| Contest meter | Centre | Only during contact |
| Dor + charkhi | Bottom centre | Dor name, line out / capacity, stock, kite durability % |
| Menu | Top left | Settings; Ranked quit = loss |

---

## 11. Arenas

| Arena | Inspired by | Wind twist | Release |
| --- | --- | --- | --- |
| Old City Rooftops | Lahore / Ahmedabad | Shifting wind between roofs, power cables | Launch |
| Coastal Cliffs | Brazil | Strong steady sea wind | Launch |
| River Festival | Japan | Layered wind over the river, height battles | Update 1 |
| Royal Field | Thailand | Open field, big vs small kites | Update 1 |
| Mountain Valley | Afghanistan | Updrafts and sudden gusts | Update 2 |
| Storm Archipelago | Indonesia / Malaysia | Big LKF map with storm zone | Update 2 |

Time of day and wind pattern randomized per match.

---

## 12. Progression & economy

### 12.1 Currencies
| Currency | Source | Use |
| --- | --- | --- |
| Coins | Every match | Repair, dor spools, common crafting |
| Parts (sail, sticks, line, wood, gears) | Matches, missions | Crafting kites, dor, charkhi |
| Gems | Rare rewards, purchase | Cosmetics, Season Pass |

### 12.2 Per-match rewards
| Item | Win | Loss | Note |
| --- | --- | --- | --- |
| Coins | 180 | 70 | ×2 via rewarded ad |
| Parts | 5–7 | 2–3 | +1 per cut |
| Season Pass XP | 300 | 140 | — |
| Dor/charkhi mastery XP | 100 | 50 | Items used |
| Dor used | 60–150 m | 60–150 m + cut line | — |

### 12.3 Ranks
Bronze → Silver → Gold → Platinum → Diamond → Master → Sky Legend (global top 500). Seasons of 2 months, soft reset. Global and country leaderboards.

| Ranked Duel result | Rank points |
| --- | --- |
| Win | +25 to +35 |
| Loss | −15 to −25 |
| Quit / no reconnect | −35, 10 min queue ban; 3rd quit in a day = 1 hour |
| Win streak 3+ | +5 per win (max 5) |

### 12.4 Monetization
Season Pass (free + premium, cosmetics/parts), cosmetic shop, rewarded ads, interstitials (limited), no-ads purchase. Never stats.

---

## 13. Ads

**Principle:** never during a match or on the path into competition. Most revenue from rewarded ads.

### 13.1 Rewarded (player-initiated)
| Placement | Button | Reward | Daily cap |
| --- | --- | --- | --- |
| Results | Double rewards | Coins + parts ×2 (not rank points) | 10 |
| Workshop | Free repair | One kite to full durability | 3 |
| Hangar (dor < 100 m) | Dor refill | +200 m Sooti | 3 |
| Hangar (kite count 0) | Free kite | 1 Common kite of that class | 2 |
| Home | Daily chest ×2 | Double daily chest | 1 |
| Shop | Free gems | 5 gems | 2 |
| Season Pass | Bonus XP | +150 Pass XP | 2 |

### 13.2 Interstitial
- Only after a match, when the player taps Next or Home on Results (never over Results).
- Minimum **2 matches and 8 minutes** since the last interstitial; max 8/day.
- None during the first 3 days or first 10 matches.
- Skip right after a Ranked loss (show after the next match instead).
- If the player watched 3+ rewarded ads today, halve interstitial frequency.

### 13.3 Never show ads
Match, launch, loadout break, contest, matchmaking, loading, app open, banners anywhere, revive/second-chance ads in Ranked or LKF.

### 13.4 Technical
- Mediation: Unity LevelPlay or Google AdMob.
- All caps and frequencies in Remote Config (per country).
- No-ads purchase removes interstitials; rewarded buttons stay. Premium Season Pass also removes interstitials.
- Consent: GDPR/UK consent form, iOS ATT prompt, non-personalized ads for under-13 users; check Google Play Ads and Families policies before launch.
- Track retention after ad exposure; reduce caps if retention drops.

---

## 14. Technical architecture

| Layer | Choice |
| --- | --- |
| Engine | Unity 6 LTS, C#, URP mobile |
| Netcode | Netcode for GameObjects (or Photon Fusion) |
| Hosting | Start: Unity Relay (player-hosted) for Quick/Private. Ranked: dedicated server authority |
| Matchmaking | Region + rank (Asia, Middle East, Europe, Americas), target ping < 120 ms |
| Accounts | Google Play Games / Apple / Guest (linkable) |
| Inventory & economy | Unity Cloud Save + Economy + Cloud Code (or PlayFab). Only server mutates kites, dor meters, durability, parts |
| Config | Unity Remote Config for balance + ads |
| Anti-cheat | Server verifies contests and movement limits; client results never trusted |
| Analytics | Unity Analytics + Crashlytics from day 1 |
| Version control | Git + private GitHub repo from day 1 |

### 14.1 Networking rules
- Client sends inputs (steer, pull, dive, reel) with timestamps; server simulates contests.
- Client predicts kite flight and contest meter; server corrects smoothly.
- Server rewinds up to 150 ms when evaluating contact.
- Ping > 250 ms → warning; excluded from that region's Ranked queue.

### 14.2 Disconnects
| Case | Rule |
| --- | --- |
| During contest | Freeze 3 s; no return → connected player wins; loss applies only to disconnected player |
| During match | 30 s reconnect window, kite in AI "hold" mode; 30 s → round forfeit; 60 s → match forfeit |

### 14.3 Bots
States: Patrol → Hunt → Attack (chooses counter) → Retreat. Difficulty by sweet-zone accuracy: Easy 50%, Medium 70%, Hard 85%. Same physics, stats and formula as players. Easy bots use Sooti + Lakri; Hard bots pick a counter to the player's build. Always labelled; never in Ranked.

### 14.4 Suggested Unity structure
```text
Assets/
  Art/  Audio/  Materials/  Prefabs/  Scenes/
  ScriptableObjects/
    Kites/  Tails/  Dors/  Charkhis/  Tunings/  Arenas/  Modes/  Ads/
  Scripts/
    Input/  Kite/  Wind/  Combat/  Match/  Camera/  UI/
    Networking/  Economy/  Ads/  Data/  Bots/
  Tests/
```

### 14.5 Core classes
`KiteController`, `KiteFlightSim`, `TailRope`, `WindField`, `TensionModel`, `LineRenderer3D`, `ContactDetector`, `ContestResolver`, `AttackSelector`, `MatchManager`, `RoundPhaseController`, `SkyCrateSpawner`, `BotBrain`, `LoadoutService`, `InventoryService` (server-backed), `AdsManager`, `ResultsPresenter`, `KiteCameraController`.

Keep gameplay calculation (pure C#, testable) separate from visuals, UI and networking.

### 14.6 Data assets (ScriptableObject fields)
```csharp
// KiteClassData
string id; string displayName; float speed; float agility; float stability;
float classBase; float turnRate; int sweetZoneBase; GameObject prefab;

// TailData
string id; int sweetZoneMod; float turnMod; float stabilityMod; int ropeSegments;

// DorData
string id; string displayName; float dorCut; float dorSpeed; int strength;
float tensionRiseMult; float wearMult; float agilityMod; int sweetZoneMod;
float dheelCounterBonus; float longRangeAngleBonus; int unlockLevel;

// CharkhiData
string id; int capacityMeters; float reelSpeed; float releaseSpeed;
float releaseTensionDropMult; int dheelTensionFloor; float kheenchPowerMult;
float pullTensionRiseMult; float launchDelay; int unlockLevel;

// TuningData
string id; enum Target { Dor, Charkhi }; StatModifier plus; StatModifier minus;

// CombatConfig
float tensionPullRate = 35; float tensionReleaseRate = 40; float tensionIdleTarget = 40;
float contactDistance = 0.3f; float approachDistance = 15; float attackLockWindow = 0.3f;
float contestMaxTime = 3; float contestK = 6; float lateWinThreshold = 40;
float snapHoldTime = 0.6f; float counterWin = 1.15f; float counterLose = 0.87f;
float diveBurst = 1.25f; float diveBurstTime = 1; float noise = 0.5f;
float neutralDurabilityLoss = 5; float neutralCooldown = 1; float multiContestMult = 0.8f;

// AdsConfig (also in Remote Config)
int interstitialMinMatches = 2; float interstitialMinMinutes = 8; int interstitialDailyCap = 8;
int newPlayerGraceDays = 3; int newPlayerGraceMatches = 10; Dictionary<string,int> rewardedDailyCaps;
```

---

## 15. Graphics & audio
- Stylized-realistic, premium look; sky is ~70% of the screen — invest in sky, clouds, golden hour.
- Highest detail on kites: fabric/paper texture, stick frame, light translucency.
- Nearby roofs detailed, distant city as silhouettes.
- Effects: line glint, contact sparks, snap flash, paper fragments.

| Setting | Low | Medium | High |
| --- | --- | --- | --- |
| FPS | 30 | 60 | 60–120 |
| Shadows | Off | Hard | Soft |
| Tail segments | 6 | 10 | 14 |
| Post-processing | Off | Bloom | Bloom + grading + volumetric light |

Audio: layered wind, line hiss/strain, contest vibration, snap, crowd, original regional music per arena. Haptics: light pulse in sweet zone, strong jolt on cut.

---

## 16. Roadmap & milestones

| # | Milestone | Done when |
| --- | --- | --- |
| 1 | KiteController + WindField + TensionModel (single kite) | Kite steers in all directions; pull/release changes tension |
| 2 | Tail rope physics | Tail reacts naturally to turns, dives, gusts |
| 3 | Line rendering + ContactDetector | Two crossing lines log CONTACT |
| 4 | ContestResolver (9.5) + contest meter UI | Every contest logs inputs, powers, M, outcome |
| 5 | 3 attacks + counters + sweet zone | Testers can explain why they won/lost |
| 6 | Dor/Charkhi data — start with Sooti, Reshmi, Mazboot + Lakri, Speed | Testers feel the difference between 3 builds |
| 7 | Round phases + best-of-5 flow | A 10–15 min duel doesn't feel boring (ask testers) |
| 8 | BotBrain (Easy/Medium/Hard) + builds | Duel vs bot is fun |
| 9 | HUD + results + ledger | Full Quick Duel offline, start to results |
| 10 | Online: Netcode/Relay + server-side ContestResolver | Two phones duel fairly at 150 ms lag |
| 11 | Accounts + server inventory | Cut kites/dor reduce on server; cannot be hacked locally |
| 12 | Home, Hangar, Workshop, tuning, Settings | Sections 4 and 7 flows complete |
| 13 | Matchmaking, Ranked, quit penalty | Match in ≤ 20 s, rank points correct |
| 14 | Ads (rewarded first), Shop, Season Pass, clip share | Section 13 caps work via Remote Config |

**Gate:** do not start milestone 10 (online) until 5 different testers, without explanation, ask for "one more match" against bots.

Release phases: Phase 1 core prototype → Phase 2 online duel → Phase 3 closed test + soft launch (3–5 countries) → Phase 4 global (Team Battle, Last Kite Flying, more classes, iOS, festival events).

---

## 17. How to work with me (instructions for the AI)

1. Work on **one milestone (section 16) at a time**. Do not jump ahead.
2. For every component give: file name and path, full code, dependencies/packages, Unity setup steps (which GameObject, which component, inspector values), and how to test it.
3. Put every tunable number in the ScriptableObjects from 14.6 — no magic numbers in code.
4. Keep combat math in plain C# classes with unit tests (Unity Test Framework), so it can later run on the server unchanged.
5. Write code that works in Unity 6 LTS with URP for Android. Prefer the new Input System.
6. Small changes only; after each one, tell me exactly what to test before continuing. Remind me to commit to Git after each working step.
7. If something in this spec is unclear, contradictory or technically risky, **say so and ask** instead of guessing.
8. Never put API keys, signing keys or passwords in code or in this chat.
9. Follow the non-negotiable rules in section 0 (no pay-to-win, server authority, safety, original assets, no ads in gameplay).
10. Explain briefly in simple English (I am learning Unity while building).

**Start with milestone 1.**
