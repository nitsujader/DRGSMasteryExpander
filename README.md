# Weapon Mastery Expander

A [BepInEx](https://github.com/BepInEx/BepInEx) mod for **Deep Rock Galactic: Survivors** that expands weapon mastery from 3 stages to 10 (configurable), matching biome mastery progression.

## Features

- Expands weapon mastery stage count from 3 to a configurable number (default 10, max 20)
- Works for all 42 weapons across all 5 biomes
- Clones existing stage configurations so new stages feel consistent
- Toggle on/off via config without uninstalling

## Installation

1. Install [BepInEx 6 IL2CPP](https://docs.bepinex.dev/articles/user_guide/installation/index.html) for Deep Rock Galactic: Survivors
2. Copy `WeaponMasteryExpander.dll` to `BepInEx/plugins/`
3. Launch the game

## Configuration

After first launch, edit `BepInEx/config/com.drgs.weaponmasteryexpander.cfg`:

```ini
[General]
# Enable/disable the mod
Enable Mod = true

# Number of stages for weapon mastery (vanilla is 3)
# Acceptable range: 3 - 20
Target Stages = 10
```

## Building from Source

Requires .NET 6.0 SDK.

1. If your game is not installed at the default path in `Directory.Build.props`, create a `Directory.Build.props.user` file in the project root:
   ```xml
   <Project>
     <PropertyGroup>
       <GameDir>C:\YourPath\Deep Rock Survivor</GameDir>
     </PropertyGroup>
   </Project>
   ```
2. Build:
   ```
   dotnet build src/WeaponMasteryExpander.csproj
   ```
3. Output DLL is in `build/`

## How It Works

The mod uses Harmony patches on the game's runtime mission pipeline to expand weapon mastery stages:

- **`ChallengeDataWeaponSlots.Apply`** — Expands `MissionMapConfig.LevelConfigs` after the challenge configures the run
- **`RunSettingsManager.GetMissionMapConfig`** — Ensures the active config has the target stage count
- **`RunStateManager.get_StageLength`** — Overrides the stage count read by the UI
- **`RunStateManager.IsLastLevel`** — Prevents the run from ending before the target stage

New stages clone the final vanilla stage's `LevelConfig` and `BossMissionPool`, so difficulty and objectives remain consistent with late-game mastery.
