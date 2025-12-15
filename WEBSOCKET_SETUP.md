# WebSocket Setup Guide

Hướng dẫn cài đặt NativeWebSocket cho Unity client.

---

## ✅ Đã thực hiện

1. ✅ Đã thêm NativeWebSocket vào `Packages/manifest.json`
2. ✅ Đã implement WebSocket connection trong `RoomWebSocketService.cs`

---

## 📦 Cài đặt NativeWebSocket

### Cách 1: Tự động (Đã thêm vào manifest.json)

1. **Mở Unity Editor**
2. **Đợi Unity tự động import package** (có thể mất vài phút)
3. **Kiểm tra Package Manager:**
   - Window → Package Manager
   - Chọn "In Project" tab
   - Tìm "Native WebSocket" hoặc "com.endel.nativewebsocket"
   - Nếu thấy package → ✅ Đã cài đặt thành công

### Cách 2: Thủ công (Nếu cách 1 không hoạt động)

1. **Mở Unity Editor**
2. **Window → Package Manager**
3. **Click dấu + (góc trên bên trái) → Add package from git URL...**
4. **Nhập URL:**
   ```
   https://github.com/endel/NativeWebSocket.git#upm
   ```
5. **Click "Add"**
6. **Đợi Unity import package**

### Cách 3: Download từ GitHub (Nếu cách 1 và 2 không hoạt động)

1. **Tải từ GitHub:**
   - URL: https://github.com/endel/NativeWebSocket
   - Click "Code" → "Download ZIP"

2. **Giải nén và copy:**
   - Copy thư mục `NativeWebSocket/Assets/WebSocket` 
   - Paste vào `NightHuntClient/Assets/Plugins/WebSocket`

3. **Refresh Unity:**
   - Unity sẽ tự động import

---

## ⚙️ Cấu hình WebSocket URL

Sau khi cài đặt, cần cấu hình WebSocket URL:

1. **Tìm GameObject có `RoomWebSocketService` component**
2. **Trong Inspector, set `Ws Base Url`:**
   - **Development:** `ws://localhost:8080` (KHÔNG có `/ws` ở cuối)
   - **Production:** `wss://your-domain.com` (WSS cho secure connection, KHÔNG có `/ws` ở cuối)

**Lưu ý:**
- Backend WebSocket endpoint: `/ws/room/{roomId}?token={token}`
- Client sẽ tự động build URL: `{wsBaseUrl}/ws/room/{roomId}?token={token}`
- **Ví dụ:** `ws://localhost:8080` → `ws://localhost:8080/ws/room/123?token=abc...`

---

## 🔍 Kiểm tra cài đặt

### 1. Kiểm tra Package đã import

**Unity Console:**
- Không còn lỗi: `The type or namespace name 'NativeWebSocket' could not be found`
- ✅ Nếu không có lỗi → Package đã được import

**Package Manager:**
- Window → Package Manager → In Project
- Tìm "Native WebSocket" hoặc "com.endel.nativewebsocket"
- ✅ Nếu thấy → Package đã được cài đặt

### 2. Kiểm tra Code

**File:** `Assets/_Night_Hunt/Scripts/Services/Room/RoomWebSocketService.cs`

**Kiểm tra:**
- ✅ Có `using NativeWebSocket;` ở đầu file
- ✅ Có `private WebSocket webSocket;` field
- ✅ Có `Update()` method với `webSocket.DispatchMessageQueue()`
- ✅ `ConnectWebSocket()` method đã implement với NativeWebSocket

### 3. Test WebSocket Connection

1. **Chạy Backend server** (port 8080)
2. **Chạy Unity client**
3. **Login và join room**
4. **Kiểm tra Unity Console:**
   - ✅ `[RoomWebSocketService] Connecting to room {roomId}...`
   - ✅ `[RoomWebSocketService] WebSocket opened`
   - ✅ `[RoomWebSocketService] Connected successfully`

--- 

## 🐛 Troubleshooting

### Lỗi: "The type or namespace name 'NativeWebSocket' could not be found"

**Nguyên nhân:** Package chưa được Unity import

**Giải pháp:**
1. **Refresh Package Manager:**
   - Window → Package Manager
   - Click "Refresh" hoặc đợi Unity tự động refresh

2. **Kiểm tra manifest.json:**
   - Đảm bảo có dòng: `"com.endel.nativewebsocket": "https://github.com/endel/NativeWebSocket.git#upm"`

3. **Restart Unity Editor:**
   - Đóng Unity
   - Mở lại project
   - Đợi Unity import packages

4. **Nếu vẫn lỗi:**
   - Thử Cách 2 hoặc Cách 3 ở trên

---

### Lỗi: "WebSocket connection failed"

**Nguyên nhân:** Backend chưa chạy hoặc URL sai

**Giải pháp:**
1. **Kiểm tra Backend đang chạy:**
   ```bash
   curl http://localhost:8080/actuator/health
   ```

2. **Kiểm tra WebSocket URL:**
   - Trong Unity Inspector, kiểm tra `Ws Base Url` của `RoomWebSocketService`
   - Phải là: `ws://localhost:8080/ws` (development)

3. **Kiểm tra Backend WebSocket endpoint:**
   - Backend endpoint: `/ws/room/{roomId}?token={token}`
   - Full URL sẽ là: `ws://localhost:8080/ws/room/{roomId}?token={token}`

---

### Lỗi: "WebSocket closed" ngay sau khi connect

**Nguyên nhân:** Backend WebSocket handler chưa được config đúng

**Giải pháp:**
1. **Kiểm tra Backend logs:**
   ```bash
   docker-compose logs backend | grep WebSocket
   ```

2. **Kiểm tra Backend WebSocket config:**
   - File: `NightHuntServer/src/main/java/com/nighthunt/room/websocket/WebSocketConfig.java`
   - Endpoint phải là: `/ws/room/{roomId}`

3. **Kiểm tra Authentication:**
   - Token phải được gửi trong query string: `?token={token}`
   - Token phải valid và chưa hết hạn

---

## 📝 Notes

- **NativeWebSocket** hỗ trợ:
  - ✅ WebGL
  - ✅ Windows/Mac/Linux
  - ✅ Android/iOS
  - ✅ UWP

- **DispatchMessageQueue():**
  - Phải gọi trong `Update()` method
  - Chỉ cần cho non-WebGL platforms
  - Code đã có `#if !UNITY_WEBGL || UNITY_EDITOR` check

- **WebSocket URL:**
  - Development: `ws://localhost:8080/ws`
  - Production: `wss://your-domain.com/ws` (WSS = secure)

---

## 🔗 Links

- **NativeWebSocket GitHub:** https://github.com/endel/NativeWebSocket
- **Documentation:** https://github.com/endel/NativeWebSocket#readme

---

*Last Updated: 2025-12-14*

