# Project Optimization & Bug Fix Plan

## 1. System & Config Integration
- [ ] Investigate and fix Map/Mode config fetching from server in Home Flow.
- [ ] Populate Settings Tab with missing UI elements and data bindings.
- [ ] Standardize 'Exit' button text to English across all platforms.

## 2. Custom Lobby & Multiplayer Logic
- [ ] Fix UI disappearance in Custom Room when switching from 2v2 to 1v1 while in seat 3.
- [ ] Implement seat index validation and automatic re-seating on mode change.

## 3. UI/UX & Visual Polish
- [ ] Correct position/context when selecting character model prefab in Home View.
- [ ] Redesign and update the 'Profile' view UI.
- [ ] Implement proper UI state clearing (Reset context/buttons when viewing profile or changing scenes).

## 4. Audio & Feedback
- [ ] Wire missing sounds for top menu keys and UI icons.
- [ ] Ensure `NH_Button` and `UIAudioTrigger` are correctly initialized for all interactive elements.

## 5. Environment & Effects
- [ ] Synchronize 2D/3D background effects between Home and Gameplay.
- [ ] Validate audio listener and spatial audio settings across scenes.
