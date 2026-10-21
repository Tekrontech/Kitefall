# Kitefall – World 3D asset list (houses and rooftop props)

Today every house and prop is built in code (`ArenaBuilder.cs`, `CityFiller.cs`, `PropFactory.cs`). You can replace any of
them with a real 3D model. The code-built version stays as the fallback, so a missing model never breaks the game.

## How to add a model (simple steps)

1. Put the file in **`Assets/Art/World/`** named exactly like the list below, e.g. `house_01.fbx`, `water_tank.glb`.
   Allowed: `.fbx`, `.glb` / `.gltf` (needs the free glTFast package – the menu offers to install it), `.obj`, or a Unity
   `.prefab`. If one name has several files: `.prefab` wins, then `.fbx`, then the rest.
2. Unity sets it up by itself (or click **Kitefall ▸ World ▸ Setup World Assets**). A window lists what was found, the size
   in metres, the triangle count, and warnings (too many triangles, wrong scale, unknown name).
   This writes `Assets/Resources/KitefallWorld/WorldAssets.asset` – that is what the game reads.
3. Press Play and start a match on a town arena (Old City Rooftops, Santiago, Mediterranean Harbour …).

## Rules for every model

- **Y up, metres** (1 unit = 1 m). From Blender: FBX export with "Apply Transform", forward −Z / up Y.
- **Pivot bottom-centre.** The game measures the model's real bounds and puts their bottom-centre on the spot, so a wrong
  pivot is forgiven – but keep it clean anyway.
- **Front faces −Z** (towards the camera / street): doors, ladder rungs, the stairwell door.
- **No colliders needed** – the world is visual only (the server owns the roofs). Colliders are removed. Cameras, lights and
  animations in the file are switched off on import.
- **Mobile budget:** triangle numbers below are for phones. Use 1–2 materials per model, textures 512–1024 px
  (houses up to 1024, props 256–512). URP/Lit materials are best; Built-in "Standard" materials are converted to the game's
  lit material (colour + main texture kept) so they do not show pink.
- **Original or CC0 only** – no brands, logos, shop signs with real names, real people, or copied art.
- **Safety:** nothing sharp or dangerous as decoration (no glass-coated string, wire spools, blades).
- **International:** houses can come from many places (Mediterranean, Latin American, South Asian, Caribbean …) – that is
  welcome. Keep them respectful and general, not a copy of a real famous building.

## How the game fits each model

- **Box fit** = stretched on each axis to the exact size the game needs (houses, parapets, planks …).
- **Height fit** = scaled evenly so its height matches (props). **Length fit** = scaled evenly to a length.
- **Natural** axes in a box fit (marked "own") keep the model's own size.

## Houses

| Name | What it is | Size (m) | Pivot | Tris (mobile) | Notes |
|---|---|---|---|---|---|
| `house_01` … `house_08` | Town houses, 2–6 storeys, windows, doors, balconies, shutters. 8 variants for variety (any number 1–8 is fine – missing ones just repeat the others). | Typical 8–14 wide × 6–18 high × 8–14 deep; **box fit** to each block | bottom-centre | ≤ 2,500 (1,000–2,000 ideal) | **Flat roof, top of the model = roof floor** (players stand there). **No parapet** on top (the game adds parapets with gaps for planks / ladders). No pitched roofs, no antennas sticking up (those are separate props). Turned 90° by the game when that fits a block better, 180° on odd variants. |

Where houses are used:
- Arena roofs and nearby houses (ArenaBuilder → `PropFactory.House`).
- The town around the arena (CityFiller): **only the near band** (about 150 m on phones, 210 m on PC), **at most 300 model
  instances (180 on phones)** including their roof tanks / stairwells; everything further away stays merged code-built meshes.
  Everything is static-batched.

## Rooftop props

| Name | What it is | Size (m) | Fit | Pivot | Tris (mobile) |
|---|---|---|---|---|---|
| `roof_parapet` | One straight low wall piece along **X** (with a coping lip on top) | ~2 long × 0.9 high × 0.22 thick | box: length × 0.9 (0.8 on small roofs) × 0.22 | bottom-centre | ≤ 150 |
| `water_tank` | Rooftop water tank on a small stand (plastic or steel drum) | ~1.4 wide × 2.6 high | height 2.6 (city: 2.0–2.5) | bottom-centre | ≤ 600 |
| `charpai` | Woven rope bed / day bed on four legs (any culture's simple roof bed or bench works) | ~1 × 0.5 × **2 long along Z** | length 2 | bottom-centre | ≤ 400 |
| `railing` | Straight railing piece along **X** (posts + rail) – used beside roof stairs | ~2 long × 0.95 high | box: length of the stairs × 0.95 | bottom-centre | ≤ 250 |
| `cable_pole` | Wooden / concrete utility pole (cables are drawn by the game) | ~0.35 wide × 5–9 high | height (5 m on roofs, block height on the ground) | bottom-centre | ≤ 300 |
| `ladder` | Upright ladder, **along Y**, rungs across X, ~3 m | 0.55 × 3 × 0.1 | box: length = climb | bottom-centre | ≤ 300 |
| `plank` | Wooden plank / board bridge, **along Z** | 0.55 × 0.08 × ~3 | box: length = gap + 0.8 | bottom-centre | ≤ 60 |
| `rope_bridge` | Short rope bridge, **along Z**, straight and level (deck at the bottom, hand ropes ~1 m up). Cotton / hemp rope look, wooden slats. | ~1.2 × 1.1 × ~4 | box: length = span | bottom-centre (under deck middle) | ≤ 1,200 |
| `stairwell` | Small stair hut on the roof (door facing −Z) | ~2.2 × 2.5 × 2.2 | height 2.5 | bottom-centre | ≤ 600 |
| `stairs` | One concrete / wooden flight between two roofs, rising towards **+Z**, rail included | 1.2 wide × rise × run | box: 1.2 × rise × run | bottom-centre of the footprint | ≤ 600 |
| `clothes_line` | Two small poles + line + some cloths, **along X** | ~4 long × 1.8 high | box: length = line | bottom-centre | ≤ 500 |
| `plant_pot` | Clay pot with a plant | ~0.5 × 0.85 high | height 0.85 | bottom-centre | ≤ 300 |
| `dish_antenna` | Small TV dish on a short mast (no brand logo) | ~0.7 × 1.3 high | height 1.3 | bottom-centre | ≤ 300 |
| `flag_pole` | Thin pole with a plain flag / pennant (no real country or brand flag) | ~1.2 × 4 high | height 4 | bottom-centre | ≤ 200 |
| `tree` | Leafy tree (ground, between houses / fields) | ~4–6 wide × 6–10 high | height = block height | bottom-centre | ≤ 1,500 |
| `palm_tree` | Palm tree (beach / coastal arenas) | ~4 wide × 8–12 high | height = block height | bottom-centre | ≤ 1,200 |
| `market_stall` | Festival / market stall with a cloth awning (no shop names) | ~3 × 2.6 × 2 | box: stall block | bottom-centre | ≤ 1,200 |

Not replaced by models (they stay code-built on purpose): roof floors and rugs, crate marks, jump marks, fairy lights,
dummy target kites, the waiting-plaza decks, cables, the far skyline and the soft hills / ridges of open arenas.

## Free CC0 packs to try (owner: always check the licence yourself)

- **Kenney – City Kit (Commercial), City Kit (Suburban), Building Kit, Furniture Kit** – kenney.nl, CC0. Good starting
  houses, props, stairs, railings.
- **Quaternius** – quaternius.com, many CC0 packs (buildings, town / village props, nature: trees, palms).
- **Poly Pizza** (poly.pizza) – mixed licences: only use models marked CC0, or credit CC-BY ones.
- **OpenGameArt.org** – filter by CC0.

Owner: please read each pack's licence page, keep a credits note (e.g. `docs/WORLD_ASSET_CREDITS.md`), and never use
models ripped from other games or paid packs you do not own.
