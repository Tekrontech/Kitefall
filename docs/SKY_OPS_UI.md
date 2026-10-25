# Sky Ops UI + graphics upgrade (v1.5)

Goal: Free Fire-class menus and a punchier 3D picture, without new image files. Everything is generated in code, so it
works the moment the project opens.

## What changed (simple English / Roman Urdu)

### UI – har screen ek saath badli
| File | Change |
|---|---|
| `UI/Theme.cs` | New palette: dark slate glass, amber call-to-action (`Accent`), cyan selection (`Accent2`), red danger, rarity colours incl. Mythic. |
| `UI/Glossy.cs` | New skins with **cut corners** (top-left + bottom-right), cool-to-amber rim, gloss. New `Style.Red`. Amber/green buttons get **dark text**. Painted `ui_*` button images are now off by default – set `Glossy.UseArtistSkins = true` to use them again. |
| `UI/Sprites.cs` | `Rounded` boxes are cut-corner too (inputs, cards, toasts). New `Orb` (HUD buttons), `Slant` (underline, meters), `Hatch` (diagonal lines). |
| `UI/UiKit.cs` | Condensed fonts: **Barlow Condensed** (SIL OFL, `Resources/Fonts`) for buttons and titles; `Caps`, `Display`, `UseButtonFont`, `SetSkin`. Big bold text (≥ 40) uses the italic display face. |
| `UI/Screens/MenuScreen.cs` | New frame for **every menu**: blurred home painting backdrop + hairlines, square back button, amber kicker + big italic title + slanted underline, currency pill with amber “+”. |
| `UI/Screens/HomeScreen.cs`, `OnboardingScreens.cs`, `UIManager.cs`, `Hud/MatchHudScreen.cs` | Home details, new Splash + Sign-in layout, popup titles, PULL / DIVE / REEL as glossy orbs. |
| `UI/DisabledSkin.cs` | Disabled buttons use a readable grey label (dark text on grey was invisible). |
| `UI/Icons.cs` | New `close` icon; the kit's faint chevron / clock glyphs are replaced by the code-drawn ones. |

Design reference (all 12 screens): the "Kitefall Sky Ops UI" design canvas.

### Bugs fixed while restyling
- **Tabs / choices / ON-OFF** only changed the tint on click, so the orange skin went brown and the blue one went orange. `UiKit.SetSkin` now swaps the real skin.

### 3D graphics
| Audit id | Fix |
|---|---|
| G2 | `UrpGraphics` changed the project's URP asset directly (in the Editor this saved into the asset file). It now works on a runtime copy. |
| G3 | MSAA + FXAA together on High/Medium → FXAA removed (sharper, cheaper). |
| G4 | A new sky material was made every arena and never freed → the old runtime sky is destroyed. |
| G5 | Every lit material was double-sided → the town's closed meshes (`CityFiller`) are now single-sided (half the pixel work, no wall shadow acne). `Materials.Lit(..., doubleSided: false)`. |
| G6 | Sky shader ran a second 4-octave noise per pixel → one noise tap. |
| G7 | Linear fog from 160 m → exponential-squared haze (clear near, thick far; edge of the world still hidden). |
| Grade | Bloom stronger, ACES, saturation +18, contrast +12, warm white balance, teal-shadow / warm-highlight split toning (High), vignette. |

## Still needs artists (code cannot fix this)
- **Characters:** 18 of 19 models have 256×256 textures. Free Fire-level needs ~15–25k triangles, 1024–2048 textures and a normal map per character.
- **`home_background.jpg`** (Incoming / design kit) has smeared rectangles where old UI was removed. Please regenerate a clean 1920×1080 painting; the menus blur it, Sign-in and Home show it sharp.
- Buildings and rooftop props: modular art kits through `WorldAssetLibrary` / Image Mapper.

## Check in Unity
1. Open the project, let it import the two new fonts.
2. Kitefall ▸ Build Game Scene, Play: Splash → Sign-in → Home → every menu (Hangar, Shop, Settings…) → a match (HUD buttons) → Results.
3. Settings ▸ Graphics: Low / Medium / High – the URP asset file in `Assets/Settings` must **not** change after Play.
