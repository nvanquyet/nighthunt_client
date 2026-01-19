# Night Hunt - Documentation

## Tổng quan

Tài liệu này cung cấp hướng dẫn đầy đủ để setup, test và phát triển game Night Hunt.

## Tài liệu chính

### 1. [QUICK_START.md](QUICK_START.md)
**Bắt đầu nhanh trong 5 phút**
- Setup cơ bản
- Test local nhanh
- Troubleshooting nhanh

### 2. [SETUP.md](SETUP.md)
**Hướng dẫn setup đầy đủ**
- Dependencies installation
- Scene setup chi tiết
- Component configuration
- Network setup
- Config files setup

### 3. [LOCAL_TESTING_SETUP.md](LOCAL_TESTING_SETUP.md)
**Hướng dẫn test local chi tiết**
- Scene setup từng bước
- Player prefab setup
- Testing workflow
- Testing individual systems
- Debugging guide
- Performance testing

### 4. [GAMEPLAY_IMPLEMENTATION_SUMMARY.md](GAMEPLAY_IMPLEMENTATION_SUMMARY.md)
**Tổng quan implementation**
- Architecture overview
- Systems đã implement
- Design patterns
- SOLID principles
- Network architecture

## Workflow Khuyến nghị

### Lần đầu setup
1. Đọc **QUICK_START.md** để setup nhanh
2. Test local thành công
3. Đọc **SETUP.md** để hiểu chi tiết
4. Đọc **LOCAL_TESTING_SETUP.md** để test đầy đủ

### Phát triển
1. Đọc **GAMEPLAY_IMPLEMENTATION_SUMMARY.md** để hiểu architecture
2. Sử dụng **LOCAL_TESTING_SETUP.md** để test systems
3. Reference **SETUP.md** khi cần config

## Cấu trúc Project

```
NightHuntClient/
├── Assets/
│   ├── _Night_Hunt/
│   │   ├── Scripts/
│   │   │   └── Gameplay/        # Gameplay systems
│   │   ├── Documentation/       # Tài liệu này
│   │   └── StreamingAssets/     # Config files
│   └── Plugins/                 # Third-party plugins
└── Scenes/
    └── TestGameplayScene.unity  # Test scene
```

## Key Systems

### Core Systems
- **Prediction System**: Client-side prediction cho movement/combat
- **State Management**: State machines cho match phases, character states
- **Event System**: Event bus cho decoupled communication
- **Network Sync**: Server authority và client prediction

### Gameplay Systems
- **Character Systems**: Stats, Movement, Combat
- **Match System**: Phases, Objectives, Scoring
- **Predator/Prey**: Dynamic role switching
- **Zone System**: Zone effects và lockdown
- **Loot System**: Phase-based spawning
- **AI System**: Boss AI
- **Anti-Camping**: Camping detection và penalties

### Input & Camera
- **Input System**: Unity New Input System với action maps
- **Camera System**: Cinemachine với 45 độ angle
- **Spectator**: Spectator camera system

## Testing

### Local Testing
- Single instance (host mode)
- Multi-instance (server + client)
- Individual system testing

### Performance Testing
- Profiler setup
- Metrics monitoring
- Optimization guidelines

## Development Tools

### Editor Tools
- **GameplaySceneSetupTool**: Tự động tạo test scene
- Menu: `Night Hunt > Setup > Create Gameplay Test Scene`

## Support

### Troubleshooting
- Xem troubleshooting section trong SETUP.md
- Check console logs
- Verify dependencies

### Common Issues
- Input not working
- Network sync issues
- Camera not following
- Systems not initializing

## Next Steps

1. **Setup**: Follow QUICK_START.md
2. **Test**: Follow LOCAL_TESTING_SETUP.md
3. **Develop**: Reference GAMEPLAY_IMPLEMENTATION_SUMMARY.md
4. **Optimize**: Performance testing và optimization

## Notes

- Tất cả code đã được implement với SOLID principles
- Code sẵn sàng cho production
- Systems có thể hoạt động độc lập
- Dễ dàng mở rộng và maintain

