# Milestone 1 – Test checklist

**Goal (spec 16):** *Kite steers in all directions; pull/release changes tension.*

Open `Assets/Scenes/M1_FlightTest.unity` (or **Kitefall ▸ Milestone 1 ▸ Build Flight Test Scene**) and press Play.
The debug panel (top left) shows every number you need. Tick each line.

## A. Unit tests (2 min)
- [ ] **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All** → 63 tests, all green.

## B. Steering (spec 9.1, 10)
- [ ] Kite starts straight downwind of the flyer, about 35° up, line out 60 m.
- [ ] **Up / Down / Left / Right** each move the kite that way. Diagonals work too.
- [ ] The kite turns smoothly (it does not jump) – turn rate shown in the panel is 180°/s for Guddi + Short ribbon.
- [ ] Let go of the stick → the kite slowly turns its nose back up (stability) and climbs.
- [ ] Steer hard left or right → the kite stops at the edge of the wind window (about ±75°) and slows down.
- [ ] Fly straight up → the kite slows near the top (zenith).
- [ ] Steer down into the ground → **CRASH!** banner, kite relaunches after 2 s.

## C. Tension (spec 9.1, 9.2)
Start from a fresh kite (Backspace).
- [ ] Idle tension is **40** and stays there when you touch nothing.
- [ ] Hold **PULL** → tension rises about 35 per second; marker turns **yellow at 60** and **red at 85**.
- [ ] While pulling the kite flies faster (Speed in the panel goes up).
- [ ] Let go of PULL → Action shows **Dheel**, tension falls about 40 per second, **Line out grows** about 10 m/s (Lakri).
- [ ] During dheel the kite slows, sinks slowly and flutters, drifting back to the centre of the window.
- [ ] Press **REEL** → dheel stops, Line out shrinks about 8 m/s, tension drifts back to 40.
- [ ] Hold PULL for a long time → tension sits at 100, the white Strength line (95 for Sooti) is passed, the bar flashes red, and after **0.6 s** above Strength → **SELF-SNAP!** banner. Kite relaunches after 2 s.
- [ ] Pull into the red, then release *before* 0.6 s → no snap (timer resets).
- [ ] Feed line out in steps (tap PULL, let it dheel a few seconds, tap PULL again to climb) until Line out reaches 600 m (Lakri capacity) → line stops at 600 and Action goes back to Idle.
- [ ] A very long dheel (≈ 20 s) lets the kite sink into the ground – this is expected: slack line = lost height.

## D. Wind
- [ ] Compass arrow (top centre) shows where the wind blows relative to the camera; speed in m/s.
- [ ] Over a minute the wind direction wanders a little and the kite follows the window.
- [ ] Select **Assets/ScriptableObjects/Config/Wind_OldCity** and set *Base Speed* to 12 during Play → "STRONG" appears, the kite is faster and tension rises 10% faster.

## E. Loadout data (spec 7, 8)
Select the **Kite** object, change *Dor* to **Reshmi** (or *Charkhi* to **Speed**), right-click the KiteController header ▸ **Rebuild Simulation**:
- [ ] Reshmi: Strength line moves to 93, tension rises faster, turn rate 189°/s.
- [ ] Mazboot: Strength 98, turn rate lower (166°/s).
- [ ] Speed charkhi: max line 450 m, release and reel 14 m/s.
- [ ] Kite class **Pipa** feels faster; **Rokkaku** slower and steadier.

## F. Phone (optional for M1)
- [ ] Android build runs in landscape at a smooth frame rate.
- [ ] Joystick, PULL, REEL and RESET work with your thumbs.

## Feedback to collect
1. Does steering feel too slow / too fast?
2. Does the automatic dheel after letting go of PULL feel right, or should letting go just "hold" the line? (CombatConfig ▸ *Dheel On Pull Release* switches this.)
3. Is the camera distance comfortable?

When everything is ticked: **commit to Git**, then we start **Milestone 2 – Tail rope physics**.
