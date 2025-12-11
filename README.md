# NightHunt Unity Client

Unity client for NightHunt multiplayer game.

## 🚀 Quick Start

### Prerequisites
- Unity 2022.3 LTS or newer
- Backend server running (see `../NightHuntServer/README.md`)

### Setup

1. **Open Project**
   - Open Unity Hub
   - Add project: `NightHuntClient`
   - Unity version: 2022.3 LTS or newer

2. **Configure Backend**
   - Navigate to `Assets/_Night_Hunt/Config/BackendConfig.asset`
   - Set `Base URL` to your backend server (e.g., `http://localhost:8080`)

3. **Run**
   - Open scene: `Assets/_Night_Hunt/Scenes/FirstLoading.unity`
   - Press Play

## 📋 Features

### Authentication
- ✅ User Registration
- ✅ Login/Logout
- ✅ Auto-login (Remember me)
- ✅ Multi-device login detection
- ✅ Force logout notification
- ✅ Session expiration handling

### Room Management
- ✅ Create Room (with mode, password, visibility)
- ✅ Join Room (by code with password)
- ✅ Quick Play (find random room or create new)
- ✅ Leave Room
- ✅ Reconnect to Room (with popup)

### Lobby System
- ✅ Player Slots (Team 1 & Team 2)
- ✅ Change Team/Slot
- ✅ Set Ready/Unready
- ✅ Swap Request (Request, Accept, Reject)
- ✅ Real-time Updates (Polling every 1 second)
- ✅ Owner Transfer (Manual via button)
- ✅ Kick Player (Owner only)
- ✅ Disband Room (Owner only)
- ✅ Start Game (Owner only, requires all ready + enough players)
- ✅ Game Start Detection (Auto-detect when game starts)

### UI Components
- ✅ Persistent UI (Loading, Reconnect Popup, Notice Popup)
- ✅ Toast Notifications
- ✅ Error Handling
- ✅ Auto-refresh Lobby

## 🏗️ Project Structure

```
NightHuntClient/
├── Assets/_Night_Hunt/
│   ├── Scripts/
│   │   ├── Core/           # GameManager, SceneLoader
│   │   ├── Services/       # BackendHttpClient, AuthService, RoomService
│   │   ├── State/          # SessionState, RoomState
│   │   ├── UI/             # Views, Popups
│   │   ├── Lobby/          # LobbyController
│   │   ├── Common/         # Constants, ApiResult
│   │   └── Data/           # DTOs
│   ├── Scenes/
│   │   ├── FirstLoading    # Initial scene
│   │   ├── Login           # Login scene
│   │   ├── Home            # Main menu
│   │   └── Waiting         # Lobby scene
│   └── Config/
│       └── BackendConfig    # Backend configuration
└── ProjectSettings/
```

## 🎮 Scene Flow

```
FirstLoading (Initialize services, Check session)
  ↓
  ├─→ Auto-login Success → Home
  └─→ Auto-login Failed → Login
        ↓
      Login Success → Home
        ↓
      Home (Create/Join/Quick Play)
        ↓
      Waiting (Lobby with real-time updates)
        ↓
      Game (When owner starts game)
```

## 🔧 Configuration

### Backend Config
- Location: `Assets/_Night_Hunt/Config/BackendConfig.asset`
- Properties:
  - `Base URL`: Backend server URL (e.g., `http://localhost:8080`)
  - `Timeout`: Request timeout in seconds

### Game Modes
- `2v2`: 4 players (2 per team)
- `3v3`: 6 players (3 per team)
- `5v5`: 10 players (5 per team)

## 📡 API Integration

### Authentication
- `POST /auth/register` - Register
- `POST /auth/login` - Login
- `POST /auth/auto-login` - Auto-login
- `POST /auth/logout` - Logout

### Rooms
- `POST /rooms/create` - Create room
- `POST /rooms/join-by-code` - Join by code
- `POST /rooms/quick-play` - Quick play
- `POST /rooms/reconnect` - Reconnect
- `GET /rooms/{roomId}` - Get room info
- `POST /rooms/{roomId}/ready` - Set ready
- `POST /rooms/{roomId}/change-team` - Change team/slot
- `POST /rooms/{roomId}/leave` - Leave room
- `POST /rooms/{roomId}/start` - Start game
- `POST /rooms/{roomId}/transfer-owner` - Transfer ownership
- And more...

## 🎯 Key Components

### GameManager
- Singleton manager for all services
- Access via `GameManager.Instance`

### SessionState
- Stores authentication state
- Singleton, persists across scenes

### RoomState
- Stores current room state
- Singleton, persists across scenes

### BackendHttpClient
- Handles all HTTP requests
- Automatic session management
- Error handling with popups

### LobbyView
- Main lobby UI
- Auto-refresh every 1 second
- Detects game start automatically

## 🔨 Build

### Client Build
1. File → Build Settings
2. Select platform (Windows/Mac/Linux)
3. Click "Build"

### Build Location
- Default: `Build/Client/`

## 🐛 Troubleshooting

### Backend Connection Error
- Verify backend is running: `http://localhost:8080`
- Check `BackendConfig.asset` has correct Base URL
- Check firewall/network settings

### Auto-login Not Working
- Check `PlayerPrefs` has saved token
- Verify backend session is valid
- Check logs in Unity Console

### Lobby Not Updating
- Check polling is enabled (should refresh every 1s)
- Verify backend is returning correct room data
- Check Unity Console for errors

### Game Start Not Detected
- Verify room status changed to `IN_GAME`
- Check `LobbyView` logs in Unity Console
- Ensure all players are ready and enough players

## 📝 Notes

- **Polling Interval**: 1 second (configurable in `LobbyView`)
- **Session Timeout**: Handled automatically by backend
- **Multi-device**: Force logout when another device logs in
- **Reconnect**: Automatic popup when reconnecting to existing room

## 📝 License

Proprietary - All rights reserved

---

*Last Updated: 2025-12-12*
