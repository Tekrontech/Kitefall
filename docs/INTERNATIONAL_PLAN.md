# Kitefall – International plan (D57)

**Goal:** a kite game for players everywhere, not only South Asia. Kite flying and kite fighting are loved worldwide, so
Kitefall shows many kite cultures side by side. South Asian rooftops stay as one much-loved arena, not the whole identity.

Status key: ✅ done · 🔜 proposed (needs owner OK) · ⚠️ check

## 1. Done now
| Area | Change |
|---|---|
| Project rule | `CLAUDE.md` + decision D57: every future change must work for a worldwide audience. |
| Art style (Excel prompts) | Master prompt changed from "South Asian miniature on wasli paper" to a **classical storybook world kite-festival** look (same gouache + gold, so the 131 images already made still fit). |
| Image Mapper | "Export missing list" button → `Assets/Art/missing_images.csv`. |

## 2. Words in the game (UI) 🔜
Players outside South Asia do not know *dheel, kheench, dor, charkhi, dum, guddi*. Proposal: **English first, local word as flavour**.

| Now | Proposed label | Note |
|---|---|---|
| Dheel | **Release** | Button + tutorial |
| Kheench | **Pull** | Button + tutorial |
| Dor | **Line** (sub-text "dor") | Loadout, shop |
| Charkhi | **Spool** (sub-text "charkhi") | Loadout, shop |
| Dum | **Tail** | Loadout |
| Bo Kata! | **Cut!** (emote keeps "Bo Kata!" as a fun regional emote) | Kill feed / banner |
| Guddi, Pipa, Pakpao… | Kite name + type: "Guddi – Fighter Diamond", "Rokkaku – Hexagon" | Kite cards |
| Basant (palette, event) | "Spring Festival" (Basant stays as a palette name) | Season events |

About 130 text strings in UI/App code (Dor 37, Charkhi 30, Dum 24, Guddi 22, Dheel 8, Basant 8, Kheench 5).

## 3. Languages 🔜
Use the Unity Localization package: English (default), Spanish, Portuguese (Brazil), French, Arabic (right-to-left),
Indonesian, Hindi, Urdu, Japanese, Turkish. Needs fonts with those scripts.

## 4. More kite cultures 🔜
**Kites.** All 7 kite classes now come from Asia. Add at least 3 from elsewhere, with stats balanced against the existing ones:
- Delta (USA/Europe), which is fast and stable.
- Box kite (Australia/UK), which is heavy and strong.
- Chilean *volantín* (a fighter kite).

Others to consider: Guatemalan *barrilete* (as a cosmetic giant kite), Bermuda kite, Greek *chartaetos*.

**Arenas.** Add these:
| Arena | Kite culture |
|---|---|
| Rio Hillside | Brazilian *pipas* over hillside houses |
| Santiago Rooftops | Chilean *volantines* in September |
| Mediterranean Harbour | Greece, Clean Monday kites |
| Atlantic Beach Festival | Big beach kite festivals in Europe |
| Caribbean Shore | Bermuda Good Friday kites |
| Bali Rice Terraces | Bali kite festival |

**Characters.** The 10 we have now are mostly from Asia, plus Lucas from Lisbon. Add these:
- Brazil, Chile, Mexico/Guatemala
- USA, UK/France
- Nigeria/South Africa, Egypt/Morocco
- Turkey, Greece

**Outfits.** Add global street and festival wear: denim jacket, windbreaker, poncho, beach shirt, track suit. Regional dress
(kurta, sherwani, kimono-style jacket, poncho) stays as optional cosmetics. The prayer cap stays optional and is not a default.

**Events.** Keep a "World Kite Calendar" season pass that rotates festivals:
| Season | Festivals |
|---|---|
| Jan | Uttarayan / Makar Sankranti |
| Spring | Basant, Clean Monday, Bermuda Good Friday |
| Summer | Beach festivals |
| Sep | Chile Fiestas Patrias |
| Nov | Guatemala giant kites |

## 5. Store, branding, legal ⚠️
- Store text and screenshots in English plus the main languages; no regional slang in the title.
- Age rating: keep it friendly; no real flags, no religious symbols in default looks.
- Data/privacy:
  - GDPR (EU) and COPPA (US under-13): the under-13 voice-off rule already exists.
  - Show a consent prompt where the law requires it.
- Prices: use the stores' local price tiers (already shown via the store's displayPrice).
- Servers: Unity Multiplayer region auto-select. Test from EU, Americas and Asia.

## 6. Images still to make (see `Kitefall_Image_Assets.xlsx`, filter Status / use "Export missing list")
- In the project now: 131 of 278 installed (plus more after the latest AUTO run).
- New images for section 4 (kites, arenas, characters, outfits) will be added to the Excel once the owner approves the list.
