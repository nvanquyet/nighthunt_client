# 🎮 NightHunt Gameplay Flow Diagrams

Tài liệu mô tả toàn bộ luồng và chức năng của gameplay system.

## 📋 Mục lục

1. [Kiến trúc tổng quan](#1-kiến-trúc-tổng-quan)
2. [Luồng chính](#2-luồng-chính)
3. [Hệ thống chính](#3-hệ-thống-chính)
4. [UI System Flow](#4-ui-system-flow)
5. [Spectate Mode Flow](#5-spectate-mode-flow)
6. [Interaction System](#6-interaction-system)
7. [QuickSlot System](#7-quickslot-system)
8. [Data Flow](#8-data-flow)

---

## 1. Kiến trúc tổng quan

```mermaid
graph TB
    Start[Game Start] --> Init[GameManager Initialize]
    Init --> Auth[Authentication Service]
    Auth --> Lobby[Lobby/Room System]
    Lobby --> Match[Match Start]
    Match --> Spawn[Player Spawn]
    Spawn --> Gameplay[Gameplay Loop]
    
    Gameplay --> Movement[Character Movement]
    Gameplay --> Combat[Combat System]
    Gameplay --> Inventory[Inventory System]
    Gameplay --> Interaction[Interaction System]
    Gameplay --> Spectate[Spectate Mode]
    
    Movement --> NetworkSync[Network Sync]
    Combat --> NetworkSync
    Inventory --> NetworkSync
    Interaction --> NetworkSync
    
    NetworkSync --> Server[Server Validation]
    Server --> Broadcast[Broadcast to Clients]
    Broadcast --> UpdateUI[Update UI]
    
    Gameplay --> Death[Player Death]
    Death --> Respawn[Respawn System]
    Respawn --> Spectate
    Spectate --> Gameplay
```

---

## 2. Luồng chính

### 2.1 Game Manager & Services

```mermaid
graph LR
    GM[GameManager] --> Auth[AuthService]
    GM --> Room[RoomService]
    GM --> Backend[BackendHttpClient]
    GM --> WS[GameWebSocketService]
    GM --> State[SessionState/RoomState]
    
    Auth --> Login[Login Flow]
    Room --> Join[Join Room]
    WS --> Events[Game Events]
    State --> Persist[Persistent Data]
```

### 2.2 Network System

```mermaid
graph TB
    Client[Client] --> Local[Local Prediction]
    Local --> ServerRpc[ServerRpc Request]
    ServerRpc --> Server[Server]
    Server --> Validate[Validate & Process]
    Validate --> ObserversRpc[ObserversRpc Broadcast]
    ObserversRpc --> AllClients[All Clients]
    AllClients --> Reconcile[Reconcile Prediction]
    Reconcile --> Update[Update State]
```

### 2.3 Character System

```mermaid
graph TB
    Player[NetworkPlayer] --> Movement[CharacterNormalMovement]
    Player --> Combat[CharacterCombat]
    Player --> Camera[CinemachineCamera]
    Player --> Inventory[Inventory Managers]
    
    Movement --> Input[MovementInputHandler]
    Combat --> CombatInput[CombatInputHandler]
    Camera --> CameraInput[CameraInputHandler]
    
    Input --> Network[Network Sync]
    CombatInput --> Network
    Network --> Prediction[Client Prediction]
```

---

## 3. Hệ thống chính

### 3.1 Inventory System Architecture

```mermaid
graph TB
    Inventory[InventoryManager] --> Data[InventoryData]
    Equipment[EquipmentManager] --> Slots[Equipment Slots]
    Weapon[WeaponManager] --> WeaponSlots[Primary/Secondary]
    QuickSlot[QuickSlotManager] --> QuickSlots[4 Quick Slots]
    Attachment[AttachmentManager] --> Attachments[Scope/Grip/Muzzle]
    
    Inventory --> Events[InventoryEvents]
    Equipment --> Events
    Weapon --> Events
    QuickSlot --> Events
    Attachment --> Events
    
    Events --> UI[UI System]
    UI --> DragDrop[Drag & Drop]
    UI --> Tooltip[Tooltip System]
    UI --> Panels[Inventory Panels]
    
    Inventory --> NetworkSync[InventoryNetworkSync]
    Equipment --> NetworkSync
    Weapon --> NetworkSync
    QuickSlot --> QuickSlotNetworkSync
    Attachment --> NetworkSync
```

### 3.2 Inventory Slot Locations

```mermaid
graph LR
    Inventory[Inventory Slots] --> Equipment[Equipment Slots]
    Inventory --> Weapon[Weapon Slots]
    Inventory --> QuickSlot[QuickSlot Slots]
    Inventory --> Container[Container Slots]
    
    Equipment --> Helmet[Helmet]
    Equipment --> Armor[Armor]
    Equipment --> Backpack[Backpack]
    
    Weapon --> Primary[Primary Weapon]
    Weapon --> Secondary[Secondary Weapon]
    
    QuickSlot --> Slot1[Slot 1 Ctrl+1]
    QuickSlot --> Slot2[Slot 2 Ctrl+2]
    QuickSlot --> Slot3[Slot 3 Ctrl+3]
    QuickSlot --> Slot4[Slot 4 Ctrl+4]
    
    Weapon --> Attachments[Attachments]
    Attachments --> Scope[Scope]
    Attachments --> Grip[Grip]
    Attachments --> Muzzle[Muzzle]
    Attachments --> Magazine[Magazine]
```

---

## 4. UI System Flow

### 4.1 UI Root Controller Flow

```mermaid
graph TB
    Input[Input Handler] --> Toggle[Toggle Inventory]
    Toggle --> UIRoot[UIRootController]
    UIRoot --> HUD[Player HUD]
    UIRoot --> InventoryUI[Inventory UI]
    
    InventoryUI --> Panels[UI Panels]
    Panels --> InventoryPanel[Inventory Panel]
    Panels --> EquipmentPanel[Equipment Panel]
    Panels --> WeaponPanel[Weapon Panel]
    Panels --> QuickSlotPanel[QuickSlot Panel]
    Panels --> AttachmentPanel[Attachment Panel]
    
    Panels --> DragDrop[DragDropHandler]
    DragDrop --> Events[DragDropEvents]
    Events --> Managers[Domain Managers]
    Managers --> NetworkSync[Network Sync]
    
    Spectate[SpectateManager] --> CurrentPlayer[Get Current Player]
    CurrentPlayer --> UIRoot
    CurrentPlayer --> BlockInteraction[Block Drag-Drop if Spectating]
```

### 4.2 Drag & Drop Flow

```mermaid
sequenceDiagram
    participant User
    participant CellUI as InventoryCellUI
    participant DragHandler as DragDropHandler
    participant Manager as Domain Manager
    participant NetworkSync
    participant Server
    
    User->>CellUI: Begin Drag
    CellUI->>DragHandler: InvokeBeginDrag Event
    User->>CellUI: Drop on Target
    CellUI->>DragHandler: InvokeDrop Event
    DragHandler->>Manager: ProcessDrop
    Manager->>NetworkSync: RequestServerRpc
    NetworkSync->>Server: ServerRpc
    Server->>Server: Validate
    Server->>NetworkSync: ObserversRpc
    NetworkSync->>Manager: Apply Change
    Manager->>CellUI: Update via Events
```

---

## 5. Spectate Mode Flow

### 5.1 Spectate Mode Architecture

```mermaid
graph TB
    Local[Local Player] --> Check{Is Spectating?}
    Check -->|No| Normal[Normal Gameplay]
    Check -->|Yes| Spectate[Spectate Mode]
    
    Spectate --> SpectateManager[SpectateManager]
    SpectateManager --> CurrentPlayer[Get Current Player]
    CurrentPlayer --> UIUpdate[Update UI with Spectated Player Data]
    
    UIUpdate --> ViewOnly[View Only Mode]
    ViewOnly --> NoDrag[No Drag-Drop]
    ViewOnly --> NoPrompt[No Prompts]
    ViewOnly --> Hover[Hover Works]
    ViewOnly --> Tooltip[Tooltip Works]
    
    Normal --> FullControl[Full Control]
    FullControl --> DragDrop[Drag-Drop Enabled]
    FullControl --> Use[Use Items]
    FullControl --> Interact[Interact with Objects]
```

### 5.2 Spectate Mode State Flow

```mermaid
stateDiagram-v2
    [*] --> LocalPlayer: Player Spawns
    LocalPlayer --> Spectating: Start Spectating
    Spectating --> LocalPlayer: Stop Spectating
    Spectating --> Spectating: Switch Player
    
    LocalPlayer: Full Control
    LocalPlayer: Drag-Drop Enabled
    LocalPlayer: Prompts Enabled
    
    Spectating: View Only
    Spectating: No Drag-Drop
    Spectating: No Prompts
    Spectating: Hover Works
```

---

## 6. Interaction System

### 6.1 Interaction Detection Flow

```mermaid
graph TB
    Raycast[Raycast Detection] --> Detector[InteractionDetector]
    Detector --> Interactable[IInteractable Objects]
    
    Interactable --> Item[World Items]
    Interactable --> Container[Containers]
    Interactable --> Player[Player Corpses]
    
    Item --> Pickup[Pickup Item]
    Container --> Open[Open Container]
    Player --> Loot[Loot Corpse]
    
    Pickup --> Inventory[Add to Inventory]
    Open --> ContainerUI[Show Container UI]
    Loot --> ContainerUI
    
    Interaction --> Events[InteractionEvents]
    Events --> Prompt[Show Prompt UI]
    Events --> Progress[Show Progress Bar]
```

### 6.2 Container Interaction Flow

```mermaid
sequenceDiagram
    participant Player
    participant Raycast
    participant Container
    participant ContainerUI
    participant Inventory
    
    Player->>Raycast: Detect Container
    Raycast->>Container: Get Container Data
    Container->>Player: Show Prompt
    Player->>Container: Hold to Open
    Container->>ContainerUI: Open Container Panel
    ContainerUI->>Player: Display Items
    Player->>ContainerUI: Drag Item
    ContainerUI->>Inventory: Add to Inventory
    Inventory->>Container: Remove from Container
    Container->>ContainerUI: Update Display
```

---

## 7. QuickSlot System

### 7.1 QuickSlot Input Flow

```mermaid
graph TB
    Input[QuickSlotInputHandler] --> KeyPress[Ctrl+1/2/3/4]
    KeyPress --> FastPress{Press Duration}
    
    FastPress -->|< 0.3s| Use[Use Item Immediately]
    FastPress -->|> 0.3s| Select[Select Slot Only]
    
    Use --> QuickSlotManager[QuickSlotManager]
    QuickSlotManager --> ItemType{Item Type}
    
    ItemType -->|Consumable| ConsumableUsage[ConsumableUsage]
    ItemType -->|Throwable| Throw[Throw Item]
    ItemType -->|Weapon| Equip[Equip Weapon]
    
    ConsumableUsage --> Progress[Progress Bar]
    ConsumableUsage --> Events[QuickSlotEvents]
    Events --> UI[Update UI]
    
    DoubleClick[Double Click on UI] --> FirstClick[First Click: Select]
    FirstClick --> SecondClick[Second Click: Use]
    SecondClick --> Use
```

### 7.2 QuickSlot Usage Flow

```mermaid
sequenceDiagram
    participant User
    participant InputHandler as QuickSlotInputHandler
    participant Manager as QuickSlotManager
    participant Usage as ConsumableUsage
    participant UI
    participant NetworkSync
    
    User->>InputHandler: Press Ctrl+1 (< 0.3s)
    InputHandler->>Manager: Get Item at Slot 1
    Manager->>InputHandler: Return Item
    InputHandler->>Usage: Start Usage
    Usage->>UI: Show Progress Bar
    Usage->>NetworkSync: RequestServerRpc
    NetworkSync->>NetworkSync: Apply Locally
    Usage->>UI: Update Progress
    Usage->>Manager: Complete Usage
    Manager->>NetworkSync: Sync to Server
    NetworkSync->>UI: Update via Events
```

---

## 8. Data Flow

### 8.1 Network Synchronization Flow

```mermaid
sequenceDiagram
    participant User
    participant UI
    participant Manager
    participant NetworkSync
    participant Server
    participant OtherClients
    
    User->>UI: Drag Item
    UI->>Manager: TryAddItem
    Manager->>NetworkSync: RequestServerRpc
    NetworkSync->>Server: ServerRpc
    Server->>Server: Validate
    Server->>NetworkSync: ObserversRpc
    NetworkSync->>UI: Update via Events
    NetworkSync->>OtherClients: Broadcast
    OtherClients->>OtherClients: Update UI
```

### 8.2 Event-Driven Architecture

```mermaid
graph LR
    Action[User Action] --> Event[Event System]
    Event --> Domain[Domain Logic]
    Domain --> StateChange[State Change]
    StateChange --> Event2[State Change Event]
    Event2 --> UI[UI Update]
    Event2 --> Network[Network Sync]
    
    style Action fill:#e1f5ff
    style Event fill:#fff4e1
    style Domain fill:#e8f5e9
    style StateChange fill:#f3e5f5
    style Event2 fill:#fff4e1
    style UI fill:#e1f5ff
    style Network fill:#e1f5ff
```

---

## 9. State Management

### 9.1 UI Slot States

```mermaid
stateDiagram-v2
    [*] --> Empty: Initialize
    Empty --> Occupied: Add Item
    Occupied --> Empty: Remove Item
    
    Empty --> Hover: Pointer Enter
    Occupied --> Hover: Pointer Enter
    Hover --> Empty: Pointer Exit
    Hover --> Occupied: Pointer Exit
    
    Occupied --> Selected: Click/Select
    Selected --> Occupied: Unselect
    Selected --> Hover: Pointer Enter
    Hover --> Selected: Click
    
    Empty: No Item
    Occupied: Has Item
    Hover: Mouse Over
    Selected: Active Selection
```

---

## 10. Component Relationships

### 10.1 Manager Dependencies

```mermaid
graph TB
    NetworkPlayer[NetworkPlayer] --> InventoryManager[InventoryManager]
    NetworkPlayer --> EquipmentManager[EquipmentManager]
    NetworkPlayer --> WeaponManager[WeaponManager]
    NetworkPlayer --> QuickSlotManager[QuickSlotManager]
    NetworkPlayer --> AttachmentManager[AttachmentManager]
    
    InventoryManager --> InventoryData[InventoryData]
    EquipmentManager --> EquipmentConfig[EquipmentConfig]
    WeaponManager --> WeaponConfig[WeaponConfig]
    QuickSlotManager --> QuickSlotConfig[QuickSlotConfig]
    
    InventoryManager --> InventoryNetworkSync[InventoryNetworkSync]
    EquipmentManager --> NetworkSync[NetworkSync]
    WeaponManager --> NetworkSync
    QuickSlotManager --> QuickSlotNetworkSync[QuickSlotNetworkSync]
    AttachmentManager --> NetworkSync
    
    InventoryNetworkSync --> Server[Server]
    QuickSlotNetworkSync --> Server
    NetworkSync --> Server
```

---

## 📝 Ghi chú

- Tất cả các sơ đồ sử dụng Mermaid syntax
- Có thể render trên GitHub, GitLab, hoặc các markdown viewer hỗ trợ Mermaid
- Sơ đồ được cập nhật theo kiến trúc hiện tại của game

---

**Version**: 1.0.0  
**Last Updated**: 2024  
**Unity Version**: 6.0+  
**FishNet Version**: Pro v4+
