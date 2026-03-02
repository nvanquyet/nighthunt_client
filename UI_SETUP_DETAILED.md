# 🎮 DETAILED UI COMPONENT SETUP GUIDE

**Chi tiết về cách setup từng component UI**

---

## Part 1: Canvas Root Setup

### Step 1.1: Create Canvas

```
Right-click in Hierarchy
→ UI → Canvas

Result:
Canvas
├─ Canvas Scaler
├─ Graphic Raycaster
└─ (Child elements here)
```

### Step 1.2: Canvas Settings

**In Inspector:**

```
Canvas Component
├─ Render Mode: Screen Space - Overlay
├─ Scale Factor: 1
├─ Reference Resolution: 1920 × 1080
└─ Pixel Perfect: false

Canvas Scaler
├─ UI Scale Mode: Scale with Screen Size
├─ Reference Resolution: 1920 × 1080
├─ Screen Match Mode: Expand
└─ Match: 1
```

### Step 1.3: Add GameHUD Script

```
Canvas (select it)
In Inspector → Add Component
Search: GameHUD
Result: GameHUD script added to Canvas
```

---

## Part 2: CombatHUDPanel Setup

### Step 2.1: Create CombatHUDPanel Container

```
Right-click Canvas
→ UI → Panel - Image Based

Rename: CombatHUDPanel

Position: Bottom left corner
Size: 400 × 200
Pivot: (0, 0) = bottom-left
```

**Component Setup:**

```
Image (remove or make transparent)
├─ Color: (0, 0, 0, 0) - fully transparent
└─ Raycast Target: false

Layout Group (optional):
└─ Vertical Layout Group
   ├─ Child Force Expand: Height = false
   ├─ Child Force Expand: Width = true
   └─ Spacing: 5
```

### Step 2.2: Add CombatHUDPanel Script

```
CombatHUDPanel (select it)
In Inspector → Add Component
Search: CombatHUDPanel
Result: CombatHUDPanel script added
```

**Fields to populate later:**
- _primaryButton
- _secondaryButton
- _meleeButton
- _quickSlotButtons[]
- _ammoLabel
- _reserveLabel
- _reloadingIndicator
- _depletedWarning

---

## Part 3: Weapon Slot Buttons Setup (×3)

### Step 3.1: Create First Weapon Slot Button (Primary)

```
Right-click CombatHUDPanel
→ UI → Button - TextMeshPro

Rename: PrimarySlotButton

Position: (0, 0)
Size: (120, 120)
Anchor: Bottom Left
Pivot: (0, 0)
```

**Result hierarchy:**
```
PrimarySlotButton (Button)
├─ Image (background)
├─ TextMeshProUGUI (label)
└─ (We'll add more children)
```

### Step 3.2: Configure Button Image

**Select:** `PrimarySlotButton → Image` (the background)

```
In Inspector:
├─ Sprite: None (or UI/Square)
├─ Color: (0.3, 0.3, 0.3, 1.0) - dark gray
├─ Material: Default
└─ Raycast Target: true
```

### Step 3.3: Add Button Component Settings

**Select:** `PrimarySlotButton`

```
In Inspector → Button Component:
├─ Navigation: Automatic
├─ Transition: Color Tint
├─ Normal Color: (0.3, 0.3, 0.3, 1.0)
├─ Highlighted Color: (0.5, 0.5, 0.5, 1.0)
├─ Pressed Color: (0.2, 0.2, 0.2, 1.0)
└─ Selected Color: (0.7, 0.7, 0.7, 1.0)
```

### Step 3.4: Set Up Child Elements

**Create weapon icon:**
```
Right-click PrimarySlotButton
→ UI → Image

Rename: Icon
Position: (5, 5)
Size: (70, 70)
Anchor: Top Left
Color: (1, 1, 1, 1)

Assign Sprite: (weapon icon from Resources)
```

**Create ammo text:**
```
Right-click PrimarySlotButton
→ UI → TextMeshPro - Text

Rename: AmmoText
Position: (5, -95)
Size: (110, 20)
Anchor: Top Left
Text: "30 / 90"
Font Size: 18
Alignment: Bottom Left
Color: (1, 1, 1, 1)
```

**Create empty slot overlay:**
```
Right-click PrimarySlotButton
→ UI → Image

Rename: EmptySlotOverlay
Size: Same as parent (120, 120)
Position: (0, 0)
Anchor: Stretch
Color: (0, 0, 0, 0.7) - semi-transparent black
"Show when slot empty" - we'll toggle with script
```

**Create selected border:**
```
Right-click PrimarySlotButton
→ UI → Image

Rename: SelectedBorder
Size: (120, 120)
Position: (0, 0)
Anchor: Stretch
Image Type: Sliced
Border: Leave at defaults or Outline preset
Color: (0, 1, 0, 1) - Green (highlight active weapon)
Raycast Target: false
```

### Step 3.5: Add WeaponSlotButton Script

**Select:** `PrimarySlotButton`

```
In Inspector → Add Component
Search: WeaponSlotButton
Result: WeaponSlotButton script added
```

**Fill in Inspector Fields:**

```
Weapon Slot UI
├─ Selected Border → Drag SelectedBorder Image here
├─ Ammo Text → Drag AmmoText here
└─ Empty Slot Overlay → Drag EmptySlotOverlay here

Slot Config
└─ Slot Type → Select "Primary" from dropdown ⭐ IMPORTANT

Action Button (parent class)
├─ Button → Auto-filled (Button component)
├─ Icon → Drag Icon Image here
├─ Label → Drag TextMeshProUGUI here (if exists)
└─ Cooldown Ring → Leave empty (optional)
```

### Step 3.6: Duplicate for Secondary & Melee

1. Right-click `PrimarySlotButton` → Duplicate (×2)
2. Rename to: `SecondarySlotButton`, `MeleeSlotButton`
3. Reposition: Offset X by 130 pixels each

**Update Slot Type:**
```
SecondarySlotButton:
└─ Slot Type: Secondary

MeleeSlotButton:
└─ Slot Type: Melee
```

---

## Part 4: Quick Slot Buttons Setup (×4)

### Step 4.1: Create Quick Slot Container

```
Right-click CombatHUDPanel
→ UI → Panel - Image Based

Rename: QuickSlotsPanel

Position: (130, 0) - to right of weapon slots
Size: (250, 100)
Anchor: Bottom Left
Pivot: (0, 0)

Image: Transparent (Color = 0,0,0,0)
```

### Step 4.2: Create First Quick Slot Button

```
Right-click QuickSlotsPanel
→ UI → Button - TextMeshPro

Rename: QuickSlot0Button

Position: (0, 0)
Size: (100, 100)
Anchor: Bottom Left
Pivot: (0, 0)
```

**Add children** (same as weapon slot):
- Icon (Image)
- QuickSlotLabel (TextMeshPro)
- CooldownRing (Image) - for when on cooldown
- EmptyOverlay (Image)

### Step 4.3: Add QuickSlotHUDButton Script

**Select:** `QuickSlot0Button`

```
In Inspector → Add Component
Search: QuickSlotHUDButton
```

**Fill Inspector:**
```
Quick Slot UI
├─ Icon → Icon Image
├─ Label → QuickSlotLabel text
└─ Cooldown Ring → CooldownRing Image

Slot Index
└─ Slot Index → 0 ⭐ (Slot 0-3)
```

### Step 4.4: Duplicate for Slots 1, 2, 3

1. Duplicate `QuickSlot0Button` (×3)
2. Rename: QuickSlot1Button, QuickSlot2Button, QuickSlot3Button
3. Reposition: Offset X by 105 pixels each time

**Update Slot Index:**
```
QuickSlot1Button → Slot Index: 1
QuickSlot2Button → Slot Index: 2
QuickSlot3Button → Slot Index: 3
```

---

## Part 5: Ammo Display Setup

### Step 5.1: Create Ammo Display Container

```
Right-click CombatHUDPanel
→ UI → Panel - Image Based

Rename: AmmoDisplayPanel

Position: (130, 110) - above quick slots
Size: (250, 60)
Anchor: Bottom Left
Pivot: (0, 0)

Image: Transparent
```

### Step 5.2: Create Ammo Label

```
Right-click AmmoDisplayPanel
→ UI → TextMeshPro - Text

Rename: AmmoLabel

Size: (250, 30)
Position: (0, 20)
Anchor: Top Stretch
Text: "30 / 90"
Font Size: 28
Bold: YES
Alignment: Center
Color: (0, 1, 0, 1) - Lime green
```

### Step 5.3: Create Reserve Label

```
Right-click AmmoDisplayPanel
→ UI → TextMeshPro - Text

Rename: ReserveLabel

Size: (250, 20)
Position: (0, 0)
Anchor: Bottom Stretch
Text: "Reserve"
Font Size: 12
Alignment: Center
Color: (0.8, 0.8, 0.8, 1) - Light gray
```

### Step 5.4: Create Reload Indicator

```
Right-click AmmoDisplayPanel
→ UI → Image

Rename: ReloadingIndicator

Size: (50, 50)
Position: (200, 5)
Anchor: Bottom Right
Sprite: LoadingSpinner or UI/Circular
Color: (1, 1, 0, 1) - Yellow

Later: Add animation script to rotate it
```

**Initial state: Disable it**
```
Checkbox: Uncheck (disabled by default)
```

### Step 5.5: Create Depleted Warning

```
Right-click AmmoDisplayPanel
→ UI → TextMeshPro - Text

Rename: DepletedWarning

Size: (250, 30)
Position: (0, 20)
Anchor: Top Stretch
Text: "OUT OF AMMO!"
Font Size: 24
Bold: YES
Alignment: Center
Color: (1, 0, 0, 1) - Red

Initial state: Disable it
```

**Checkbox: Uncheck (disabled) at start**

### Step 5.6: Link to CombatHUDPanel

**Select:** CombatHUDPanel

**In Inspector:**

```
Ammo Display
├─ Ammo Label → Drag AmmoLabel TextMeshPro here
├─ Reserve Label → Drag ReserveLabel here

Status Messages
├─ Reloading Indicator → Drag ReloadingIndicator here
└─ Depleted Warning → Drag DepletedWarning here
```

---

## Part 6: Player HUD Panel Setup

### Step 6.1: Create PlayerHUDPanel

```
Right-click Canvas
→ UI → Panel - Image Based

Rename: PlayerHUDPanel

Position: (0, 0) - top-left
Size: (400, 150)
Anchor: Top Left
Pivot: (1, 1)

Image: Transparent or semi-dark
```

### Step 6.2: Add Health Bar

```
Right-click PlayerHUDPanel
→ UI → Slider - Horizontal

Rename: HealthBar

Position: (0, -30)
Size: (350, 30)
Anchor: Top Stretch
Min Value: 0
Max Value: 100
Value: 75

Slider Component
├─ Handle Slide Area: (rect)
├─ Background: (image)
└─ Fill: 
   └─ Image Color: (1, 0, 0, 1) - Red for health
```

### Step 6.3: Add Health Text

```
Right-click PlayerHUDPanel
→ UI → TextMeshPro - Text

Rename: HealthText

Position: (10, -30)
Size: (330, 30)
Anchor: Top Left
Text: "HP: 75 / 100"
Font Size: 18
Alignment: Left
Color: (1, 1, 1, 1)
```

### Step 6.4: Add Stamina & Armor Bars (Same pattern)

**Stamina Bar:**
```
Similar to Health but:
├─ Position: (0, -70)
├─ Text: "Stamina: 60 / 100"
└─ Color: (0, 0.5, 1, 1) - Blue
```

**Armor Bar:**
```
Similar to Health but:
├─ Position: (0, -110)
├─ Text: "Armor: 40 / 100"
└─ Color: (0.7, 0.7, 0.7, 1) - Gray
```

### Step 6.5: Add PlayerHUDPanel Script

**Select:** PlayerHUDPanel

```
In Inspector → Add Component
Search: PlayerHUDPanel
```

**Assign all bars and texts in Inspector**

---

## Part 7: Crosshair Setup

### Step 7.1: Create Crosshair Container

```
Right-click Canvas
→ UI → Panel - Image Based

Rename: CrosshairPanel

Size: (200, 200)
Position: Center screen (0, 0)
Anchor: Middle Center
Pivot: (0.5, 0.5)

Image:Transparent
```

### Step 7.2: Add Crosshair Image

```
Right-click CrosshairPanel
→ UI → Image

Rename: CrosshairImage

Size: (50, 50)
Anchor: Middle Center
Pivot: (0.5, 0.5)
Position: (0, 0)

Sprite: Find crosshair in Resources
Color: (0, 1, 0, 1) - Green
```

### Step 7.3: Add CrosshairUI Script

**Select:** CrosshairPanel

```
In Inspector → Add Component
Search: CrosshairUI
Result: Script added
```

**Assign:**
```
Crosshair Image → Drag CrosshairImage here
```

---

## Part 8: Link Everything in CombatHUDPanel

**Select:** CombatHUDPanel

**Fill these fields:**

```
Weapon Slot Buttons
├─ Primary Button → PrimarySlotButton
├─ Secondary Button → SecondarySlotButton
└─ Melee Button → MeleeSlotButton

Quick Slot Buttons (Array of 4)
├─ Element 0 → QuickSlot0Button
├─ Element 1 → QuickSlot1Button
├─ Element 2 → QuickSlot2Button
└─ Element 3 → QuickSlot3Button

Ammo Display
├─ Ammo Label → AmmoLabel
├─ Reserve Label → ReserveLabel

Status Messages
├─ Reloading Indicator → ReloadingIndicator
└─ Depleted Warning → DepletedWarning
```

---

## Part 9: Link Everything in GameHUD

**Select:** Canvas (with GameHUD script)

**Fill these main fields:**

```
Core HUD Panels
├─ Player HUD Panel → PlayerHUDPanel
├─ Combat HUD Panel → CombatHUDPanel
└─ UI Root Controller → (UIRootController if exists)

Crosshair
└─ Crosshair UI → CrosshairPanel

Death Screen & Other
├─ Minimap UI → MinimapUI (create separate if needed)
├─ Interaction Prompt UI → InteractionPromptUI
└─ Etc...
```

---

## Part 10: Final Verification

### Hierarchy Check

```
Canvas
├─ GameHUD (Script) ✓
├─ PlayerHUDPanel
│  ├─ HealthBar (Slider) ✓
│  ├─ HealthText (TextMeshPro) ✓
│  ├─ StaminaBar (Slider) ✓
│  ├─ ArmorBar (Slider) ✓
│  └─ PlayerHUDPanel (Script) ✓
│
├─ CombatHUDPanel
│  ├─ CombatHUDPanel (Script) ✓
│  ├─ PrimarySlotButton ✓
│  │  ├─ SelectedBorder ✓
│  │  ├─ Icon ✓
│  │  ├─ AmmoText ✓
│  │  └─ WeaponSlotButton (Script) ✓
│  ├─ SecondarySlotButton ✓
│  ├─ MeleeSlotButton ✓
│  ├─ QuickSlotsPanel
│  │  ├─ QuickSlot0Button ✓
│  │  ├─ QuickSlot1Button ✓
│  │  ├─ QuickSlot2Button ✓
│  │  └─ QuickSlot3Button ✓ (all 4 with QuickSlotHUDButton script)
│  └─ AmmoDisplayPanel
│     ├─ AmmoLabel ✓
│     ├─ ReloadingIndicator ✓
│     └─ DepletedWarning ✓
│
└─ CrosshairPanel
   ├─ CrosshairUI (Script) ✓
   └─ CrosshairImage ✓
```

### Inspector Fields Check

```
□ All WeaponSlotButton have correct Slot Type
□ All WeaponSlotButton have references filled
□ All QuickSlotHUDButton have correct Slot Index
□ All QuickSlotHUDButton have references filled
□ CombatHUDPanel has all 3 weapon buttons assigned
□ CombatHUDPanel has all 4 quick slot buttons in array
□ CombatHUDPanel has ammo labels assigned
□ CombatHUDPanel has status indicators assigned
□ GameHUD has CombatHUDPanel assigned
□ GameHUD has PlayerHUDPanel assigned
□ GameHUD has CrosshairUI assigned
```

---

## Part 11: Common UI Mistakes

### ❌ Mistake 1: Slot Type Not Set

```
Error: Buttons don't respond correctly
Solution: Select each WeaponSlotButton
         Set _slotType correctly (Primary/Sec/Melee)
```

### ❌ Mistake 2: Empty References

```
Error: NullReferenceException in inspector
Solution: Verify all "Drag here" fields have something
         Use GameObject dropdowns if manual entry fails
```

### ❌ Mistake 3: Wrong Anchor/Pivot

```
Error: UI elements in wrong positions
Solution: Use the visual Rect Transform tool
         Or copy values from reference guide
```

### ❌ Mistake 4: Script Not Assigned

```
Error: UI not updating when weapon fires
Solution: Add Component → Search script name
         Add ComponentVerify it shows in Inspector
```

### ❌ Mistake 5: Hierarchy Mismatch

```
Error: Initialize() called but nothing happens
Solution: Check your UI matches the hierarchy diagram
         Especially parent-child relationships
```

---

## Part 12: Testing Your UI

### Test 1: Visual Inspection
```
□ Play scene
□ Look at Game view
□ UI should appear at bottom-left
□ Weapon buttons should be visible
□ Ammo counter should show (e.g., "30 / 90")
```

### Test 2: Interaction
```
□ Hover over weapon button → Should highlight
□ Click weapon button → Should trigger equip
□ Ammo text updates when you fire
```

### Test 3: Console
```
No red errors in console
No warnings about null references
See "Gameplay initialized!" message
```

### Test 4: Update Behavior
```
Fire weapon → Ammo decrements
Alternate weapons → Slot highlight changes
Quick slot uses → Shows cooldown
```

---

## 🎨 UI CUSTOMIZATION OPTIONS

### Colors Preset

**Red theme (aggressive):**
```
UI: (1, 0, 0, 1)
Text: (1, 1, 0, 1) yellow
Active: (0.5, 0, 0, 1) dark red
```

**Blue theme (cool):**
```
UI: (0, 0.5, 1, 1) bright blue
Text: (0.8, 1, 1, 1) cyan
Active: (0, 0.2, 1, 1) dark blue
```

**Green theme (nature):**
```
UI: (0, 1, 0, 1) bright green
Text: (0, 1, 0.5, 1) lime
Active: (0, 0.5, 0, 1) dark green
```

### Size Options

**Compact UI:**
```
Button Size: 80 × 80
Font Size: 12
Panel Height: 150
```

**Large UI (better visibility):**
```
Button Size: 150 × 150
Font Size: 24
Panel Height: 250
```

---

## Final Checklist

Before calling UI "done":

- [ ] Hierarchy matches visual reference diagram
- [ ] All components have correct scripts
- [ ] All inspector fields filled (no empty slots)
- [ ] All buttons have WeaponSlotButton or QuickSlotHUDButton
- [ ] Slot Type / Slot Index set correctly
- [ ] GameHUD references point to correct panels
- [ ] CombatHUDPanel references correct buttons
- [ ] No red errors in console
- [ ] UI appears in game view
- [ ] Ammo counter displays value
- [ ] Can test firing and ammo updates

**All checked? UI is ready!** ✅

---

**Remember:** These are templates. Feel free to customize colors, positions, and fonts to match your game style!
