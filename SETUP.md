# NightHunt Client - Setup Guide

Hướng dẫn setup và luồng hoạt động từ Login → Home → Lobby.

---

## 📋 Mục lục

1. [Cấu trúc Project](#cấu-trúc-project)
2. [Luồng hoạt động chính](#luồng-hoạt-động-chính)
3. [Chi tiết từng Scene](#chi-tiết-từng-scene)
4. [Setup trong Unity Editor](#setup-trong-unity-editor)
5. [Cấu hình Backend](#cấu-hình-backend)
6. [Troubleshooting](#troubleshooting)

---

## 🏗️ Cấu trúc Project

```
NightHuntClient/
├── Assets/_Night_Hunt/
│   ├── Scripts/
│   │   ├── Core/                    # GameManager, SceneLoader, LoadingManager
│   │   ├── Services/                 # AuthService, RoomService, BackendHttpClient
│   │   ├── State/                    # SessionState, RoomState
│   │   ├── UI/                       # LoginView, HomeView, LobbyView, Popups
│   │   ├── Lobby/                    # LobbyController
│   │   ├── Networking/               # NetworkGameManager, NetworkPlayer
│   │   └── Data/                     # DTOs
│   ├── Scenes/
│   │   ├── FirstLoading.unity        # Scene khởi tạo đầu tiên
│   │   ├── Login.unity               # Scene đăng nhập
│   │   ├── Home.unity                # Scene menu chính
│   │   ├── Waiting.unity             # Scene lobby
│   │   └── Game.unity                # Scene game
│   └── Config/
│       └── BackendConfig.asset       # Cấu hình backend server
```

---

## 🔄 Luồng hoạt động chính

```
┌─────────────────┐
│  FirstLoading    │  ← Scene đầu tiên (khởi tạo GameManager, check auto-login)
└────────┬─────────┘
         │
         ├─→ Auto-login Success ──→ ┌─────────┐
         │                           │  Home   │  ← Menu chính
         │                           └────┬────┘
         │                                │
         └─→ Auto-login Failed ──→ ┌──────┴──────┐
                                   │   Login    │  ← Đăng nhập/Đăng ký
                                   └──────┬─────┘
                                          │
                                          └─→ Login Success ──→ Home
                                                
┌─────────────────────────────────────────────────────────────┐
│  Home Scene                                                 │
│  - Create Room                                             │
│  - Join Room (by code)                                     │
│  - Quick Play                                              │
└────────────────────┬────────────────────────────────────────┘
                     │
                     └─→ Join/Create Success ──→ ┌──────────┐
                                                 │ Waiting  │  ← Lobby
                                                 └────┬─────┘
                                                      │
                                                      └─→ Owner Start Game ──→ ┌──────┐
                                                                              │ Game │
                                                                              └──────┘
```

---

## 📍 Chi tiết từng Scene

### 1. FirstLoading Scene

**Mục đích:** Khởi tạo hệ thống, check auto-login

**Các component chính:**
- `GameManager` (DontDestroyOnLoad)
- `PersistentUICanvas` (DontDestroyOnLoad)
- `LoadingManager`

**Luồng hoạt động:**

```csharp
1. LoadingManager.Start()
   └─→ InitializeAndLoad()
       ├─→ WaitForGameManager()          // Đợi GameManager khởi tạo
       ├─→ WaitForPersistentUICanvas()   // Đợi PersistentUICanvas khởi tạo
       └─→ CheckAutoLogin()
           ├─→ AuthService.AutoLogin()
           │   ├─→ Success → LoadHome()
           │   └─→ Failed → LoadLogin()
           └─→ LoadTargetScene()          // Load scene đã set (nếu có)
```

**File liên quan:**
- `Assets/_Night_Hunt/Scripts/Core/LoadingManager.cs`
- `Assets/_Night_Hunt/Scripts/Core/GameManager.cs`
- `Assets/_Night_Hunt/Scripts/Services/Auth/AuthService.cs`

---

### 2. Login Scene

**Mục đích:** Đăng nhập/Đăng ký tài khoản

**Các component chính:**
- `LoginView`

**Luồng hoạt động:**

```csharp
LoginView.Start()
  └─→ LoadingManager.Hide()  // Ẩn loading (đã show ở FirstLoading)

User clicks "Login"
  └─→ LoginView.OnLoginClicked()
      └─→ AuthService.Login(identifier, password)
          ├─→ Success
          │   ├─→ SessionState.SetSession(...)
          │   ├─→ BackendHttpClient.SetAuthToken(...)
          │   └─→ SceneLoader.LoadHome()
          └─→ Failed
              └─→ NoticePopup.Show("Lỗi đăng nhập")

User clicks "Register"
  └─→ LoginView.OnRegisterClicked()
      └─→ AuthService.Register(username, email, password, confirmPassword)
          ├─→ Success
          │   ├─→ SessionState.SetSession(...)
          │   └─→ SceneLoader.LoadHome()
          └─→ Failed
              └─→ NoticePopup.Show("Lỗi đăng ký")
```

**File liên quan:**
- `Assets/_Night_Hunt/Scripts/UI/LoginView.cs`
- `Assets/_Night_Hunt/Scripts/Services/Auth/AuthService.cs`
- `Assets/_Night_Hunt/Scripts/State/SessionState.cs`

---

### 3. Home Scene

**Mục đích:** Menu chính sau khi đăng nhập

**Các component chính:**
- `HomeView`

**Chức năng:**
- Hiển thị thông tin user (username, email, userId)
- **Create Room:** Tạo phòng mới (mode, public/private, password)
- **Join Room:** Tham gia phòng bằng mã code
- **Quick Play:** Tìm phòng ngẫu nhiên hoặc tạo mới
- **Logout:** Đăng xuất

**Luồng hoạt động:**

```csharp
HomeView.Awake()
  └─→ Setup buttons, panels, dropdowns

User clicks "Create Lobby"
  └─→ HomeView.OnCreateLobbyClicked()
      └─→ Show createLobbyPanel

User fills form and clicks "Create"
  └─→ HomeView.OnCreateConfirmClicked()
      └─→ RoomService.CreateRoom(mode, isPublic, isLocked, password)
          ├─→ Success
          │   ├─→ RoomState.SetRoom(...)
          │   ├─→ RoomWebSocketService.ConnectToRoom(...)
          │   └─→ SceneLoader.LoadWaiting()
          └─→ Failed
              └─→ NoticePopup.Show("Lỗi tạo phòng")

User clicks "Join Lobby"
  └─→ HomeView.OnJoinLobbyClicked()
      └─→ Show joinLobbyPanel

User enters room code and clicks "Join"
  └─→ HomeView.OnJoinConfirmClicked()
      └─→ RoomService.JoinRoomByCode(roomCode, password)
          ├─→ Success
          │   ├─→ RoomState.SetRoom(...)
          │   ├─→ RoomWebSocketService.ConnectToRoom(...)
          │   └─→ SceneLoader.LoadWaiting()
          └─→ Failed
              └─→ NoticePopup.Show("Lỗi tham gia phòng")

User clicks "Quick Play"
  └─→ HomeView.OnQuickPlayClicked()
      └─→ RoomService.QuickPlay()
          ├─→ Success → LoadWaiting()
          └─→ Failed → NoticePopup.Show("Lỗi quick play")
```

**File liên quan:**
- `Assets/_Night_Hunt/Scripts/UI/HomeView.cs`
- `Assets/_Night_Hunt/Scripts/Services/Room/RoomService.cs`
- `Assets/_Night_Hunt/Scripts/Services/Room/RoomWebSocketService.cs`
- `Assets/_Night_Hunt/Scripts/State/RoomState.cs`

---

### 4. Waiting Scene (Lobby)

**Mục đích:** Quản lý phòng chờ, chuẩn bị game

**Các component chính:**
- `WaitingView`
- `LobbyView`
- `LobbyController`

**Chức năng:**
- Hiển thị thông tin phòng (room code, mode)
- Hiển thị danh sách players (Team 1 & Team 2)
- **Change Team/Slot:** Click vào slot trống hoặc slot khác team
- **Set Ready/Unready:** Bấm nút Ready
- **Swap Request:** Gửi yêu cầu đổi chỗ với player khác (timeout 5s, có thể cancel)
- **Kick Player:** Owner có thể kick player
- **Transfer Owner:** Owner có thể chuyển quyền owner
- **Start Game:** Owner bấm Start (cần tất cả ready + đủ players)
- **Real-time Updates:** WebSocket cập nhật real-time (**KHÔNG CÒN POLLING**)

**Luồng hoạt động:**

```csharp
WaitingView.Start()
  └─→ Find LobbyView in children
      └─→ LobbyView.Awake()
          ├─→ Setup buttons, slots
          └─→ Subscribe to RoomWebSocketService events

LobbyView.OnEnable()
  └─→ Subscribe to WebSocket events:
      ├─→ OnRoomUpdated → RefreshLobby()
      ├─→ OnPlayerJoined → Add player to UI
      ├─→ OnPlayerLeft → Remove player from UI
      ├─→ OnPlayerReady → Update ready status
      ├─→ OnTeamChanged → Update team/slot
      ├─→ OnSwapRequest → Show swap request panel (timeout 5s)
      ├─→ OnSwapRequestStatus → Handle accept/reject/cancel
      └─→ OnRoomStatusChanged → Check if game started → LoadGame()

// KHÔNG CÒN Update() hoặc polling coroutine
// Tất cả updates đều qua WebSocket events (real-time)

User clicks slot
  └─→ LobbyView.OnSlotClicked(team, slot)
      └─→ LobbyController.ChangeTeam(team, slot)
          └─→ RoomService.ChangeTeam(team, slot)

User clicks "Ready"
  └─→ LobbyView.OnReadyClicked()
      └─→ RoomService.SetReady(!isReady)

Owner clicks "Start"
  └─→ LobbyView.OnStartClicked()
      └─→ RoomService.StartGame()
          ├─→ Success
          │   └─→ RoomStatus = "IN_GAME"
          │       └─→ LobbyView detects status change
          │           └─→ SceneLoader.LoadGame()
          └─→ Failed
              └─→ NoticePopup.Show("Không thể bắt đầu game")
```

**File liên quan:**
- `Assets/_Night_Hunt/Scripts/UI/WaitingView.cs`
- `Assets/_Night_Hunt/Scripts/UI/LobbyView.cs`
- `Assets/_Night_Hunt/Scripts/Lobby/LobbyController.cs`
- `Assets/_Night_Hunt/Scripts/Services/Room/RoomService.cs`
- `Assets/_Night_Hunt/Scripts/Services/Room/RoomWebSocketService.cs`

---

### 5. Game Scene

**Mục đích:** Scene chơi game thực tế

**Các component chính:**
- `NetworkGameManager` (FishNet)
- `NetworkPlayer`
- `CharacterMovement`

**Luồng hoạt động:**
- Kết nối FishNet server
- Spawn player
- Gameplay logic

---

## 🔌 WebSocket vs Polling

### ✅ WebSocket (Real-time Updates)
**Lobby updates sử dụng WebSocket:**
- Kết nối khi join/create room
- Nhận updates real-time qua events:
  - `OnRoomUpdated` - Room info thay đổi
  - `OnPlayerJoined` - Player mới join
  - `OnPlayerLeft` - Player rời phòng
  - `OnPlayerReady` - Player ready/unready
  - `OnTeamChanged` - Player đổi team/slot
  - `OnSwapRequest` - Swap request mới
  - `OnSwapRequestStatus` - Swap request accepted/rejected/cancelled
  - `OnRoomStatusChanged` - Room status thay đổi (WAITING → IN_GAME)
- Ngắt kết nối khi leave/disband room
- **KHÔNG CÒN POLLING** - Tất cả updates đều real-time

**File:** `Assets/_Night_Hunt/Scripts/Services/Room/RoomWebSocketService.cs`

### ⚠️ SessionMonitor (Vẫn dùng Polling)
**Session status check vẫn dùng polling:**
- Check session validity mỗi 30s (configurable)
- Khác với room updates
- Chỉ check khi user đã đăng nhập

**File:** `Assets/_Night_Hunt/Scripts/Services/Auth/SessionMonitor.cs`

---

## ⚙️ Setup trong Unity Editor

### Bước 1: Mở Project

1. Mở Unity Hub
2. Add project: `NightHuntClient`
3. Chọn Unity version: **2022.3 LTS** hoặc mới hơn

### Bước 2: Cấu hình Backend

1. Navigate to `Assets/_Night_Hunt/Config/BackendConfig.asset`
2. Set **Base URL** = `http://localhost:8080` (hoặc backend server của bạn)
3. Set **Timeout** = `30` (seconds)

### Bước 3: Kiểm tra Scenes

Đảm bảo các scenes sau tồn tại:
- `Assets/_Night_Hunt/Scenes/FirstLoading.unity`
- `Assets/_Night_Hunt/Scenes/Login.unity`
- `Assets/_Night_Hunt/Scenes/Home.unity`
- `Assets/_Night_Hunt/Scenes/Waiting.unity`
- `Assets/_Night_Hunt/Scenes/Game.unity`

### Bước 4: Setup FirstLoading Scene

**FirstLoading scene phải có:**
- GameObject `GameManager` với component:
  - `GameManager`
  - `BackendHttpClient`
  - `AuthService`
  - `RoomService`
  - `SessionState`
  - `RoomState`
  - `SessionMonitor`
- GameObject `PersistentUICanvas` (hoặc GameManager sẽ tự tạo) với:
  - `PersistentUICanvas`
  - `LoadingManager` (child)
  - `NoticePopup` (child)
  - `ReconnectPopup` (child)

**Lưu ý:** `GameManager` và `PersistentUICanvas` phải có `DontDestroyOnLoad`.

### Bước 5: Setup Login Scene

**Login scene phải có:**
- GameObject `LoginView` với component `LoginView`
- UI Elements:
  - `usernameInput` (TMP_InputField)
  - `emailInput` (TMP_InputField)
  - `passwordInput` (TMP_InputField)
  - `confirmPasswordInput` (TMP_InputField)
  - `loginButton` (Button)
  - `registerButton` (Button)
  - `errorText` (TextMeshProUGUI - optional)

### Bước 6: Setup Home Scene

**Home scene phải có:**
- GameObject `HomeView` với component `HomeView`
- UI Elements:
  - User info: `usernameText`, `emailText`, `userIdText`
  - Buttons: `createLobbyButton`, `joinLobbyButton`, `quickPlayButton`, `logoutButton`
  - Join Panel: `joinLobbyPanel`, `roomCodeInput`, `joinConfirmButton`, `cancelJoinButton`
  - Create Panel: `createLobbyPanel`, `modeDropdown`, `isPublicToggle`, `isLockedToggle`, `passwordInput`, `createConfirmButton`, `cancelCreateButton`
  - `passwordPopup` (PasswordPopup component)

### Bước 7: Setup Waiting Scene

**Waiting scene phải có:**
- GameObject `WaitingView` với component `WaitingView`
- GameObject `LobbyView` (child hoặc sibling) với component `LobbyView`
- GameObject `LobbyController` với component `LobbyController`
- UI Elements:
  - `roomCodeText`, `modeText`
  - `team1Container`, `team2Container` (Transform)
  - `readyButton`, `leaveButton`, `startButton`
  - `playerSlotPrefab` (Prefab)
  - Swap Request Panel: `swapRequestPanel`, `swapRequestText`, `acceptSwapButton`, `rejectSwapButton`
  - Room Settings Panel (Owner only): `roomSettingsPanel`, `settingsButton`, `modeDropdown`, `isPublicToggle`, `isLockedToggle`, `passwordInput`, `saveSettingsButton`, `cancelSettingsButton`

### Bước 8: Build Settings

1. File → Build Settings
2. Add scenes theo thứ tự:
   - `FirstLoading`
   - `Login`
   - `Home`
   - `Waiting`
   - `Game`
3. Set **FirstLoading** làm scene đầu tiên

---

## 🔧 Cấu hình Backend

### BackendConfig.asset

**Location:** `Assets/_Night_Hunt/Config/BackendConfig.asset`

**Properties:**
- **Base URL:** `http://localhost:8080` (hoặc backend server URL)
- **Timeout:** `30` (seconds)

**Lưu ý:** Nếu backend chạy trên server khác, đổi Base URL thành IP/domain của server.

---

## 🐛 Troubleshooting

### Lỗi: "GameManager not found"

**Nguyên nhân:** FirstLoading scene chưa được load đầu tiên, hoặc GameManager chưa được setup.

**Giải pháp:**
1. Đảm bảo Build Settings có FirstLoading scene đầu tiên
2. Kiểm tra FirstLoading scene có GameObject `GameManager` với component `GameManager`
3. Chạy từ FirstLoading scene trong Editor

---

### Lỗi: "Backend connection failed"

**Nguyên nhân:** Backend server chưa chạy hoặc Base URL sai.

**Giải pháp:**
1. Kiểm tra backend server đang chạy: `http://localhost:8080`
2. Kiểm tra `BackendConfig.asset` có Base URL đúng
3. Kiểm tra firewall/network settings

---

### Lỗi: "Auto-login not working"

**Nguyên nhân:** Không có token lưu trong PlayerPrefs, hoặc session đã hết hạn.

**Giải pháp:**
1. Đăng nhập lại để lưu token mới
2. Kiểm tra backend session còn valid không
3. Xem logs trong Unity Console

---

### Lỗi: "Lobby not updating"

**Nguyên nhân:** WebSocket connection chưa được thiết lập, hoặc backend chưa broadcast events.

**Giải pháp:**
1. Kiểm tra `RoomWebSocketService` đã connect chưa (xem logs: "Connected to room WebSocket")
2. Kiểm tra backend WebSocket handler đang chạy
3. Kiểm tra `LobbyView` đã subscribe WebSocket events chưa (trong `OnEnable()`)
4. Xem logs trong Unity Console và backend logs
5. **Lưu ý:** Lobby updates chỉ qua WebSocket, **KHÔNG CÒN POLLING**

---

### Lỗi: "Game start not detected"

**Nguyên nhân:** Room status chưa đổi thành `IN_GAME`, hoặc `LobbyView` chưa detect.

**Giải pháp:**
1. Kiểm tra backend đã set `room.status = "IN_GAME"` chưa
2. Kiểm tra `LobbyView` có subscribe `OnRoomStatusChanged` event chưa
3. Xem logs trong Unity Console

---

## 📝 Notes

- **GameManager:** Singleton, tồn tại suốt lifecycle của app (DontDestroyOnLoad)
- **PersistentUICanvas:** Tồn tại suốt lifecycle, chứa Loading, Notice, Reconnect popups
- **SessionState:** Lưu thông tin session (token, userId, username, email)
- **RoomState:** Lưu thông tin room hiện tại (roomId, roomCode, matchId)
- **WebSocket:** Real-time updates cho lobby (**KHÔNG CÒN POLLING**)
  - Kết nối khi join/create room
  - Ngắt kết nối khi leave/disband room
  - Tự động reconnect nếu mất kết nối
- **SessionMonitor:** Vẫn dùng polling để check session status (khác với room updates)
- **SceneLoader:** Quản lý scene transitions, tự động load FirstLoading nếu GameManager chưa init

---

## 🔗 Related Files

- `README.md` - Tổng quan về project
- `Assets/_Night_Hunt/Scripts/Core/GameManager.cs` - Core manager
- `Assets/_Night_Hunt/Scripts/Core/SceneLoader.cs` - Scene management
- `Assets/_Night_Hunt/Scripts/Core/LoadingManager.cs` - Loading screen & auto-login
- `Assets/_Night_Hunt/Scripts/UI/LoginView.cs` - Login UI
- `Assets/_Night_Hunt/Scripts/UI/HomeView.cs` - Home UI
- `Assets/_Night_Hunt/Scripts/UI/LobbyView.cs` - Lobby UI
- `Assets/_Night_Hunt/Scripts/Services/Auth/AuthService.cs` - Authentication
- `Assets/_Night_Hunt/Scripts/Services/Room/RoomService.cs` - Room management
- `Assets/_Night_Hunt/Scripts/Services/Room/RoomWebSocketService.cs` - WebSocket for real-time updates

---

*Last Updated: 2025-12-14*

