# Milestone 15 – real online smoke test (2 Android phones)

The online layer was written from the package documentation and compile-checked against stubs. Before testing the social
features (squad, warmup, voice), run this once on **two real phones** and send me every error from the Unity Console /
`adb logcat -s Unity`.

## Prepare
1. Finish `docs/ONLINE_SETUP.md` steps 1–4 (Cloud project, packages, Online Config, Cloud Code deploy).
2. Build a **Development** APK (Kitefall ▸ Build ▸ Android APK – Development) and install it on both phones.
3. Both phones on the internet (Wi-Fi or mobile data – Relay works across networks).
4. Optional: connect phone A by USB and run `adb logcat -s Unity` on the computer.

## A. Sign-in and server economy
| Step | Expected |
|---|---|
| Open the game on phone A, accept terms, pick a name | Home screen, Settings ▸ Account shows "Backend: Unity Gaming Services" |
| Claim the daily login | Coins go up; Cloud Save (dashboard ▸ Player Management) shows the protected `profile` key |
| Equip a free character in the Wardrobe (Sana) | Works; equipping Lucas (gems) without gems is refused |

## B. Private Room (2 phones)
| Step | Expected |
|---|---|
| Phone A: Home ▸ Private Room ▸ *Create online room* (Free-for-all, 2 players) | A 6-letter code appears |
| Phone B: Private Room ▸ *Join with code* | Both see "2/2 players"; the match starts |
| Warmup (10 s): walk with the joystick, send an emote and a quick chat | Each phone sees the other character walking, the emote and the chat bubble |
| Roof select: both tap roofs | A taken roof shows grey on the other phone; first tap wins |
| Launch ritual | Both kites go up; ritual grades only show on your own phone |
| Fly, cross lines | Contest meter on both phones; a cut shows on both with the same feed row |
| Phone B: pack up ▸ cross a link ▸ launch here | Phone A sees B's character walk/climb across and launch again |
| End of match | Both see results; rewards arrive (phone A = host reports to Cloud Code) |

## C. Quick Duel (2 phones)
| Step | Expected |
|---|---|
| Both: Home ▸ Quick Duel ▸ Play at the same time (same region) | They find each other (else bots after 20 s) |
| Rounds | Mirrored roofs (same height), best of 3, round replay |

## D. Disconnect / reconnect
| Step | Expected |
|---|---|
| During a match turn phone B to airplane mode for 10 s, then back | B's kite holds (AI hold) and B reconnects into the same match |
| Phone B quits a Ranked match | B gets the quit penalty; A wins |

## What to send me
- The exact Console / logcat error text (first red error first) and which step it happened in.
- Screenshots of anything that looks wrong (positions jumping, characters frozen, wrong roof colours).

When A–D pass, test squads (Home ▸ Squad, 2–4 phones), Team Battle with a party, and voice (install `com.unity.services.vivox`).
