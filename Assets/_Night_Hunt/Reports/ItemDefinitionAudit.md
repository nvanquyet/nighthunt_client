# Báo cáo audit ItemDefinition và Deployable

Ngày cập nhật: 2026-05-13

Phạm vi: `Assets/_Night_Hunt/Data/Resources/Database/Items/List Items`

Tổng số definition đang audit: 36

- Weapon: 8
- Throwable: 3
- Consumable: 9
- Deployable/Beacon: 5
- Equipment: 5
- Attachment: 6

## Cleanup đã thực hiện

Đã xóa các asset không còn reference thật trong project:

| File đã xóa | Lý do |
| --- | --- |
| `Assets/_Night_Hunt/Prefabs/Items/Visuals/Visual_GenericDeployable 1.prefab` | Bản duplicate, không được item definition, scene, script hoặc prefab khác tham chiếu bằng GUID. |
| `Assets/_Night_Hunt/Prefabs/Items/Visuals/Visual_GenericDeployable 1.prefab.meta` | Xóa cùng prefab để Unity không giữ GUID rác. |
| `Assets/_Night_Hunt/Prefabs/Items/Mine/Projectile_Mine_Lvl3.prefab` | Mine runtime hiện dùng `NetworkDeployable_TrapMine.prefab`; prefab này không còn được definition/scene/script tham chiếu. |
| `Assets/_Night_Hunt/Prefabs/Items/Mine/Projectile_Mine_Lvl3.prefab.meta` | Xóa cùng prefab để Unity không giữ GUID rác. |

Không xóa các prefab sau vì còn reference trong `Assets/_Night_Hunt/Scenes/Scne Test.unity`:

| File giữ lại | Reference còn tồn tại | Ghi chú |
| --- | --- | --- |
| `Weapon_AR_2.prefab` | Scene test dùng GUID `bb2e6ae88a4b0e844b765e801a0dc093` | Không nối với item definition chính; chỉ nên xóa sau khi bỏ khỏi scene test. |
| `Weapon_Shotgun_2.prefab` | Scene test dùng GUID `eeb0a40bc465a7246838a9ee42d08067` | Không nối với item definition chính; chỉ nên xóa sau khi bỏ khỏi scene test. |
| `Weapon_Melee_1.prefab` | Scene test và `NightHuntWeaponPrefabAuditTool` còn nhắc tới | Item chính đang dùng `Weapon_Melee_2.prefab`. |

## Thay đổi code để ổn định flow

| File | Thay đổi | Tác dụng |
| --- | --- | --- |
| `DeployablePlacementHandler.cs` | Server validate `DeployableKind` trước khi spawn prefab. | Tránh data trap nhưng prefab lại là vision node hoặc ngược lại. |
| `DeployableDefinition.cs` | Ghi rõ contract prefab theo kind, thêm `OverridePrefabHealth`. | Prefab giữ HP mặc định; chỉ override bằng data khi bật cờ. |
| `NightHuntItemDefinitionAuditTool.cs` | Audit bắt lỗi prefab sai component và auto-fix chọn template theo kind. | Thiếu prefab trap sẽ gắn template trap, thiếu vision sẽ gắn template `VisionWard`, không gắn generic bừa. |
| `ItemVisualResolver.cs` | Fallback visual theo item type, dùng `MaterialPropertyBlock` thay vì tạo `Material` mới. | Item thiếu visual không invisible, giảm nguy cơ leak material runtime. |
| `BaseDeployable.cs` | `OnStopNetwork()` gọi `CancelInvoke()`. | Hủy timer còn treo khi deployable despawn/network stop. |
| `TrapDeployable.cs` | `OnStopNetwork()` stop coroutine và remove field modifiers; VFX trigger tự destroy sau 5s. | Tránh slow modifier hoặc coroutine còn giữ reference khi object bị despawn ngoài flow destroy. |
| `VisionWard.cs` | Chỉ bật FOW revealer khi local player đã resolve và cùng team. | Tránh client chưa có local player tạm thời thấy vision node của team khác. |

## Luồng deploy hiện tại

1. Client owner gọi `BeginDeploy(item, def)`.
2. Client tạo preview từ `PlacementPreviewPrefab`; nếu thiếu thì dùng `VisualPrefab`; nếu vẫn thiếu thì tạo runtime preview.
3. Client confirm vị trí, gửi `CmdRequestPlaceDeployable(position, rotation, definitionId, itemInstanceId)`.
4. Server validate:
   - player và `definitionId` tồn tại;
   - item đang cầm khớp `definitionId`;
   - vị trí trong tầm `VisionRange` hoặc `PlacementDistance`;
   - surface hợp lệ, slope không quá `MaxPlacementSlope`;
   - không overlap blocker.
5. Server spawn:
   - `BeaconDefinition` đi qua `BeaconManager` và `NetworkBeaconPrefab`;
   - `DeployableDefinition` instantiate `NetworkDeployablePrefab`;
   - prefab phải có `NetworkObject` và `BaseDeployable`;
   - `VisionNode/LightPoint` bắt buộc `VisionWard`;
   - `ExplosiveMine/ShockField` bắt buộc `TrapDeployable`;
   - `Generic` dùng `SimpleDeployable` hoặc class kế thừa `BaseDeployable`.
6. Server `Spawn(go, Owner)`, gọi `StartPlacement()`, rồi mới consume item nếu placement thành công.

Đánh giá: flow placement hiện hợp lý và server-authoritative cho spawn/consume. Client chỉ đề xuất vị trí; server vẫn re-check khoảng cách và blocker.

## Deployable và Beacon

| ItemID | Tên | Runtime prefab | Component runtime | Visual/preview | Thông số chính | Trạng thái |
| --- | --- | --- | --- | --- | --- | --- |
| `DEPLOY_BEACON` | Respawn Beacon | `NetworkBeacon_RespawnBeacon.prefab` | `RespawnBeacon`, `BaseDeployable`, `NetworkObject` | `Visual_RespawnBeacon.prefab` / `Visual_GenericDeployable.prefab` | Weight 2.5, deploy 1.25s, prefab HP 100 | OK. Tên ID còn cũ nhưng đang functional. |
| `TRAP_MINE` | Explosive Mine | `NetworkDeployable_TrapMine.prefab` | `TrapDeployable`, `NetworkObject` | `Visual_GenericDeployable.prefab` / same | Weight 1, HP prefab 100, trigger radius 2, explosion radius 4, damage 120, arm 0.75s | OK runtime; visual còn generic. |
| `TRAP_SHOCK` | Shock Trap | `NetworkDeployable_ShockField.prefab` | `TrapDeployable`, `NetworkObject` | `Visual_GenericDeployable.prefab` / same | Weight 1, HP prefab 100, field radius 4, duration 8s, tick 0.25s, slow -40%, stamina -8/s | OK runtime; visual còn generic. |
| `VISION_LIGHTPOINT` | Light Point | `NetworkDeployable_LightPoint.prefab` | `VisionWard`, `NetworkObject` | `Visual_GenericDeployable.prefab` / same | Weight 1, HP prefab 100, vision radius 18, lifetime 90s | OK runtime; visual còn generic. |
| `VISION_NODE` | Vision Node | `NetworkDeployable_VisionNode.prefab` | `VisionWard`, `NetworkObject` | `Visual_GenericDeployable.prefab` / same | Weight 1, HP prefab 100, vision radius 15, lifetime 120s | OK runtime; visual còn generic. |

## Throwable

| ItemID | Tên | Projectile prefab | Visual | Tác dụng/thông số | Ghi chú |
| --- | --- | --- | --- | --- | --- |
| `throwable_fraggrenade` | Frag Grenade | `NetworkProjectile_FragGrenade.prefab` | `Visual_FragGrenade.prefab` | Damage 100, radius 5, fuse 2s, weight 0.4 | Functional; ID casing lệch với các item còn lại. |
| `UTIL_SMOKE` | Smoke Grenade | `NetworkProjectile_SmokeGrenade.prefab` | `Visual_SmokeGrenade.prefab` | Damage 0, radius 6, fuse 2s, use 0.5s, weight 0.6 | OK. |
| `UTIL_EMP` | EMP Grenade | `NetworkProjectile_SmokeGrenade.prefab` | `Visual_SmokeGrenade.prefab` | Damage 0, radius 12, fuse 2s, use 0.5s, weight 0.9 | Functional placeholder; nên có projectile/visual EMP riêng. |

## Consumable

| ItemID | Tên | Visual | Tác dụng/thông số | Trạng thái |
| --- | --- | --- | --- | --- |
| `HEAL_BANDAGE` | Bandage | `Visual_Bandage.prefab` | Restore 25 HP, use 2s, weight 0.2 | OK. |
| `HEAL_MEDKIT` | Medkit | `Visual_Medkit.prefab` | Restore 70 HP, use 4s, weight 0.8 | OK. |
| `STAM_DRINK` | Energy Drink | `Visual_StaminaDrink.prefab` | +60 stamina over 10s, use 1.5s, weight 0.3 | OK. |
| `BUFF_SPEED` | Speed Boost | `Visual_BuffItem.prefab` | +15% movement speed trong 8s, use 1s, weight 0.4 | OK. |
| `BUFF_SILENT` | Silent Step | `Visual_BuffItem.prefab` | Noise radius x0.5 trong 10s, use 1s, weight 0.4 | OK. |
| `CLEANSE_ANTIDOTE` | Antidote | `Visual_Antidote.prefab` | Cleanse debuffs, use 1s, weight 0.3 | OK. |
| `KEY_CORE` | Energy Core Key | `Visual_KeyCore.prefab` | Unlock objective, use 0s, weight 0.1 | OK nếu item key dùng chung visual core. |
| `RADAR_SCANNER` | Radar Scanner | `Visual_KeyCore.prefab` | Reveal enemy positions 5s, use 0.75s, weight 0.3 | Functional; visual đang placeholder. |
| `VISION_EYE` | Eye Vision | `Visual_KeyCore.prefab` | +5 vision range trong 10s, use 0.5s, weight 0.2 | Functional; visual đang placeholder. |

## Weapon

| ItemID | Tên | Visual prefab | Thông số chính | Ghi chú |
| --- | --- | --- | --- | --- |
| `WEAPON_AR` | AR | `Weapon_AR_1.prefab` | Damage 25, fire rate 480, mag 30, ammo 120, reload 2.4 | OK. `Weapon_AR_2` chỉ còn trong scene test. |
| `WEAPON_MACHINE_GUN` | Machine Gun | `Weapon_MachineGun_1.prefab` | Damage 12, fire rate 900, mag 100, ammo 500, reload 4.2 | OK. |
| `WEAPON_MELEE` | Melee | `Weapon_Melee_2.prefab` | Damage 45, fire rate 90, mag 1, ammo 0 | OK. `Weapon_Melee_1` còn reference test/audit. |
| `WEAPON_PISTOL` | Pistol | `Weapon_Pistol_1.prefab` | Damage 18, fire rate 240, mag 15, ammo 45, reload 1.8 | OK. |
| `WEAPON_ROCKET_LAUNCHER` | Rocket Launcher | `Weapon_Rocket_1.prefab` | Damage 140, fire rate 30, mag 1, ammo 1, reload 4.5 | OK. |
| `WEAPON_SHOTGUN` | Shotgun | `Weapon_Shotgun_1.prefab` | Damage 7, fire rate 96, mag 8, ammo 56, reload 2.6 | OK. `Weapon_Shotgun_2` chỉ còn trong scene test. |
| `WEAPON_SMG` | SMG | `Weapon_SMG_1.prefab` | Damage 11, fire rate 900, mag 40, ammo 280, reload 1.9 | OK. |
| `WEAPON_SNIPER` | Sniper | `Weapon_Sniper_1.prefab` | Damage 80, fire rate 48, mag 5, ammo 15, reload 3.0 | OK. |

## Equipment

Equipment hiện chưa có `VisualPrefab`. Runtime fallback sẽ render primitive equipment để không invisible, nhưng data vẫn nên gắn visual thật khi có art.

| ItemID | Tên | Slot | Socket | Stats | Visual |
| --- | --- | --- | --- | --- | --- |
| `armor_backpack` | Tactical Backpack | Back | Light | Armor 10, durability 100, +20 WeightCapacity, -5% MovementSpeed | Missing data; runtime fallback. |
| `armor_belt` | Tactical Belt | Belt | None | +5 WeightCapacity, +10 Armor | Missing data; runtime fallback. |
| `armor_gloves` | Tactical Gloves | Hands | None | +2% MovementSpeed, +5 Armor | Missing data; runtime fallback. |
| `armor_helmet` | Combat Helmet | Head | Light | Armor 40, durability 100 | Missing data; runtime fallback. |
| `armor_vest` | Tactical Vest | Chest | Light, Pouch, Pouch, Plate | Armor 80, durability 150, -10% MovementSpeed | Missing data; runtime fallback. |

## Attachment

Attachment hiện chưa có `VisualPrefab`. Runtime fallback sẽ render primitive attachment để không invisible.

| ItemID | Tên | Slot phù hợp | Tác dụng | Visual | Ghi chú |
| --- | --- | --- | --- | --- | --- |
| `attachment_extmag` | Extended Magazine | Magazine | +50% MagazineSize, +10% ReloadSpeed | Missing data; runtime fallback. | Stat target đã sửa đúng. |
| `attachment_flashlight` | Tactical Flashlight | UnderBarrel, Light | BatteryCapacity 3600, +15 VisionRange | Missing data; runtime fallback. | Functional. |
| `attachment_grip` | Vertical Grip | Grip | -25% SpreadPenalty, +5 Accuracy, -15% SpreadBase | Missing data; runtime fallback. | OK nếu spread là recoil-control chính. |
| `attachment_pouch` | Storage Pouch | Pouch | Không modifier | Missing data; runtime fallback. | Placeholder; dư nếu chưa có storage effect. |
| `attachment_reddot` | Red Dot Sight | Optic | +10 Accuracy, +20% RecoilHorizontal, -3 SpreadPenalty | Missing data; runtime fallback. | Description "Faster aim" nhưng stat đang là `RecoilHorizontal`; cần quyết định lại. |
| `attachment_suppressor` | Suppressor | Barrel | -30% SpreadPenalty, -2 Damage | Missing data; runtime fallback. | OK nếu suppressor intended giảm spread và damage. |

## Đánh giá server sync và memory

Ổn sau chỉnh sửa:

- Spawn/consume deployable đang server-authoritative.
- `NetworkObject` chỉ nằm ở runtime prefab; `VisualPrefab` được audit để không chứa `NetworkObject`.
- `BaseDeployable` sync HP, active, placed, team, owner bằng `SyncVar`.
- `VisionWard` chỉ bật revealer cho client cùng team sau khi local player đã resolve.
- `TrapDeployable` dọn coroutine và field modifier khi network stop.
- Runtime fallback visual không tạo material instance mới nữa.

Rủi ro còn lại:

- `BaseDeployable.RequestDamageServerRpc(RequireOwnership = false)` cho phép client gửi damage request trực tiếp tới deployable. Nếu toàn bộ weapon/projectile đã raycast và apply damage ở server thì nên khóa đường này lại hoặc validate nguồn damage chặt hơn.
- `Radar Scanner`, `EMP`, `Vision Eye`, deployable visuals vẫn dùng placeholder. Functional nhưng UX/debug khó phân biệt item.
- `Weapon_AR_2`, `Weapon_Shotgun_2`, `Weapon_Melee_1` là prefab variant không nối item definition, nhưng còn scene test reference nên chưa xóa.
- `ConsumableEffectType` vẫn giữ enum legacy `DeployBeacon`, `PlaceVisionNode`, `PlaceExplosiveTrap`, `PlaceSlowField`. Không nên xóa ngay vì enum serialized theo số; cần migration nếu muốn dọn hẳn.
- `ItemDatabaseJsonRebuildTool.StaleAssets` còn comment/path cũ dưới `Consumables`; tool không chạy runtime nhưng nên review trước khi dùng chức năng rebuild/delete.

## Kết luận

Flow deploy hiện đúng hơn: data kind khớp prefab component, server kiểm tra trước spawn, visual thiếu có fallback, và các leak dễ thấy đã được vá. Phần còn thiếu chủ yếu là art/data polish và một pass security riêng cho đường client request damage.
