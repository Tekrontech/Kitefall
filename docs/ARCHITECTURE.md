# Architecture (milestone 1)

```text
           ┌──────────────── Unity (Kitefall.Runtime) ────────────────┐
 Touch /   │ KiteInputReader ──► KiteController ──► Transform (kite)  │
 keys /    │                        │   ▲                               │
 gamepad   │ WindField ─────────────┘   │ events: ZoneChanged,          │
           │                            │ SelfSnapped, Crashed …        │
           │ HUD: TensionBarView, WindCompassView, FlightDebugHud      │
           └────────────────────────────┼──────────────────────────────┘
                                        │ plain data only
           ┌────────── Pure C# (Kitefall.Core, no UnityEngine) ────────┐
           │ KiteSimulation                                            │
           │   ├─ LineActionResolver  (buttons → Idle/Pull/Dheel/Reel) │
           │   ├─ TensionModel        (spec 9.1 / 9.2, self-snap)      │
           │   ├─ LineSpool           (line out, capacity, stock)      │
           │   └─ KiteFlightSim       (wind-window flight)             │
           │ WindModel (deterministic, seeded)                         │
           │ LoadoutStats (kite + dum + dor + charkhi → final numbers) │
           └───────────────────────────────────────────────────────────┘
```

## Assemblies
| Assembly | Folder | References | Why |
|---|---|---|---|
| `Kitefall.Core` | `Assets/Scripts/Core` | none (`noEngineReferences`) | Game rules. Same code will run on the server (milestone 10). |
| `Kitefall.Runtime` | `Assets/Scripts` | Core, Input System, uGUI | MonoBehaviours, ScriptableObjects, HUD. |
| `Kitefall.Editor` | `Assets/Scripts/Editor` | Core, Runtime | Menus: scene builder, default data. |
| `Kitefall.Tests.EditMode` | `Assets/Tests/EditMode` | Core, NUnit | Unit tests. Also run by `Tools/CoreTests` with plain .NET. |

## Simulation loop
- `KiteController.FixedUpdate` (50 Hz) → `KiteSimulation.Step(input, wind, time, dt)`.
- Order inside a step: resolve line action → tension → line out → flight → snap/crash check.
- `Update` interpolates the kite between the last two ticks so it is smooth at 30, 60 or 120 FPS.
- Wind is sampled from `Time.fixedTime` and the kite height; with the same seed every machine gets the same wind.

## Data flow
ScriptableObject (`DorData` …) → `ToStats()` → plain struct (`DorStats`) → `LoadoutStats` → Core models.
Core never sees a ScriptableObject, so Remote Config (later) can build the same structs from JSON.

## Coordinates
- World +Y up, ground at y = 0.
- Wind direction = where the wind blows **to**, degrees clockwise from +Z.
- Kite position = spherical (azimuth, elevation, radius) around the line anchor. Heading 0 = up, +90 = right (as seen by the flyer).
