# Kitefall – Audio list

The game already makes every sound in code (`Assets/Scripts/Services/ProceduralAudio.cs`), so it is never silent.
To use a real recording, drop a file with the exact name below into:

```
Assets/Resources/KitefallAudio/
```

(create the folder if it is not there). Example: `Assets/Resources/KitefallAudio/wind_loop.ogg`.
The audio service (`AudioService.cs`) loads the file first; if a name is missing it uses the code-built sound.
Nothing else to set up – press Play. The Console shows `[Kitefall] Audio: N sound file(s) from Resources/KitefallAudio`.

Volumes follow the player's settings: **Music** slider = all `music_*` and `dhol_loop`, **Sound effects** slider = everything
else, **Master** = all.

## Rules (read first)

- **Original or CC0 only.** No copyrighted music, no film / TV / pop songs, no "royalty-free" packs whose licence you have
  not read, no recordings of real broadcasts, brands or jingles. Keep the source link + licence of every file in a credits
  note (e.g. `docs/AUDIO_CREDITS.md`).
- **Kite string = cotton thread.** Line sounds are soft cotton / silk-like thread: a dry "ffff" flutter or hiss, a dull
  "tk" when it breaks. **No** metal ring, wire twang, glass scrape, blade or injury sounds.
- **International feel.** Music is welcoming worldwide; regional instruments (dhol, frame drums, guitars, flutes …) are
  flavour, used respectfully, not a joke or a stereotype.
- **No ads, no voice-overs of real people.**

## Files

| Name | What it is | Length | Loop | Where it plays | Loudness hint |
|---|---|---|---|---|---|
| `wind_loop` | Open-air wind, soft gusts, no birds/voices | 6–20 s | **Yes** (seamless) | Always (menus quiet, matches louder with wind speed; pitch rises with wind) | quiet bed, about −24 LUFS |
| `tension_loop` | Cotton thread under strain: dry, fine hiss / flutter of a tight cotton line in the wind | 2–6 s | **Yes** | In a match, louder and higher as your line tension rises | quiet, about −26 LUFS |
| `contest_loop` | Two cotton threads rubbing (pech / line contest): soft rhythmic friction, no metal | 2–6 s | **Yes** | While your line is crossing another kite's line | medium, about −22 LUFS |
| `snap` | Cotton thread breaking: short dull "tk / pop" + tiny flutter | 0.2–0.5 s | No | Your line snaps (over-tension) | peak about −3 dBFS |
| `cut` | Winning a contest: thread slips free, soft "zip" + light whoosh of the cut kite | 0.5–1.2 s | No | A kite is cut | peak about −3 dBFS |
| `crowd_cheer` | Friendly crowd cheer / clapping on rooftops (no words, no chants of names) | 1.5–3 s | No | Cuts, wins, special moments | peak about −6 dBFS |
| `crowd_ooh` | Crowd "ooooh!" – tense surprise | 1–2 s | No | When your line starts crossing another (max once per 8 s) | peak about −8 dBFS |
| `dhol_loop` | Festival hand-drum groove (dhol or any frame / hand drum), steady tempo ~90–120 BPM, no melody | 4–8 bars | **Yes** | Quietly under the match music, a bit louder during a contest. Silent if no file (there is no code-built drum). | about −22 LUFS |
| `ui_click` | Small soft button tap (wood / paper tick) | 0.03–0.1 s | No | Every button | peak about −10 dBFS |
| `ui_confirm` | Positive "yes / go" (2 soft rising notes) | 0.2–0.5 s | No | When a match starts (after PLAY); code can call `Game.Audio.Play(Sfx.Confirm)` elsewhere | peak about −8 dBFS |
| `music_home` | Calm, warm menu music, festival / rooftop mood, international instruments | 1.5–3 min | **Yes** (loop point clean) | Home and all menus | about −18 LUFS |
| `music_match_1` | Upbeat match music, light percussion, leaves room for wind & SFX | 1.5–3 min | **Yes** | Matches (one of the match tracks is picked at random) | about −18 LUFS |
| `music_match_2` | Second match track, different key / instruments | 1.5–3 min | **Yes** | Matches (random) | about −18 LUFS |

Optional extra names (also picked up when present, otherwise code-built): `whoosh` (kite dive / dash, 0.4–0.8 s),
`gust` (wind gust, 1.5–3 s), `spark` (line contest friction burst, 0.2 s – soft, no metal), `win` (victory stinger, 1–2 s),
`lose` (defeat stinger, 1–2 s).

If there is no `music_match_*` file, the match plays the code-built track of the arena's style; if there is no
`music_home`, menus play the code-built old-city loop.

## Format

- **OGG Vorbis** (best for music and loops) or **WAV** (16-bit PCM), **44.1 kHz**. Avoid MP3 for loops (it adds a gap).
- Mono for all sound effects and the line loops; stereo is fine for music, `wind_loop` and `dhol_loop`.
- Loops must start and end at the same level and phase (no click when they repeat). Trim silence at the start of one-shots.
- Normalise: one-shots peak no higher than −1 dBFS; music around −16 to −18 LUFS integrated (phones are loud).

Recommended Unity import settings (select the file ▸ Inspector):

| Kind | Load Type | Compression | Force To Mono |
|---|---|---|---|
| `music_*` | Streaming | Vorbis, quality 50–70 % | off |
| `*_loop` | Compressed In Memory | Vorbis, quality 60 % | on (except wind / dhol, optional) |
| one-shots (`snap`, `cut`, `ui_*`, `crowd_*`) | Decompress On Load | ADPCM or PCM | on |

## Where to find CC0 sounds (always check each file's licence yourself)

- **freesound.org** – search, then filter **Licence: Creative Commons 0**. Good for wind, crowd, cloth / thread flutter,
  hand drums. (Files marked "Attribution" are not CC0 – credit them or skip them.)
- **Kenney.nl** – "Interface Sounds", "UI Audio", "Impact Sounds", "Music Jingles" packs are **CC0** (great for `ui_click`,
  `ui_confirm`, `win`, `lose`).
- **OpenGameArt.org** – filter by **CC0**; has loopable music and ambience.
- Record your own: a cotton thread pulled tight near a microphone, rubbing two cotton threads, a real hand drum.

Owner: please check every licence before adding a file. When in doubt, leave it out – the code-built sound is used.
