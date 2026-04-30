# Phase Test Spawn Overview

## Primary test table
- `SpawnTable_PhaseTest_AllItems`: fixed table containing weapons, melee, throwables, deployables, consumables, and equipment for full-system loot testing.
- `WorldSpawnConfig_PhaseTest_AllItems`: item scatter config using that table, `MaxActive=64`, `ScatterRadius=8`, `RespawnTime=30`.

## Map01 phase-test loot spawns
- `PhaseLoot_AllItems_Main` at `(-13.50, 0.50, 4.20)` -> `WorldSpawnConfig_PhaseTest_AllItems`
- `PhaseLoot_AllItems_North` at `(-89.50, 0.50, 19.00)` -> `WorldSpawnConfig_PhaseTest_AllItems`
- `PhaseLoot_AllItems_South` at `(-40.90, 0.50, -178.10)` -> `WorldSpawnConfig_PhaseTest_AllItems`
- `PhaseLoot_AllItems_West` at `(-192.90, 0.50, -58.10)` -> `WorldSpawnConfig_PhaseTest_AllItems`

## Map01 phase-test player spawns
- `PhasePlayer_Team0_A` team `0` at `(-24.00, 0.50, 12.00)`
- `PhasePlayer_Team0_B` team `0` at `(-36.00, 0.50, 8.00)`
- `PhasePlayer_Team1_A` team `1` at `(-168.00, 0.50, -46.00)`
- `PhasePlayer_Team1_B` team `1` at `(-180.00, 0.50, -54.00)`
- `PhasePlayer_Neutral_A` team `-1` at `(-96.00, 0.50, -82.00)`

## Existing spawn table groups
- `Items/Ground_*`: single item or small ground scatter presets.
- `Items/Crate_*`, `Items/Chest_*`: container/chest loot presets.
- `Items/Cluster_*`: focused mixed clusters for weapon/consumable/attachment tests.
- `Boss/*`, `Zone_*`, `SupplyDrop_*`: reward/drop tables for boss, zone, and event flows.
