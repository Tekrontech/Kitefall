# Kitefall – International plan (D57)

**Goal:** a kite game for players everywhere, not only South Asia. Kite flying and kite fighting are loved worldwide, so
Kitefall shows many kite cultures side by side. South Asian rooftops stay as one much-loved arena, not the whole identity.

Status key: ✅ done · 🔜 proposed (needs owner OK) · ⚠️ check

## 1. Done (international update)
Asia stays fully available – players choose. Images already made keep working (their # numbers did not change).

| Area | What changed |
|---|---|
| Project rule | `CLAUDE.md` + decision D57. |
| **Kite words** (Settings ▸ Gameplay) | **International** (default): Release · Pull · Line · Spool · Tail, item names "Cotton line", "Wooden spool"… / **South Asian**: Dheel · Kheench · Dor · Charkhi · Dum, Sooti, Lakri… Same gameplay. (`Core/Meta/Terms.cs`) |
| **Home scene** (Settings ▸ Gameplay) | World harbour (default) or Old city rooftops. |
| **Kites** | + Delta (USA/Europe), Box Kite (Australia/UK), Volantín (Chile) – stats inside existing ranges, 8 skins each. All 7 Asian kites stay. |
| **Arenas** | + Rio Hillside, Santiago Rooftops, Mediterranean Harbour, Atlantic Beach Festival, Caribbean Shore, Bali Rice Terraces (each shows its region on the arena picker). Old 6 arenas unchanged. Unity adds their data assets automatically (Kitefall ▸ Fix Game Scene Links / any build). |
| **Characters** | + Mateus (Rio), Valentina (Santiago), Diego (Guatemala), Emma (France), Jack (USA), Amara (Lagos), Youssef (Marrakesh), Elif (Istanbul), Nikos (Athens) → 19 characters, 18 home towns. |
| **Outfits** | + Denim Jacket, Windbreaker, Track Suit, Beach Shirt, Carnival Shirt, Andes Poncho, Festival Print → 19 outfits. Kurta, sherwani, topi stay optional. |
| **World Kite Calendar** | Home banner shows today's festival (Makar Sankranti, Basant, Clean Monday, Good Friday, Children's Day, beach festivals, Bali, Fiestas Patrias, Guatemala giant kites, Brazilian pipas) and picks its arena first. Cosmetic only. (`Core/Meta/KiteCalendar.cs`) |
| Art style (Excel) | Classical storybook world kite-festival style. 80 new image rows appended (#279–#358). |
| Image Mapper | "Export missing list" button. |

## 2. Words still South-Asian-only
Item ids (save data, servers) keep their original names – only display text changes. The "Bo Kata!" emote stays as a fun regional emote.

## 3. Languages 🔜 (next step)
Not done yet: needs a translator check and fonts for Arabic/Urdu (right-to-left shaping) and Japanese. Plan: Unity Localization package: English (default), Spanish, Portuguese (Brazil), French, Arabic (right-to-left),
Indonesian, Hindi, Urdu, Japanese, Turkish. Needs fonts with those scripts.

## 4. More kite cultures ✅ first batch done (see section 1) – ideas for later
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
