# Kitefall – Complete Setup Guide (Roman Urdu)

Yeh guide shuru se aakhir tak sab kuch batati hai: computer tayyar karna, project kholna, game chalana, phone par install karna, online services lagana, aur Play Store tak le jana.
Har hissa alag hai. **Hissa 1–8 offline game ke liye kaafi hain** (koi account ya key nahi chahiye). Online hissay (9–16) baad mein, jab chahein.

> **Zaroori:** Apni API keys, keystore passwords ya service-account secrets kabhi code mein, GitHub par ya chat mein mat dalein. Sirf Unity Dashboard / apne computer par rakhein.

---

## Fehrist (Contents)
1. [Computer ki zaroorat](#1-computer-ki-zaroorat)
2. [Unity Hub aur Unity 6 install karna](#2-unity-hub-aur-unity-6-install-karna)
3. [Code hasil karna (zip ya Git)](#3-code-hasil-karna)
4. [URP project settings copy karna (sirf ek dafa)](#4-urp-project-settings-copy-karna)
5. [Project kholna aur Input System](#5-project-kholna-aur-input-system)
6. [Game scene banana](#6-game-scene-banana)
7. [Game chalana, controls aur game ka tour](#7-game-chalana)
8. [Tests chalana](#8-tests-chalana)
9. [Android phone par chalana (APK)](#9-android-phone-par-chalana)
10. [Git mein save karna](#10-git-mein-save-karna)
11. [Numbers / balance tabdeel karna](#11-numbers-balance-tabdeel-karna)
12. [Online: Unity Cloud project + packages](#12-online-unity-cloud-project--packages)
13. [Online: Server economy (Cloud Code) deploy](#13-online-server-economy-cloud-code)
14. [Online: Leaderboard, Multiplayer, Ranked](#14-online-leaderboard-multiplayer-ranked)
15. [Online: Ads (LevelPlay) aur In-App Purchases](#15-online-ads-aur-in-app-purchases)
16. [Online: Remote Config, Analytics, Google/Apple sign-in](#16-online-remote-config-analytics-sign-in)
17. [Play Store par release ki checklist](#17-play-store-release-checklist)
18. [Masle aur unke hal (Troubleshooting)](#18-masle-aur-unke-hal)
19. [Kya test hua hai aur kya nahi](#19-kya-test-hua-hai-aur-kya-nahi)

---

## 1. Computer ki zaroorat
| Cheez | Kam se kam | Behtar |
|---|---|---|
| OS | Windows 10/11 64-bit ya macOS 12+ | Windows 11 / macOS 14 |
| RAM | 8 GB | 16 GB |
| Disk | 25 GB khaali (Unity + Android tools + project) | SSD |
| Graphics | DirectX 11 / Metal support | koi bhi naya GPU |
| Internet | Pehli dafa install ke liye (kai GB download) | |

Phone (testing ke liye): Android 7.0 (API 24) ya naya, ARM64.

---

## 2. Unity Hub aur Unity 6 install karna
1. **unity.com/download** se **Unity Hub** download karein aur install karein.
2. Unity Hub kholein ▸ **Sign in** (Unity ID free banti hai).
3. Licence: Hub ▸ ⚙️ Settings ▸ **Licenses ▸ Add ▸ Get a free personal licence**.
4. Hub ▸ **Installs ▸ Install Editor** ▸ **Unity 6 LTS** (version `6000.x`, "LTS" likha ho) chunein.
5. Modules wali screen par yeh tick karein:
   - ✅ **Android Build Support**
     - ✅ **OpenJDK**
     - ✅ **Android SDK & NDK Tools**
   - ✅ **Microsoft Visual Studio Community** (Windows par; Mac par Visual Studio Code ya JetBrains Rider bhi chalega)
   - (iPhone ke liye: **iOS Build Support** – sirf Mac par build hoga)
   - (Ranked dedicated server ke liye, baad mein: **Linux Dedicated Server Build Support**)
6. **Install** dabayein aur poora hone dein (20–60 minute lag sakte hain).

> Agar baad mein koi module bhool gaye: Hub ▸ Installs ▸ apne Unity ke saath ⚙️ ▸ **Add modules**.

---

## 3. Code hasil karna

### Tareeqa A – Zip (aasaan)
1. Jo `Kitefall_Complete.zip` file mili hai, usay download karein.
2. Right-click ▸ **Extract All** ▸ aisi jagah jahan path chhota ho aur space na ho, maslan `D:\Kitefall` (Windows) ya `~/Projects/Kitefall` (Mac).
   - Zip ke andar `Kitefall` naam ka folder hai. Uske andar `Assets`, `docs`, `Server`, `Tools` folders hone chahiyein.

### Tareeqa B – Git (updates lene ke liye behtar)
1. **git-scm.com** se Git install karein.
2. Terminal / Command Prompt mein:
   ```bash
   git clone https://github.com/tekrontech/kitefall.git
   cd kitefall
   git checkout claude/amazing-bardeen-xi1xqo
   ```
3. Naye updates lene ke liye baad mein: `git pull`.

---

## 4. URP project settings copy karna
Repo mein game ka saara code hai, lekin Unity ki apni `Packages` aur `ProjectSettings` files nahi (yeh aap ke exact Unity version se bannay chahiyein). Yeh **sirf ek dafa** karna hai:

1. Unity Hub ▸ **Projects ▸ New project**.
2. Upar Editor version **Unity 6 LTS** chunein.
3. Template: **Universal 3D** (URP). (Agar "Download template" likha ho to pehle download karein.)
4. Project name: `KitefallTemplate`, koi bhi location ▸ **Create project**.
5. Unity khul jaye aur poora load ho jaye to **Unity band kar dein** (File ▸ Exit).
6. Ab `KitefallTemplate` folder se yeh cheezein copy karke apne `Kitefall` folder mein paste karein (agar poochay to **Replace**):
   | Copy karein | Kahan |
   |---|---|
   | `Packages` (poora folder) | `Kitefall/Packages` |
   | `ProjectSettings` (poora folder) | `Kitefall/ProjectSettings` |
   | `Assets/Settings` folder **aur** `Assets/Settings.meta` | `Kitefall/Assets/` |
   | `Assets/UniversalRenderPipelineGlobalSettings.asset` + `.meta` (agar mojood ho) | `Kitefall/Assets/` |
7. Yeh **copy mat karein**: `SampleScene`, `TutorialInfo`, `Readme.asset`, `Library`, `Temp`.
8. Ab `KitefallTemplate` delete kar sakte hain.

Aakhir mein `Kitefall` folder aisa dikhna chahiye:
```
Kitefall/
├─ Assets/
│  ├─ CloudCode/  RemoteConfig/  Scripts/  Tests/
│  └─ Settings/            ← template se aaya
├─ Packages/               ← template se aaya
├─ ProjectSettings/        ← template se aaya
├─ Server/  Tools/  docs/
└─ README.md
```

---

## 5. Project kholna aur Input System
1. Unity Hub ▸ **Projects ▸ Add ▸ Add project from disk** ▸ `Kitefall` folder chunein.
2. List mein Kitefall par click karein (Unity 6 LTS version ke saath). Pehli dafa kholne mein kuch minute lagte hain (Library ban rahi hoti hai).
3. Agar "Safe Mode" ka sawal aaye: **Enter Safe Mode** ya **Ignore** – phir Console ke errors dekhein (Hissa 18).
4. **Window ▸ Package Manager** ▸ upar baayen **Unity Registry** ▸ search `Input System`:
   - Agar "Installed" nahi to **Install** dabayein.
   - Popup aaye "enable the new input backends?" ▸ **Yes** (Unity restart hoga).
5. **Edit ▸ Project Settings ▸ Player ▸ Other Settings ▸ Active Input Handling** = **Input System Package (New)** ya **Both**.
6. Package Manager mein yeh bhi "Installed" hon (Universal 3D template mein aam taur par pehle se hote hain):
   - `Universal RP`
   - `Unity UI` (uGUI)
7. **Window ▸ General ▸ Console** kholein. **Laal (red) errors 0** hone chahiyein. Peelay (yellow) warnings theek hain.

---

## 6. Game scene banana
Upar menu bar mein ab **Kitefall** menu nazar aayega.

1. **Kitefall ▸ Build Game Scene (complete game)** click karein. Yeh khud bana deta hai:
   - `Assets/ScriptableObjects/` – 7 kite classes, 5 dum (tails), 6 dor, 5 charkhi, 6 tunings, 6 arenas, wind/flight/combat/camera configs, `BalanceConfig`, `GameDatabase`
   - `Assets/Materials/Runtime/` – URP material templates
   - `Assets/Scenes/Main.unity` – game ka scene (Build Settings mein pehla)
   - Landscape player settings
2. Agar kabhi data reset karna ho: **Kitefall ▸ Data ▸ Reset All Data Assets To Spec Values**.
3. Mobile settings dobara lagani hon: **Kitefall ▸ Setup ▸ Apply Mobile Player Settings**.

Doosre menu:
| Menu | Kaam |
|---|---|
| Kitefall ▸ Open Game Scene | Main.unity kholta hai |
| Kitefall ▸ Data ▸ Create Missing Data Assets | sirf ghayab assets banata hai |
| Kitefall ▸ Milestone 1 ▸ Build Flight Test Scene | purana flight lab (sirf udaane ki practice) |
| Kitefall ▸ Help ▸ Open Game Spec | spec file kholta hai |
| Kitefall ▸ Online ▸ … | online config (Hissa 12) |

---

## 7. Game chalana
1. `Assets/Scenes/Main.unity` kholein (ya Kitefall ▸ Open Game Scene).
2. **Game** tab mein upar resolution: **1920x1080 Landscape** chunein. Phone jaisa dekhna ho to Game tab ke dropdown se **Simulator** ▸ koi Android phone.
3. Upar ▶ **Play** dabayein.

### Controls
| Kaam | Keyboard (PC) | Gamepad | Phone (touch) |
|---|---|---|---|
| Kite ghumana (steer) | **W A S D** ya **Arrow keys** | Left stick | Baayen taraf joystick |
| **Pull** (khainchna – tez, tension barhti hai) | **Space** (daba kar rakhein) | Right trigger | PULL button |
| **Dheel** (dor chhorna) | Pull chhor dein | – | button chhor dein |
| **Reel** (dor lapetna) | **Left Shift** | Left trigger | REEL button |
| **Dive** | **Q** ya **Left Ctrl** | X / Square (west) | DIVE button / neeche swipe |
| Reset (practice) | Backspace | Select | – |

Tension bar par nazar rakhein: zyada der Pull karne se dor **khud toot jaati hai** (self-snap).

### Pehli dafa game mein kya hoga
1. Terms/age screen ▸ naam chunein ▸ **Tutorial** (udaana, pull/dheel, pench).
2. **Home** screen: Quick Duel, Free-for-All, Team, Last Kite Flying, Ranked, Private Room, Practice.
3. **Hangar**: kite, dum, dor, charkhi, tuning chunein. **Workshop**: upgrades. **Shop**: coins/gems (offline mein test purchases).
4. Match ke baad **Results** screen batati hai ke kis pench mein kyun jeete/haare.
5. Offline profile computer par file mein save hota hai (`kitefall_profile.json` Unity ke persistentDataPath mein). Nayi shuruaat: **Settings ▸ Account ▸ Delete local save**.

Offline mein saare modes **bots** ke saath chalte hain – koi internet nahi chahiye.

---

## 8. Tests chalana
**Unity ke andar:** Window ▸ General ▸ **Test Runner** ▸ **EditMode** tab ▸ **Run All** → 202 tests hare (green) hone chahiyein.

**Unity ke baghair** (developer ke liye, .NET 8 SDK chahiye – dotnet.microsoft.com):
```bash
cd Kitefall
dotnet test Tools/CoreTests
```

---

## 9. Android phone par chalana

### 9.1 Phone tayyar karna
1. Phone ▸ Settings ▸ About phone ▸ **Build number** par 7 dafa tap karein (Developer options khul jayenge).
2. Settings ▸ Developer options ▸ **USB debugging** ON.
3. USB cable se computer se jorein, phone par **Allow USB debugging** ▸ OK.

### 9.2 Unity mein Android platform
1. **File ▸ Build Profiles** ▸ **Android** ▸ **Switch Platform** (pehli dafa kuch der lagegi).
2. **Player Settings** (usi window se ya Edit ▸ Project Settings ▸ Player) ▸ Android tab:
   | Setting | Value |
   |---|---|
   | Company Name | apni company, maslan `Tekrontech` |
   | Product Name | `Kitefall` |
   | Package Name (Other Settings ▸ Identification) | `com.tekrontech.kitefall` (ek dafa Play Store par chala gaya to badal nahi sakta) |
   | Version / Bundle Version Code | `1.0.0` / `1` (har naye upload par code +1) |
   | Minimum API Level | **24** ya zyada |
   | Target API Level | **Automatic (highest installed)** |
   | Scripting Backend | **IL2CPP** |
   | Target Architectures | **ARM64** ✅ (ARMv7 band kar sakte hain) |
   | Default Orientation | Landscape (Build Game Scene khud set karta hai) |
3. Build Profiles ▸ **Scenes** list mein `Assets/Scenes/Main.unity` pehle number par ho.

### 9.3 Build aur install
**Sab se aasaan – Kitefall menu se (ek click):**
| Menu | Kya banta hai |
|---|---|
| Kitefall ▸ Build ▸ **Android APK (install on phone)** | `.apk` – seedha phone mein install hoti hai |
| Kitefall ▸ Build ▸ **Android APK – Development** | `.apk` + errors dikhane wala mode (testing) |
| Kitefall ▸ Build ▸ **Android APK – Build and Run on USB phone** | build karke USB wale phone par khud chala deta hai |
| Kitefall ▸ Build ▸ **Android AAB (Google Play upload)** | `.aab` – sirf Play Store par upload ke liye (phone mein install nahi hoti) |
| Kitefall ▸ Build ▸ **Open Builds Folder** | `Builds/Android` folder kholta hai |

Yeh menu khud Android par switch karta hai, scene link check karta hai, package name / IL2CPP / ARM64 / API 24 set karta hai, aur build ke baad folder khol deta hai.

**Haath se (Build Profiles):**
- Phone jura ho ▸ Build Profiles ▸ **Build And Run** ▸ APK ka naam dein ▸ game phone par khul jayegi.
- Ya sirf **Build** karke `.apk` file phone par copy karke install karein (phone par "Install unknown apps" allow karna hoga).

### 9.4 Release ke liye keystore (Play Store)
1. Player Settings ▸ **Publishing Settings ▸ Keystore Manager ▸ Create New ▸ Anywhere**.
2. Keystore file **project folder se bahar** save karein (maslan `D:\Keys\kitefall.keystore`).
3. Password aur alias likh kar mehfooz jagah rakhein. **Yeh file ya password kho gaya to app update nahi kar sakenge.**
4. Keystore kabhi GitHub par na dalein (`.gitignore` pehle se `*.keystore` / `*.jks` ko rokta hai).
5. Play Store ke liye: Build Profiles ▸ **Build App Bundle (Google Play)** ✅ ▸ `.aab` file banegi.

---

## 10. Git mein save karna
Unity pehli dafa kholne par har file ke saath `.meta` files banata hai – yeh zaroori hain. In sab ko commit karein:
```bash
cd Kitefall
git add .
git commit -m "Unity project settings, meta files and generated data"
git push
```
`Library/`, `Temp/`, `Builds/` aur keystores `.gitignore` ki wajah se khud nikal jaate hain.

---

## 11. Numbers / balance tabdeel karna
Saare numbers (spec ke mutabiq) ScriptableObjects mein hain – code chhoone ki zaroorat nahi:
| Asset (`Assets/ScriptableObjects/`) | Kya control karta hai |
|---|---|
| Kites / Tails / Dors / Charkhis / Tunings | har item ke stats aur qeemat |
| `FlightConfig` | udaan ki speed, turn, dive |
| `WindConfig` | hawa ki taqat, jhonkay |
| `CombatConfig` | pench formula, sweet zone, attack lock |
| `CameraConfig` | camera ka faasla aur smoothness |
| `BalanceConfig` | rounds, crates, coins/gems inaam, ads ki hadd, ranks, matchmaking |
| `Arenas/*` | 6 maidan |

Asset select karein ▸ **Inspector** mein value badlein ▸ Play. Wapas spec values: **Kitefall ▸ Data ▸ Reset All Data Assets To Spec Values**.
Online game mein server bhi yahi rules chalata hai – agar economy numbers badlein to Cloud Code module dobara deploy karein (Hissa 13).

---

## 12. Online: Unity Cloud project + packages
> Har online hissa **khud ON** hota hai jab uska package install ho. Koi package na ho to game offline chalti rahegi. Check: **Kitefall ▸ Online ▸ Show Installed Online Packages**.

### 12.1 Cloud project
1. **Edit ▸ Project Settings ▸ Services** ▸ apni organisation chunein ▸ **Create project** (ya pehle wala link karein).
2. Browser mein **cloud.unity.com** ▸ apna project ▸ **Environments**: `development` aur `production` banayein (development aksar pehle se hota hai).

### 12.2 Packages install karna
**Window ▸ Package Manager ▸ + ▸ Add package by name…** aur ek ek karke:
| Package naam | Kya chalata hai | Zaroori? |
|---|---|---|
| `com.unity.services.authentication` | Guest / Google / Apple login | online ke liye haan |
| `com.unity.services.cloudcode` | server par economy (coins, rewards) | online ke liye haan |
| `com.unity.services.deployment` | Editor se Cloud Code / config deploy | haan |
| `com.unity.services.leaderboards` | global / country leaderboard | ikhtiyari |
| `com.unity.services.multiplayer` | online rooms, quick join, matchmaking | online match ke liye |
| `com.unity.netcode.gameobjects` | online match networking | multiplayer ke saath |
| `com.unity.remote-config` | bina update ke balance badalna | ikhtiyari |
| `com.unity.services.analytics` | analytics (consent ke saath) | ikhtiyari |
| `com.unity.services.levelplay` | asli ads | ikhtiyari |
| `com.unity.purchasing` (v5) | asli in-app purchases | ikhtiyari |

Har package ke baad Console check karein. Koi laal error aaye to Hissa 18 dekhein.

### 12.3 Online config asset
**Kitefall ▸ Online ▸ Create or Select Online Config** → `Assets/Resources/KitefallOnlineConfig.asset` banega. Inspector mein:
| Field | Value |
|---|---|
| Environment | `development` (testing), release par `production` |
| Cloud Code Module | `KitefallEconomy` |
| Ranked Leaderboard Id | `ranked` |
| Ranked Queue | `ranked-duel` |
| LevelPlay App Key / Ad Unit ids | Hissa 15 |
| Enable Online | ✅ (untick karein to zabardasti offline) |

---

## 13. Online: Server economy (Cloud Code)
Server par wohi game rules chalte hain jo game mein hain (`Assets/Scripts/Core`), is liye cheating se coins nahi badhaye ja sakte.

1. Computer par **.NET 9 SDK** install karein (dotnet.microsoft.com ▸ Download .NET 9 ▸ SDK). Check: terminal mein `dotnet --list-sdks` mein `9.x` dikhe.
2. Unity ▸ **Services ▸ Deployment** (window) ▸ upar environment **development** chunein.
3. List mein yeh teen tick karein:
   - `Assets/CloudCode/KitefallEconomy.ccmr` (server module)
   - `Assets/CloudCode/Kitefall.ac` (players ko leaderboard/economy mein khud likhne se rokta hai)
   - `Assets/RemoteConfig/kitefall.rc` (balance config)
4. **Deploy Selected**. Hara nishaan aaye to kaamyaab.
5. Check: cloud.unity.com ▸ **Cloud Code ▸ Modules** ▸ `KitefallEconomy` ke functions: `GetProfile`, `Run`, `ReportMatch`.
6. Game Play karein ▸ Guest sign-in ▸ profile ab server par (Cloud Save, protected) save hoga.

Note:
- **Ranked** results sirf dedicated server se qabool hote hain. Server ke baghair test karna ho to `Server/KitefallEconomy/KitefallEconomy/KitefallEconomy.cs` mein `AllowPlayerHostedRanked = true` karke dobara deploy karein. **Release se pehle wapas `false`.**
- Deploy fail ho aur .NET 9 na mile: SDK install karke Unity restart karein.

---

## 14. Online: Leaderboard, Multiplayer, Ranked

### 14.1 Leaderboard
cloud.unity.com ▸ **Leaderboards ▸ Create** ▸ ID **`ranked`** ▸ Sort: **Descending** ▸ Update: **Keep latest score** ▸ Save.

### 14.2 Online matches (Quick Duel, FFA, Team, Last Kite Flying)
- `com.unity.services.multiplayer` + `com.unity.netcode.gameobjects` install hon to Home ke modes online room dhoondte hain; na milay to naya room banta hai. 20 second mein room poora na ho to khaali jagah **bots** (label ke saath) bhar dete hain (Ranked mein kabhi nahi).
- Test: do instances chahiyein – ek Editor mein, ek build (PC build ya phone) mein. Dono ka **game version same** ho.
- Ek hi computer par 2–4 players test karne ke liye package `com.unity.multiplayer.playmode` install karein, phir **Window ▸ Multiplayer ▸ Multiplayer Play Mode** se virtual players ON karein.

### 14.3 Private room
Home ▸ **Private Room ▸ Create online room** ▸ join code dikhega ▸ dost **Join with code** se aayein ▸ **Start**.

### 14.4 Ranked Duel (matchmaker)
cloud.unity.com ▸ **Matchmaker ▸ Queues ▸ Create**:
- Name: **`ranked-duel`**
- 2 teams × 1 player
- Rule: rank 1 tier ke andar, 10 second baad 2 tier tak
- Region attribute, **bots off**

### 14.5 Dedicated server (Ranked ke liye)
1. Unity Hub se **Linux Dedicated Server Build Support** module add karein.
2. **File ▸ Build Profiles ▸ Linux Server** (Dedicated Server) ▸ Main.unity ke saath build.
3. Chalane ka command:
   ```bash
   ./Kitefall.x86_64 -port 7777 -mode RankedDuel -players 2 -wait 60 -arena OldCityRooftops
   ```
4. Server ke environment variables (keys code mein nahi, sirf server machine par):
   `KITEFALL_PROJECT_ID`, `KITEFALL_ENVIRONMENT_ID`, `KITEFALL_SA_KEY_ID`, `KITEFALL_SA_SECRET`
   (cloud.unity.com ▸ Administration ▸ **Service Accounts** ▸ naya account, Cloud Code call ki ijazat).
5. Kisi game-server hosting par yeh build chalayein aur `ranked-duel` queue ko us se jorein (Unity Dashboard mein mojooda hosting options ya third-party host).

---

## 15. Online: Ads aur In-App Purchases

### 15.1 Ads (LevelPlay)
1. **LevelPlay dashboard** (unity.com/products/mediation) par account ▸ **Add app** (Android, aur iOS agar chahiye).
2. App Key, **Rewarded** aur **Interstitial** ad unit ids copy karein ▸ `KitefallOnlineConfig` mein dalein.
3. Unity ▸ **Ads Mediation ▸ Network Manager** se ad networks add karein ▸ Android ke liye **Assets ▸ External Dependency Manager ▸ Android Resolver ▸ Force Resolve**.
4. Rules (code mein pehle se): gameplay ke dauran **koi ad nahi**; rewarded sirf player ki marzi se; interstitial ki rozana hadd `BalanceConfig ▸ Ads` mein; "No Ads" khareedne par interstitial band.
5. Consent: terms screen personalised ads ka poochti hai; 13 saal se kam = COPPA + non-personalised. EU/UK release se pehle Google UMP consent form, iOS par ATT prompt lagana hoga.

### 15.2 In-App Purchases (Unity IAP v5)
1. **Google Play Console** ▸ app banayein ▸ **Monetize ▸ Products ▸ In-app products** ▸ yeh IDs banayein:
   | Product ID | Type |
   |---|---|
   | `kitefall.gems.small` | Consumable |
   | `kitefall.gems.medium` | Consumable |
   | `kitefall.gems.large` | Consumable |
   | `kitefall.noads` | Non-consumable |
   | `kitefall.pass.premium` | Non-consumable |
   (iOS: App Store Connect mein same IDs.)
2. Play Console mein products tabhi chalte hain jab app ek dafa **Internal testing** track par upload ho chuki ho, aur test account **License testing** mein ho.
3. Flow: store ▸ receipt ▸ Cloud Code `grant_purchase` ▸ server gems deta hai ▸ phir order confirm.
4. ⚠️ **Production se pehle** `KitefallEconomy.ReceiptValid` mein asli receipt check (Google Play Developer API / App Store Server API) lagana zaroori hai. Credentials Cloud Code **Secret Manager** mein rakhein. Tab tak `production` environment mein server har purchase rad karta hai (dhoke se bachne ke liye).

---

## 16. Online: Remote Config, Analytics, Sign-in

### 16.1 Remote Config
`Assets/RemoteConfig/kitefall.rc` mein ek key **`balance`** hai (JSON). Jo yahan likhein woh game start par `BalanceConfig` ke upar lag jata hai, maslan:
```json
{ "economy": { "coinsWin": 200 }, "ads": { "interstitialDailyCap": 6 }, "contest": { "noise": 0.4 } }
```
Badal kar Services ▸ Deployment se deploy karein, ya cloud.unity.com ▸ Remote Config mein edit karein.

### 16.2 Analytics
`com.unity.services.analytics` install karein. Events sirf tab bhejte hain jab player ne terms screen par consent diya ho. Data cloud.unity.com ▸ Analytics mein.

### 16.3 Google Play Games / Apple sign-in (ikhtiyari)
- Guest login bina kisi setup ke chalta hai. Guest baad mein Profile ▸ **Link Google / Link Apple** kar sakta hai.
- **Google:** Google Play Games plugin (GitHub: playgameservices/play-games-plugin-for-unity) install karein, Play Console mein Play Games Services setup karein, aur cloud.unity.com ▸ Authentication ▸ **Google Play Games** provider add karein. Phir game start par (kisi bhi script mein):
  ```csharp
  Kitefall.Online.SocialSignIn.GooglePlayGamesAuthCode = () => {
      var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
      GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(true, code => tcs.SetResult(code));
      return tcs.Task;
  };
  ```
- **Apple:** "Sign in with Apple" plugin + Authentication mein Apple provider, aur `SocialSignIn.AppleIdToken` isi tarah set karein.

---

## 17. Play Store release checklist
- [ ] Package name final (`com.tekrontech.kitefall`) aur keystore mehfooz (Hissa 9.4)
- [ ] Version code har upload par +1
- [ ] IL2CPP + ARM64, **App Bundle (.aab)**
- [ ] Online Config ▸ Environment = `production`, module `production` environment mein deploy
- [ ] `AllowPlayerHostedRanked = false`
- [ ] Asli receipt validation laga hua (Hissa 15.2.4)
- [ ] LevelPlay mein asli app key (test mode off), consent form (EU/UK), iOS ATT
- [ ] Privacy policy ka URL (Play Console ▸ App content) – kya data jaata hai: account id, analytics (consent ke saath), ads id
- [ ] Data safety form, content rating questionnaire, target audience (bachon ke liye ho to Families policy)
- [ ] Store listing: icon 512×512, feature graphic 1024×500, kam se kam 2 screenshots (landscape)
- [ ] Pehle **Internal testing** ▸ phir **Closed testing** (naye personal accounts ke liye Google 12 testers × 14 din maangta hai) ▸ phir Production
- [ ] Spec ki safety: game mein kahin bhi sheesha/dhaat/nylon wali dor ka zikr nahi (pehle se aisa hi hai)

---

## 18. Masle aur unke hal
| Masla | Hal |
|---|---|
| Sab cheezein **gulabi (pink)** | URP settings copy nahi huin (Hissa 4). Copy karein, Unity restart, phir **Kitefall ▸ Build Game Scene** dobara. |
| `The type or namespace 'InputSystem' could not be found` | Input System package install karein (Hissa 5.4). |
| `UnityEngine.UI could not be found` | Package Manager ▸ **Unity UI** install karein. |
| **Kitefall** menu nazar nahi aata | Console mein laal errors hain – pehle woh theek karein (scripts compile nahi huin). |
| Keyboard / touch kaam nahi karta | Active Input Handling = New ya Both (Hissa 5.5). Game tab par ek dafa click karein. |
| Game tab mein UI ajeeb / kata hua | Game tab resolution **1920x1080 Landscape** karein. |
| Play par kaali screen | Main.unity khula ho; Build Game Scene dobara chalayein. |
| Android build: "SDK/NDK not found" | Unity Hub ▸ Installs ▸ ⚙️ ▸ Add modules ▸ Android SDK & NDK + OpenJDK. |
| Android build: Gradle error | Project path chhota rakhein (spaces/Urdu letters na hon), Library folder delete karke Unity dobara kholein. |
| Phone nazar nahi aata | USB debugging ON, cable data wali ho, phone par "Allow" dabayein. |
| `Unity.Services…` / `Unity.Netcode…` error package ke baad | Package version ka API naam mukhtalif ho sakta hai. **Poora error text** mujhe bhejein, ya woh package hata dein (online hissa khud off ho jata hai). |
| "Server unavailable" | Cloud Code module us environment mein deploy nahi hua, ya Online Config mein environment ka naam galat. |
| "Connecting to host" par atka | Dono players ka game version same ho; cloud.unity.com par Multiplayer/Relay enabled ho. |
| Purchase production mein fail | Receipt validation ke baghair yeh jaan boojh kar hai (Hissa 15.2.4). |
| Profile reset karna hai | Settings ▸ Account ▸ Delete local save. |

**Madad ke liye:** Console mein error par click karein ▸ neeche poora text copy karein ▸ mujhe paste kar dein (screenshot se behtar text hai).

---

## 19. Kya test hua hai aur kya nahi
| Hissa | Status |
|---|---|
| Core game rules (udaan, tension, pench, economy, bots, modes) | ✅ 202 automated tests pass |
| Saara Unity code | ✅ Unity ki DLLs ke against compile hua – ❗ lekin asli Unity Editor mein abhi chala kar nahi dekha |
| Cloud Code server module | ✅ asli Cloud Code packages ke saath build hua – deploy nahi hua (aap ka account chahiye) |
| Online code (login, multiplayer, ads, IAP) | ⚠️ sirf nakli (stub) libraries ke saath compile – asli packages lagne par chhote fixes ho sakte hain |

Pehli dafa kholne par koi error aaye to ghabrayein nahi – error ka text bhejein, main theek kar doonga.

Mazeed English docs: [`SETUP.md`](SETUP.md), [`ONLINE_SETUP.md`](ONLINE_SETUP.md), [`ARCHITECTURE.md`](ARCHITECTURE.md), [`TESTING.md`](TESTING.md), [`MILESTONES.md`](MILESTONES.md), [`DECISIONS.md`](DECISIONS.md).

---

## 20. Upgrade v1.4 – naye features (characters, rooftops, launch ritual, squad)
Naya code lene ke baad Unity mein **Kitefall ▸ Build Game Scene (complete game)** dobara chalayein (naya sky aur kite-paper material GameDatabase mein judta hai). Phir ▶ Play.

| Feature | Kahan / kaise test karein |
|---|---|
| Insani characters (W1) | Home par aap ka character rooftop par patang uda raha hai; dor **baayen haath** se nikalti hai |
| Shoulder camera (W2) | Flight mein camera character ke peeche-daayen; cross par kite camera; upar-baayen camera button = 10 s |
| Wardrobe (F1) | Home ▸ **Wardrobe** – character, outfit, accessories, emotes; swatch tap = try-on |
| Kill feed + spectate (F4, F5) | FFA mein cut ki rows icons ke saath; cut hone par cutter ko spectate, ◀ ▶ |
| Emotes (F6) | Daayen kinare emote button: daba kar ghumayein aur chhor dein |
| Training (F8) | Home ▸ **Training** – sab items unlocked, dummies, hawa ke controls |
| Squad (F2) | Home ▸ **Squad** ▸ Create party – code share karein (online 2–4 phones) |
| Warmup plaza (F3) | FFA ke shuru mein 10 s plaza par chalein, emote/chat karein |
| Roof choice + hopping (F9) | Map par roof tap karein; round mein **ROOF** button → pack up → plank/ladder se doosri roof → LAUNCH HERE |
| Launch ritual (F10) | TAP! (kanni) → HOLD · LET GO (toss) → TUG! → DHEEL; Settings ▸ Auto-prep |
| Quick chat + voice (F7) | Chat button (8 messages); voice ke liye Vivox package (ONLINE_SETUP ▸ 12) |
| Graphics (28) | Naya aasman, badal, kite paper roshni, ghar/chhaton par saaman; Settings ▸ Quality |

Har step ki tafseel: **[`UPGRADE_TEST_GUIDE.md`](UPGRADE_TEST_GUIDE.md)**. Online test (2 phones): **[`ONLINE_SMOKE_TEST.md`](ONLINE_SMOKE_TEST.md)**.

> Sach: yeh sab code abhi asli Unity Editor / phone par chala kar nahi dekha gaya (compile + 202 tests pass). Pehli dafa koi error aaye to Console ka pehla laal error bhejein.

## 21. Apni images game mein lagana (Image Mapper)

Images ki list, sizes aur AI prompts: **[`Kitefall_Image_Assets.xlsx`](Kitefall_Image_Assets.xlsx)** (278 slots).
**File ke naam kuch bhi hon – koi masla nahi.** Tool khud sahi naam rakh deta hai.

1. Pehle images chhoti karein (backgrounds JPG quality 85, Excel wale size par resize). Poora set ~60–100 MB ho.
2. Unity menu **Kitefall ▸ Art ▸ Open Incoming Folder** → saari images `Assets/Art/Incoming` mein copy karein.
3. **Kitefall ▸ Art ▸ Image Mapper** kholein. Matching ke 4 tareeqe (jo asaan lage):
   - **Auto-match by name** – naam mein "zara face", "guddi sunset", "shop bg" jaise words hon to khud jod deta hai.
   - **Match by # number** – agar file ka naam sirf Excel ka # number hai (`45.png`, `image (45).jpg`).
   - **Import CSV** – Excel ke aakhri column "Your file name" mein apni file ka naam likhein, sheet ko CSV save karein, import karein.
   - **Hath se** – image ko us slot ke chhote box mein drag karein.
4. Peeli rows ("Waiting to install") check karein → **INSTALL** dabayein.
   Images ka naam slot ke naam par ho jata hai, `Assets/Resources/KitefallArt` mein chali jati hain, mobile compression lag jati hai, aur app icon set ho jata hai.
5. Play dabayein. Jo image nahi lagi, us jagah game apni banayi hui drawing dikhata hai – kuch toot ta nahi.

Game abhi yeh images use karta hai: kite skins (3D kite par), shop/wardrobe cards, characters ki tasveer (squad lobby), menu backgrounds, splash + logo, HUD / quick-chat / roof icons.
HUD, quick-chat aur roof-trait icons **safed (white) shape, transparent background** par hon – game unhe khud rang deta hai.
Adaptive icon (foreground/background) Player Settings ▸ Icon mein hath se lagayein. Excel badalni ho: `python3 Tools/ArtList/make_assets.py docs/Kitefall_Image_Assets.xlsx Assets/Scripts/UI/ArtSlots.Generated.cs`.
