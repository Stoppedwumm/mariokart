# Kart Racer: a Mario Kart-style game in Unity

A small, self-contained kart racer inspired by Mario Kart. Everything is generated from C# at
runtime: the track, karts, drivers, item boxes, items, HUD and engine sound. There are no imported
models, textures or prefabs, so the project is just a handful of scripts.

## Features

- **Mushroom Circuit**: a procedurally built closed circuit (Catmull-Rom spline) with kerbs,
  red/white barriers, off-road grass that slows you down, a start gantry, trees, pipes and clouds.
- **Arcade kart physics**: grippy steering, a hop plus power-slide drift, and three **mini-turbo**
  tiers (blue, orange, purple sparks) that give a boost when you release the drift.
- **8 racers**: you plus 7 CPU drivers. The CPUs follow a racing line, drift through corners,
  dodge bananas, use items tactically, recover when stuck, and rubber-band around you.
- **Items** from rainbow `?` boxes, weighted by race position:
  - Mushroom and Triple Mushroom: speed boost
  - Banana: dropped behind you, and anyone who hits it spins out
  - Green Shell: fires straight ahead and bounces off walls
  - Red Shell: follows the track and homes in on the racer one place ahead
  - Star: invincibility and extra speed, and karts you touch spin out
- **Race flow**: title screen with 50cc / 100cc / 150cc, a 3-2-1 countdown with a **rocket start**,
  3 laps, live standings, a minimap, a wrong-way warning, lap and final-lap callouts, and a
  results screen.
- A chase camera with speed-dependent FOV and look-back.

## Getting started

1. Install **Unity 2022.3 LTS** (any 2021.3+ version should work) with the Built-in Render Pipeline.
2. In Unity Hub, click **Add → Add project from disk** and select this folder.
3. Open the project and press **Play**.

   You don't need a scene. `GameBootstrap` builds the race automatically in whatever scene is open,
   including the default untitled one.
4. Optional: choose **Kart Racer → Create Race Scene** from the menu. It saves
   `Assets/Scenes/Race.unity` and adds it to Build Settings so you can make a standalone build.

> The materials use the Built-in **Standard** shader (`Assets/Resources/KartBase.mat` keeps it
> included in builds). If you open the project with URP, the scripts fall back to
> `Universal Render Pipeline/Lit`.
>
> Input uses the legacy **Input Manager**, which is the default for new 2022.3 projects. If your
> project is set to "Input System Package (New)" only, change
> *Project Settings → Player → Active Input Handling* to **Both** or **Input Manager (Old)**.

## Controls

| Action              | Keyboard                 | Gamepad (Xbox layout) |
|---------------------|--------------------------|-----------------------|
| Accelerate          | W / ↑                    | A                     |
| Brake / reverse     | S / ↓                    | B                     |
| Steer               | A / D, ← / →             | Left stick            |
| Hop / drift         | Space or Left Shift      | RB                    |
| Use item            | E or Ctrl                | LB / X                |
| Look back           | Q or B                   | Y                     |
| Respawn on track    | T                        |                       |
| Pause               | P                        |                       |
| Restart race        | R                        |                       |
| Back to title       | Esc                      |                       |

**Drifting:** hold Space while steering at speed. Keep holding to charge the sparks
(blue, then orange, then purple). Steering into the drift tightens it and charges faster.
Release to boost.

**Rocket start:** press and hold accelerate while **1** is showing on the countdown.

## Code overview (`Assets/Scripts`)

| Script | Purpose |
|---|---|
| `GameBootstrap.cs` | Auto-starts the game when Play mode begins. |
| `RaceManager.cs` | Builds the world, spawns karts and item boxes, and runs the title → countdown → race → results flow. It also computes standings. |
| `Track.cs` | Generates the spline, road mesh, barriers, start line and scenery. Provides nearest-point queries used for progress tracking and AI. |
| `KartController.cs` | Arcade kart physics, drifting and mini-turbos, boosts, spin-outs, items and respawning. |
| `KartVisual.cs` | Builds the kart and driver from primitives and animates wheels, hops, drift lean, sparks, flames and the star effect. |
| `PlayerDriver.cs` / `AIDriver.cs` | Input sources that feed the kart controller. |
| `RaceProgress.cs` | Lap counting, race distance, place and wrong-way detection. |
| `ItemBox.cs`, `ItemType.cs`, `Shell.cs`, `Banana.cs` | Item boxes, the position-weighted item roll, and the item behaviours. |
| `KartCamera.cs` | Chase camera and title-screen orbit. |
| `RaceHUD.cs` | The OnGUI HUD. |
| `EngineAudio.cs` | Procedural engine sound for the player's kart. |
| `Factory.cs` | Runtime materials, procedural textures and primitive helpers. |
| `Editor/KartRacerMenu.cs` | The **Kart Racer → Create Race Scene** menu item. |

### Tweaking

- **Track shape:** edit the `Layout` control points in `Track.cs`. Keep corner radii above
  about 16 m so the barriers don't overlap.
- **Speeds:** edit `RaceManager.Classes`, and `turnSpeed` / `acceleration` in `KartController`.
- **Laps:** `RaceManager.TotalLaps`.
- **Item odds:** `Items.Roll` in `ItemType.cs`.
