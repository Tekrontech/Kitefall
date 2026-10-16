# Online setup (Unity Gaming Services, ads, store)

The offline game works without any of this. Each online part switches on **automatically** when its package is installed (see `Assets/Scripts/Online/Kitefall.Online.asmdef` → *versionDefines*). Check what is active with **Kitefall ▸ Online ▸ Show Installed Online Packages**.

> **Keys and secrets:** never commit real keys, keystores or service-account secrets (spec 17.8). The `.gitignore` already excludes keystores and Google config files.

## 1. Link a Unity Cloud project
1. Edit ▸ Project Settings ▸ **Services** ▸ link (or create) a project in your organisation.
2. Open the Unity Dashboard (cloud.unity.com) for that project. Create environments `development` and `production`.

## 2. Install packages (Window ▸ Package Manager ▸ + ▸ Add package by name)
| Package | Enables |
|---|---|
| `com.unity.services.authentication` | Guest / Google / Apple sign-in |
| `com.unity.services.cloudcode` | Server economy (required together with authentication) |
| `com.unity.services.leaderboards` | Global / country / friends leaderboards |
| `com.unity.services.multiplayer` + `com.unity.netcode.gameobjects` | Online matches (Relay sessions, matchmaking, private rooms) |
| `com.unity.remote-config` | Balance overrides without an update |
| `com.unity.services.analytics` | Analytics (with consent) |
| `com.unity.services.deployment` | Deploy Cloud Code / config files from the editor |
| `com.unity.services.levelplay` (Ads Mediation) | Real rewarded + interstitial ads |
| `com.unity.purchasing` (v5) | Real in-app purchases |

## 3. Online config asset
**Kitefall ▸ Online ▸ Create or Select Online Config** → `Assets/Resources/KitefallOnlineConfig.asset`
- *Environment*: `development` while testing.
- *Cloud Code Module*: `KitefallEconomy` (matches `Assets/CloudCode/KitefallEconomy.ccmr`).
- *Ranked Leaderboard Id*: `ranked` · *Ranked Queue*: `ranked-duel`.
- LevelPlay keys / ad unit ids (step 8).
- Untick *Enable Online* to force offline mode.

## 4. Deploy the server economy (Cloud Code)
The module `Server/KitefallEconomy` compiles the **same** `Assets/Scripts/Core` rules as the game.
1. Install the **.NET 9 SDK** on your computer (Cloud Code runs .NET 9).
2. **Services ▸ Deployment** ▸ select the environment ▸ tick `KitefallEconomy.ccmr`, `Kitefall.ac`, `kitefall.rc` ▸ **Deploy**.
3. Dashboard ▸ Cloud Code ▸ Modules: `KitefallEconomy` with functions `GetProfile`, `Run`, `ReportMatch`.

How it stays server-authoritative:
- Profiles are **Cloud Save protected** data – players can read, only the module writes.
- `Kitefall.ac` denies player writes to Leaderboards and Economy.
- `ReportMatch` validates every result (`MatchResultDto.Validate`) and rewards each match id once per player.
- **Ranked results are accepted only from a dedicated server** (service account). For tests without a server set `AllowPlayerHostedRanked = true` in `KitefallEconomy.cs` and redeploy.

Local build check without .NET 9: `dotnet build Server/KitefallEconomy/KitefallEconomy/KitefallEconomy.csproj -p:KitefallTfm=net8.0`

## 5. Authentication
- **Guest** (anonymous) works as soon as the package is installed.
- **Google Play Games:** install the *Google Play Games plugin for Unity*, enable Google Play Games in Dashboard ▸ Authentication (client id + secret), then give Kitefall the auth code at start-up:
  ```csharp
  // e.g. in a small script of your own (Assets/Scripts/Online is fine)
  Kitefall.Online.SocialSignIn.GooglePlayGamesAuthCode = () =>
  {
      var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
      GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(true, code => tcs.SetResult(code));
      return tcs.Task;
  };
  ```
- **Apple:** add a *Sign in with Apple* plugin and set `SocialSignIn.AppleIdToken` the same way.
- Guests can link later: Profile ▸ Link Google / Link Apple.

## 6. Leaderboards
Dashboard ▸ Leaderboards ▸ create **`ranked`** (sort: descending, update: keep latest score). The module writes scores with `country` and `name` metadata after each Ranked result; the Country tab filters on that.

## 7. Online matches (Multiplayer Services + Netcode)
- **Quick Duel / FFA / Team / Last Kite Flying:** players quick-join a session with the same mode and region, or create one. The host starts when the room is full, or after 20 s fills empty seats with labelled bots (never in Ranked).
- **Private room:** Home ▸ Private Room ▸ *Create online room* shows a join code; friends use *Join with code*; *Start* fills empty seats with bots.
- **Ranked Duel:** Dashboard ▸ Matchmaker ▸ create queue **`ranked-duel`**, 2 teams of 1 player, region attribute, rule *rank within 1 tier*, relaxed to 2 tiers after 10 s, no bots.
- The **host runs the authoritative simulation** (Core `MatchSimulation`), clients send inputs at 20 Hz and draw snapshots (`NetCodec`, < 1.2 KB for 8 players). Disconnect rules from spec 14.2 apply (AI hold, 30 s round forfeit, 60 s match forfeit, reconnect with the same account).
- After the match the host (or the dedicated server) calls `ReportMatch`; everyone's rewards are applied by the server.

### Dedicated server (Ranked)
1. File ▸ Build Profiles ▸ **Linux Dedicated Server** ▸ build with `Assets/Scenes/Main.unity`.
2. Run: `./Kitefall.x86_64 -port 7777 -mode RankedDuel -players 2 -wait 60 -arena OldCityRooftops`
3. Environment variables: `KITEFALL_PROJECT_ID`, `KITEFALL_ENVIRONMENT_ID`, `KITEFALL_SA_KEY_ID`, `KITEFALL_SA_SECRET` (Dashboard ▸ Administration ▸ Service Accounts, role allowing Cloud Code calls).
4. Choose a game-server host and connect the `ranked-duel` queue to it (check Unity's current hosting options or a third-party host). The server binary listens on the given port, runs the match, reports via the service account and exits.

## 8. Ads (LevelPlay)
1. Create the app in the LevelPlay dashboard (Android + iOS) → app key, rewarded and interstitial **ad unit ids**.
2. Enter them in the Online Config (step 3). Integrate networks with the Ads Mediation integration manager; build once so Android dependencies resolve.
3. All pacing rules stay in `Core/Meta/AdsPolicy.cs` (caps in BalanceConfig ▸ Ads, overridable per country with Remote Config game overrides).
4. Consent: the terms screen asks for personalised ads; under-13 players get COPPA + non-personalised ads. Add an EU consent form (e.g. Google UMP) before launch in the EEA/UK, and the iOS ATT prompt for iOS.

## 9. In-app purchases (Unity IAP v5)
1. Google Play Console ▸ create products: `kitefall.gems.small`, `kitefall.gems.medium`, `kitefall.gems.large` (consumable), `kitefall.noads`, `kitefall.pass.premium` (non-consumable). Same ids in App Store Connect for iOS.
2. Purchases go: store → receipt → Cloud Code `grant_purchase` → order confirmed only after the server granted it.
3. **Before production:** implement real receipt validation in `KitefallEconomy.ReceiptValid` (Google Play Developer API / App Store Server API with credentials in Cloud Code Secret Manager). Until then the module refuses all purchases in the `production` environment.

## 10. Remote Config
`Assets/RemoteConfig/kitefall.rc` has one JSON key **`balance`**. Anything you put there overwrites `BalanceConfig` at start-up, e.g.
```json
{ "economy": { "coinsWin": 200 }, "ads": { "interstitialDailyCap": 6 }, "contest": { "noise": 0.4 } }
```
(The module uses the code defaults; load the same JSON there if you want the server economy to follow it.)

## Troubleshooting
| Problem | What to do |
|---|---|
| Compile error inside `Assets/Scripts/Online` right after adding a package | The package version may name an API differently. Tell me the exact error, or remove that package (the online part switches off). |
| "Server unavailable" toasts | Module not deployed to the selected environment, or the environment name in the Online Config is wrong. |
| Stuck on "Connecting to host" | Relay must be allowed for the project (Multiplayer Services enables it); both players need the same game version. |
| Purchases fail in production | Expected until receipt validation is implemented (step 9.3). |

## What is verified and what is not
- `Server/KitefallEconomy` compiles against the real Cloud Code NuGet packages.
- `Assets/Scripts/Online` was written from the package documentation and compile-checked against hand-written API stubs, **not** against the real packages and not run against live services. Expect small fixes when you first install the packages.
