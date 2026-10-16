# Upgrade v1.4 – what to test in Unity (milestones 16–28)

Do these in order. After each block that works: **commit to Git** (`git add . && git commit -m "M16 tested"`).
If something is wrong, copy the first red Console error and send it.

**Before you start:** pull the latest code, open Unity, wait for compiling, then run **Kitefall ▸ Build Game Scene (complete game)**
again (it adds the new sky and kite-paper materials to the GameDatabase). Press Play in `Main`.

## M16 – Characters (W1)
1. Home: your character stands on the rooftop flying the kite; the kite line starts at the **left hand**.
2. Start Quick Duel. The opponent has a different look and a name tag; yours has no tag.
3. While flying: PULL → arms pull back and the body leans back; let go (dheel) → arms reach toward the kite; REEL → right hand cranks, the charkhi spins.
4. Win / lose a round → jump with arms up / head down (Settings ▸ Gameplay ▸ Auto emotes).

## M17 – Shoulder camera (W2)
1. During flight the camera is behind-right of your character, kite in the upper right.
2. Fly close to the enemy → the camera switches to the kite camera; 1.5 s after the contest it comes back.
3. Top-left camera button → manual switch for 10 s. Settings ▸ Camera ▸ Camera style: try *Shoulder only* and *Kite only*.

## M18 – Wardrobe, characters, outfits (F1)
1. Home ▸ Wardrobe. The camera moves close; the character turns.
2. Tap a swatch → it is **tried on** at once (even if not owned). Equip only works for owned items.
3. Buy an item with gems (offline you can get test gems from Shop ▸ Gems ▸ free gems ad, which runs in test mode).
4. Back on Home and in a match: the new look is used.

## M19 – Kill feed + spectate (F4, F5)
1. FFA: cuts appear top-right as rows: avatar, name (you orange, team blue, enemies red), attack icon + dor icon, victim.
2. Cut 2 kites within 10 s → "Double cut!" banner.
3. Get cut in FFA → spectate the cutter; ◀ ▶ cycle; 👍/🔥 reaction buttons float icons.
4. Last Kite Flying: get eliminated → "Leave match (keep rewards)" button.

## M20 – Emotes (F6)
1. Right edge ▸ emote button: hold, drag to a slot, release (or tap the button, then tap a slot).
2. Spam it: only one emote per 3 s, max 10 per minute. During a cross nothing happens.
3. Wardrobe ▸ Emotes: tap a slot, put another emote there.

## M21 – Training ground (F8)
1. Home ▸ Training: pick any kite / dor / charkhi / tunings (all unlocked), dummies on, 1 sparring bot.
2. In training the left panel controls wind direction, speed, gusts and phase; the practice text shows your sweet-zone % and the last contest's main factors.
3. Leave (menu ▸ Quit match): no coins or items changed.

## M22 – Squad lobby (F2)
1. Home ▸ Squad ▸ Create party → a code and 4 spots; you are the leader (crown).
2. Offline you are alone; pick Team Battle and press PLAY → a normal match starts.
3. Online (2–4 phones): others join with the code, tap Ready, the leader plays – everyone ends up in the same match (Team Battle: same team).

## M23–M25 – Rooftops, roof choice, roof hopping (F9)
1. FFA: after the warmup a **map** shows the rooftops with trait icons and heights. Tap a roof: it turns orange; bots take others (grey).
2. Tap different roofs to read the trait text (High, Sheltered, Upwind, Crate spot, Edge, Cables).
3. In the round: the **ROOF** button (above DIVE) packs up the kite (3–5 s, you can still be crossed).
4. On the roof: buttons with an arrow, the crossing type (plank / bridge / ladder / stairs / jump) and seconds. Tap one: the character crosses.
5. Tap LAUNCH HERE (or wait 25 s) → short launch ritual on the new roof.
6. Quick Duel: both players start on mirrored roofs of the same height.

## M24 – Warmup plaza (F3)
1. FFA offline: first 10 s on a festival plaza (decks, bridges, dummy kites, flags, lights).
2. Joystick walks your character (relative to the camera). Emote and quick chat work. Bots walk around.

## M26 – Launch ritual (F10)
1. Round start: PICK (kite from the floor) → **TAP!** twice when the needle is in gold (kanni) → TIE → **HOLD · LET GO** (release in the gold band) – a helper walks out with your kite and tosses it → **TUG!** when the pulse is biggest → DHEEL → flying.
2. Miss the toss on purpose → "MISS – AGAIN", 3 s later you can retry.
3. Settings ▸ Gameplay ▸ Auto-prep: the ritual plays itself with Good results.
4. On a Cables roof a missed toss can snag: swipe the joystick left/right 3 times.

## M27 – Quick chat + voice (F7)
1. Chat button: 8 messages with icons; a bubble appears over your character and a line in the feed.
2. Team Battle: only your team sees team chat. Settings ▸ Gameplay ▸ "Show quick chat from others" hides others.
3. Voice: install `com.unity.services.vivox` and test with 2 phones in a squad (see ONLINE_SETUP.md ▸ Voice).

## M28 – Graphics
1. Look at the sky (gradient, sun disc, moving clouds, warm golden hour in the morning/evening) and the kites (light shines through the paper when the sun is behind them).
2. Houses have windows, doors, brick/plaster textures; roofs have water tanks, charpais, clothes lines, plants, dish antennas.
3. Settings ▸ Graphics ▸ Quality Low/Medium/High: bloom/tonemapping/vignette change (URP only).
4. On a phone: Window ▸ Analysis ▸ Profiler (Development build) – check draw calls (target ≤ 150 on Medium) and FPS.
