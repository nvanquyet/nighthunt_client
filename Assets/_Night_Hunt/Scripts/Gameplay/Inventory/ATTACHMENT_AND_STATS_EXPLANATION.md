# Giải Thích: Attachment System và Stat System

## 1. ATTACHMENT SYSTEM - Gắn Item Vào Equipment

### Khái Niệm:
- **Attachment** không chỉ gắn vào **Weapon**, mà có thể gắn vào **BẤT KỲ Equipment nào**:
  - **Weapon**: Scope, Barrel, Magazine, Grip, Stock
  - **Helmet**: Night Vision, Flashlight, Camera
  - **Armor**: Plate, Pouch1-3, Hydration
  - **Backpack**: Pouch1-2, Hydration

### Cấu Trúc:
```
EquipmentDataBase (base class cho tất cả equipment)
├── attachmentSlots: AttachmentSlotDefinition[]
│   └── Định nghĩa các slot có thể gắn attachment
│
AttachmentData (item có thể gắn vào equipment)
├── compatibleSlots: AttachmentSlotType[]
│   └── Các loại slot mà attachment này có thể gắn vào
└── statModifiers: StatModifier[]
    └── Stats mà attachment này modify
```

### Flow:
1. Player drag attachment item từ inventory
2. Drop vào attachment slot của equipment (weapon/helmet/armor/backpack)
3. UI fire event: `RequestAttachItem(attachmentItemId, equipmentSlotType, attachmentSlotType)`
4. `InventoryEventHandler` nhận event → gọi `EquipmentHandler.AttachToEquipmentServerRpc()`
5. Server validate → remove attachment từ inventory → attach vào equipment
6. `ObserversRpc` sync đến tất cả clients

---

## 2. STAT SYSTEM - Sự Khác Biệt Giữa baseStatModifiers và Specific Stats

### A. `baseStatModifiers` (StatModifier[]) trong EquipmentDataBase:

**Mục đích**: Generic stat modifiers dùng cho **CharacterStats system**

**Cách hoạt động**:
- Modify các stats trong `StatType` enum (Damage, Accuracy, Recoil, FireRate, Range, DamageReduction, MovementSpeed, Weight, ArmorValue, VisionRange, HeadshotProtection, DetectionRange, Durability, RepairCost, Stealth, Visibility)
- Được apply vào `CharacterStats` hoặc `StatModifierStack`
- Có thể dùng cho **bất kỳ equipment nào** (weapon, helmet, armor, backpack)

**Ví dụ**:
```csharp
// Trong ArmorData ScriptableObject
baseStatModifiers = new StatModifier[]
{
    new StatModifier(StatType.DamageReduction, ModifierType.Multiplicative, 0.2f, "Armor"), // Giảm 20% damage
    new StatModifier(StatType.MovementSpeed, ModifierType.Multiplicative, 0.9f, "Armor"), // Giảm 10% movement speed
    new StatModifier(StatType.Weight, ModifierType.Additive, 5f, "Armor") // Tăng 5 weight
}
```

**Khi nào dùng**: Khi muốn equipment modify **character stats** (health, stamina, speed, etc.)

---

### B. Specific Stats trong WeaponData/HelmetData:

**Mục đích**: Hardcoded properties chỉ dùng cho **logic riêng của từng loại item**

**Cách hoạt động**:
- **WeaponData**: `damage`, `fireRate`, `accuracy`, `recoil`, `range`, `reloadSpeed`
  - Chỉ dùng cho weapon shooting logic
  - Không modify CharacterStats
  - Được dùng trực tiếp trong weapon system

- **HelmetData**: `headshotProtection`, `visionRange`, `detectionRange`
  - Chỉ dùng cho helmet logic (vision system, damage calculation)
  - Không modify CharacterStats
  - Được dùng trực tiếp trong helmet/vision system

**Ví dụ**:
```csharp
// Trong WeaponData ScriptableObject
[SerializeField] private float damage = 50f; // Dùng trong weapon shooting
[SerializeField] private float fireRate = 600f; // Dùng trong weapon firing logic
[SerializeField] private float accuracy = 75f; // Dùng trong weapon accuracy calculation

// Trong HelmetData ScriptableObject
[SerializeField] private float headshotProtection = 0.5f; // Dùng trong damage calculation
[SerializeField] private float visionRange = 20f; // Dùng trong vision system
```

**Khi nào dùng**: Khi cần stats **riêng biệt** cho từng loại item, không liên quan đến CharacterStats

---

### So Sánh:

| Aspect | `baseStatModifiers` | Specific Stats (damage, fireRate, etc.) |
|--------|---------------------|-----------------------------------------|
| **Scope** | Generic, dùng cho CharacterStats | Specific, chỉ cho item logic riêng |
| **Apply vào** | CharacterStats/StatModifierStack | Item logic trực tiếp |
| **Flexibility** | Có thể modify bất kỳ stat nào | Hardcoded, không flexible |
| **Use Case** | Armor giảm damage, tăng weight | Weapon damage, helmet vision range |
| **Example** | `StatModifier(StatType.DamageReduction, ...)` | `damage = 50f` |

---

### Kết Luận:

1. **`baseStatModifiers`**: Dùng khi muốn equipment **modify character stats** (health, stamina, speed, damage reduction, etc.)
2. **Specific Stats**: Dùng khi cần stats **riêng cho item logic** (weapon damage, helmet vision, etc.)

**Cả 2 có thể dùng cùng lúc**:
- Weapon có thể có `damage = 50f` (specific) VÀ `baseStatModifiers` để modify `StatType.Damage` (generic)
- Helmet có thể có `visionRange = 20f` (specific) VÀ `baseStatModifiers` để modify `StatType.VisionRange` (generic)
