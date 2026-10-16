# Design decisions & open questions

Spec rule 17.7: *"If something is unclear, contradictory or risky, say so and ask."*
These are the places where milestone 1 had to choose something the spec does not fix.
Every value is in a data asset, so any choice can be changed without code.

| # | Topic | What the spec says | What was implemented | Where to change |
|---|-------|-------------------|----------------------|-----------------|
| D1 | **Idle drift speed** | "Idle: drifts toward 40" – no rate given | 20 points / second, no overshoot | CombatConfig ▸ Tension Idle Drift Rate |
| D2 | **What REEL does to tension** | Only line length is defined for reel | Reel uses idle drift (tension → 40) | `TensionModel.Step` |
| D3 | **When does dheel stop?** | "PULL: hold = pull, release = dheel" and "REEL cancels Dheel" | Letting go of PULL starts dheel; it continues until REEL, PULL, or the charkhi is empty. Can be switched off so letting go = idle | CombatConfig ▸ Dheel On Pull Release |
| D4 | **Which rises get the dor/wind multipliers** | "Strong wind: rises 10% faster · then dor/charkhi multipliers" | Dor rise mult + strong wind apply to *every* rise (pull and idle drift up). Charkhi pull-rise mult applies only while pulling. Charkhi release-drop mult only while releasing | `TensionModel.Step` |
| D5 | **"Strong wind" threshold** | Not defined | Wind ≥ 9 m/s | WindConfig ▸ Strong Wind Speed |
| D6 | **Turn rate numbers** | Only words: Fast / Medium / Slow | Very fast 280 · Fast 240 · Medium 180 · Slow 130 · Very slow 100 °/s (Guddi 180, Pipa 240, Rokkaku 130) | Kite class assets |
| D7 | **Flight model** | Not defined (only "steers", height/speed rules) | Arcade "wind window" sphere around the flyer: joystick turns the kite's nose, the kite flies where the nose points; pull = faster, dheel = slower + sinks + flutters; window ±75°, zenith 82° | FlightConfig |
| D8 | **Tension zone edges** | 0–60 / 60–85 / 85–100 | 60 counts as yellow, 85 counts as red | CombatConfig |
| D9 | **Self-snap & crash in M1** | Round end rules come in M7 | Kite relaunches after 2 s (debug only) | KiteController ▸ Auto Respawn |
| D10 | **Ground height** | – | World y = 0 is ground in milestone 1; buildings don't collide yet (roof/building crash comes with the arena work) | – |
| D11 | **Kite size** | – | Placeholder kite is 1.6 × 1.8 m so it is visible from the follow camera | `KiteMeshFactory` |
| D12 | **Folder structure** | `Assets/Scripts/{Kite, Wind, …}` | Same, plus `Assets/Scripts/Core/` for pure C# rules (no UnityEngine), so they can run on the server unchanged (spec 14.5) | – |
| D13 | **Joystick** | Floating joystick | Fixed joystick in M1 (debug controls). Floating + draggable layout come with the HUD (milestone 9) | – |

## Questions for you
1. **D3** – After testing, should letting go of PULL keep feeding line (current), or only when you *swipe/tap* a dheel gesture?
2. **D7** – Should the kite rise to the top by itself when you let go (current, "stable kite"), or should it hang where it is?
3. Kite classes *Pakpao, Gudiparan, Chula, Wau* are "Update" releases in the spec, so only Guddi, Pipa and Rokkaku assets are created. OK?
