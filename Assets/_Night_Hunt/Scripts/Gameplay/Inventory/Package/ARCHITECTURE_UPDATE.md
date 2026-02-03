# Architecture Update - ComponentFinder & PlayerInventoryCache

## 🔄 Thay đổi kiến trúc

Hệ thống đã được cập nhật để sử dụng **ComponentFinder** và **PlayerInventoryCache** thay vì `GetComponent` trực tiếp.

## ✅ Các thay đổi

### 1. ComponentFinder
- **Vị trí**: `Runtime/Core/ComponentFinder.cs`
- **Chức năng**: Tìm component trong hierarchy (self, children, parent)
- **Sử dụng**: Khi components không nằm trên cùng GameObject mà ở child objects

```csharp
// Thay vì:
var manager = GetComponent<InventoryManager>();

// Sử dụng:
var manager = ComponentFinder.FindInHierarchy<InventoryManager>(this);
```

### 2. PlayerInventoryCache
- **Vị trí**: `Runtime/Core/PlayerInventoryCache.cs`
- **Chức năng**: Cache player và tất cả inventory components cho UI
- **Lý do**: UI nằm riêng ngoài Canvas của player, cần cache để truy cập

```csharp
// UI sử dụng cache thay vì GetComponent:
var inventoryManager = PlayerInventoryCache.Instance.InventoryManager;
```

### 3. PlayerInventoryCacheInitializer
- **Vị trí**: `Runtime/Core/PlayerInventoryCacheInitializer.cs`
- **Chức năng**: Tự động cache player khi spawn
- **Cách dùng**: Attach vào NetworkPlayer hoặc child object

## 📝 Components đã cập nhật

### Domain Layer
- ✅ `InventoryManager` - Không cần thay đổi (được tìm bởi ComponentFinder)
- ✅ `InventoryOperationValidator` - Dùng ComponentFinder
- ✅ `EquipmentManager` - Được cache trong PlayerInventoryCache
- ✅ `WeaponManager` - Được cache trong PlayerInventoryCache
- ✅ `QuickSlotManager` - Được cache trong PlayerInventoryCache

### UI Layer
- ✅ `InventoryUIController` - Dùng PlayerInventoryCache thay vì GetComponent
- ✅ `ContainerUIController` - Cần cập nhật nếu có

### Interaction Layer
- ✅ `WorldItem` - Dùng ComponentFinder để tìm InventoryManager

### Container Layer
- ✅ `PlayerCorpseSpawner` - Dùng ComponentFinder để tìm tất cả managers

### Networking Layer
- ✅ `InventoryNetworkSync` - Dùng ComponentFinder
- ✅ `InventoryNetworkClient` - Dùng ComponentFinder

### QuickSlot Layer
- ✅ `QuickSlotInputHandler` - Dùng ComponentFinder
- ✅ `ConsumableUsage` - Được tìm bởi ComponentFinder

## 🎯 Cách sử dụng

### 1. Setup PlayerInventoryCache
```csharp
// Tạo GameObject riêng cho cache (không phải trên player)
// Attach PlayerInventoryCache component
// Attach PlayerInventoryCacheInitializer vào NetworkPlayer hoặc child
```

### 2. UI Controllers
```csharp
// Trong UI controller (nằm ngoài player Canvas):
private InventoryManager GetInventoryManager()
{
    if (PlayerInventoryCache.Instance == null || !PlayerInventoryCache.Instance.IsCacheValid())
        return null;
    
    return PlayerInventoryCache.Instance.InventoryManager;
}
```

### 3. Components trong Hierarchy
```csharp
// Khi component nằm trên child object:
void Awake()
{
    inventoryManager = ComponentFinder.FindInHierarchy<InventoryManager>(this);
    equipmentManager = ComponentFinder.FindInHierarchy<EquipmentManager>(this);
}
```

## 🔧 Setup trong Unity

### Bước 1: Tạo PlayerInventoryCache GameObject
1. Tạo GameObject mới (không phải child của player)
2. Đặt tên: "PlayerInventoryCache"
3. Attach component: `PlayerInventoryCache`
4. Đặt DontDestroyOnLoad nếu cần

### Bước 2: Setup PlayerInventoryCacheInitializer
1. Trên NetworkPlayer prefab hoặc child object
2. Attach component: `PlayerInventoryCacheInitializer`
3. Component sẽ tự động cache player khi spawn

### Bước 3: UI Setup
1. UI nằm riêng ngoài player Canvas
2. InventoryUIController tự động sử dụng cache
3. Không cần reference trực tiếp đến player

## ✅ Lợi ích

1. **Tách biệt UI**: UI không phụ thuộc vào player Canvas
2. **Linh hoạt**: Components có thể nằm ở bất kỳ đâu trong hierarchy
3. **Dễ maintain**: Không cần hardcode GetComponent
4. **Performance**: Cache giảm số lần tìm component
5. **Clean Architecture**: Tách biệt rõ ràng giữa UI và Domain

## 📋 Checklist

- [x] ComponentFinder utility class
- [x] PlayerInventoryCache singleton
- [x] PlayerInventoryCacheInitializer
- [x] Cập nhật InventoryUIController
- [x] Cập nhật tất cả components dùng ComponentFinder
- [x] Cập nhật WorldItem
- [x] Cập nhật PlayerCorpseSpawner
- [x] Cập nhật Networking components
- [x] Cập nhật QuickSlot components
