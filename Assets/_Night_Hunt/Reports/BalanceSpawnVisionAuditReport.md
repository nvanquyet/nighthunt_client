# NightHunt Balance / Spawn / Vision Audit

Generated: 2026-04-27 13:33:21

## Item Database
- ItemDefinitions found: 34

## Player Stats
- `VisionRange` baseline set to 18m, max 80m.

## Weight Penalty
- Rebuilt tiers: Heavy 80% (-8% speed), Overweight 100% (-22% speed), Overloaded 130% (-45% speed).

## Spawn Tables
- Filled `SpawnTable_ItemScatter`: 0 fixed, 13 random.
- Kept `SpawnTable_Ground_Pistol`: already configured (1 fixed, 0 random).
- Kept `SpawnTable_Ground_AR_Common`: already configured (0 fixed, 1 random).
- Kept `SpawnTable_Ground_AR_Mixed`: already configured (0 fixed, 1 random).
- Kept `SpawnTable_Ground_AR_Rare`: already configured (1 fixed, 2 random).
- Kept `SpawnTable_Ground_Medkit_Common`: already configured (0 fixed, 2 random).
- Kept `SpawnTable_Ground_Medkit_Rare`: already configured (1 fixed, 1 random).
- Kept `SpawnTable_Ground_EnergyDrink_Common`: already configured (0 fixed, 2 random).
- Kept `SpawnTable_Ground_Beacon`: already configured (1 fixed, 0 random).
- Kept `SpawnTable_Ground_Throwable_Mixed`: already configured (0 fixed, 1 random).
- Kept `SpawnTable_Ground_FragGrenade`: already configured (0 fixed, 1 random).
- Kept `SpawnTable_Ground_SmokeGrenade`: already configured (0 fixed, 1 random).
- Kept `SpawnTable_Ground_Attachment_Any`: already configured (0 fixed, 6 random).
- Kept `SpawnTable_Ground_Attachment_Optic`: already configured (0 fixed, 2 random).
- Kept `SpawnTable_Ground_Attachment_Support`: already configured (0 fixed, 3 random).
- Kept `SpawnTable_Ground_Helmet`: already configured (0 fixed, 1 random).
- Kept `SpawnTable_Ground_Vest`: already configured (0 fixed, 1 random).
- Kept `SpawnTable_Ground_Gloves`: already configured (0 fixed, 1 random).
- Kept `SpawnTable_Ground_Belt`: already configured (0 fixed, 1 random).
- Kept `SpawnTable_Ground_Backpack`: already configured (0 fixed, 1 random).
- Kept `SpawnTable_Crate_Medical`: already configured (0 fixed, 2 random).
- Kept `SpawnTable_Crate_Weapons_Light`: already configured (0 fixed, 3 random).
- Kept `SpawnTable_Crate_Weapons_Heavy`: already configured (0 fixed, 3 random).
- Kept `SpawnTable_Crate_Equipment_Basic`: already configured (2 fixed, 2 random).
- Kept `SpawnTable_Crate_Equipment_Full`: already configured (3 fixed, 2 random).
- Kept `SpawnTable_Crate_Utility`: already configured (2 fixed, 1 random).
- Kept `SpawnTable_Crate_General_Common`: already configured (0 fixed, 3 random).
- Kept `SpawnTable_Crate_General_Rare`: already configured (0 fixed, 4 random).
- Kept `SpawnTable_Chest_Basic`: already configured (0 fixed, 3 random).
- Kept `SpawnTable_Chest_Military`: already configured (0 fixed, 4 random).
- Kept `SpawnTable_Chest_Locked_Basic`: already configured (1 fixed, 3 random).
- Kept `SpawnTable_Chest_Locked_Elite`: already configured (3 fixed, 5 random).
- Kept `SpawnTable_LockedChest`: already configured (1 fixed, 5 random).
- Kept `SpawnTable_Container`: already configured (0 fixed, 5 random).
- Kept `SpawnTable_Cluster_Weapon_Scattered`: already configured (0 fixed, 4 random).
- Kept `SpawnTable_Cluster_Consumable_Mixed`: already configured (0 fixed, 4 random).
- Kept `SpawnTable_Cluster_Attachment_Mixed`: already configured (0 fixed, 6 random).
- Kept `SpawnTable_BossDrop_Tier1_CommonLoot`: already configured (0 fixed, 2 random).
- Kept `SpawnTable_BossDrop_Tier1_Medical`: already configured (1 fixed, 2 random).
- Kept `SpawnTable_BossDrop_Tier2_Rifle`: already configured (0 fixed, 3 random).
- Kept `SpawnTable_BossDrop_Tier2_Armor`: already configured (2 fixed, 3 random).
- Kept `SpawnTable_BossDrop_Tier2_FullKit`: already configured (2 fixed, 2 random).
- Kept `SpawnTable_BossDrop_Tier3_Elite`: already configured (3 fixed, 6 random).
- Kept `SpawnTable_BossDrop_Tier3_AllWeapons`: already configured (0 fixed, 6 random).
- Kept `SpawnTable_Zone_Phase1_Reward`: already configured (0 fixed, 2 random).
- Kept `SpawnTable_Zone_Phase2_Reward`: already configured (1 fixed, 2 random).
- Kept `SpawnTable_Zone_Phase3_Reward`: already configured (2 fixed, 6 random).
- Kept `SpawnTable_Zone_Capture_Standard`: already configured (2 fixed, 0 random).
- Kept `SpawnTable_Zone_Capture_Elite`: already configured (4 fixed, 2 random).
- Kept `SpawnTable_Zone_Clear_Beacon`: already configured (1 fixed, 3 random).
- Kept `SpawnTable_Zone_BeaconGuardian_Drop`: already configured (0 fixed, 2 random).
- Kept `SpawnTable_SupplyDrop_Common`: already configured (0 fixed, 2 random).
- Kept `SpawnTable_SupplyDrop_Rare`: already configured (0 fixed, 6 random).
- Kept `SpawnTable_Cache_Hidden_A`: already configured (4 fixed, 0 random).
- Kept `SpawnTable_Cache_Hidden_B`: already configured (0 fixed, 4 random).
- Kept `SpawnTable_Event_Special_Crate`: already configured (0 fixed, 6 random).
- Kept `SpawnTable_FinalZone_Jackpot`: already configured (9 fixed, 0 random).
- Filled `SpawnTable_PhaseTest_AllItems`: 1 fixed, 10 random.

## World Spawn Configs
- Wired `WorldSpawnConfig_ItemScatter` -> `SpawnTable_ItemScatter` (Item, maxActive=3).
- Wired `WorldSpawnConfig_Crate_General_Common` -> `SpawnTable_Crate_General_Common` (Container, maxActive=1).
- Wired `WorldSpawnConfig_Chest_Locked_Elite` -> `SpawnTable_Chest_Locked_Elite` (Container, maxActive=1).
- Wired `WorldSpawnConfig_Zone_Phase1_Reward` -> `SpawnTable_Zone_Phase1_Reward` (Item, maxActive=4).
- Wired `WorldSpawnConfig_Zone_Phase2_Reward` -> `SpawnTable_Zone_Phase2_Reward` (Item, maxActive=5).
- Wired `WorldSpawnConfig_Zone_Phase3_Reward` -> `SpawnTable_Zone_Phase3_Reward` (Item, maxActive=6).
- Wired `WorldSpawnConfig_BossDrop_Tier3_Elite` -> `SpawnTable_BossDrop_Tier3_Elite` (Item, maxActive=6).

## Audit
- EMPTY: `Assets/_Night_Hunt/Data/Resources/Database/Spawn/SpawnTables/Items/SpawnTable_Template.asset`.
- Empty tables: 1
- Null item refs: 0
