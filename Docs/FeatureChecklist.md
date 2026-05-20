# Feature Checklist

## Settings And Input
- [x] Move `ForceMobileMode` into `GameSettings` and persist it.
- [x] Move mobile camera sensitivity into `GameSettings` and persist it.
- [x] Apply `MouseSensitivity` and mobile camera sensitivity from config on load.
- [x] Update `MobileCameraDragArea` to read live config changes.
- [x] Remove duplicate `forceMobileMode` source fields from UI and gameplay.
- [x] Generate keybinding UI when the scene does not provide `RebindActionUI`.
- [ ] Verify every settings prefab uses the expected child names.
- [ ] Audit every settings tab so only one panel is active at a time.

## Profile And Account
- [x] Show a real profile action block for change password.
- [x] Add a runtime change-password popup.
- [ ] Wire the change-password flow into every profile entry point.
- [ ] Verify password-change success always returns the player to a clean auth state.

## Party And Match Flow
- [x] Reject joining a second party without leaving the current one first.
- [x] Reject inviting a player who is already in another party.
- [x] Reject accepting an invite while already in another party.
- [ ] Rename ambiguous party and mode labels to `RankedPartyMode` and `CustomPartyMode` style names.
- [ ] Separate ranked queue logic from custom-room logic in UI copy and API names.
- [ ] Review every party action for mutual exclusion with room and matchmaking state.

## Backend Validation
- [ ] Confirm party queue cancellation clears all member state on every exit path.
- [ ] Confirm custom room join and leave update player status and room ownership consistently.
- [ ] Confirm ranked match start and end paths never leave stale party state behind.

## Scene Wiring
- [ ] Add the missing settings and input listeners in the relevant scenes and prefabs.
- [ ] Check that profile, settings, and party UI roots are registered in the home scene bootstrap.
- [ ] Verify runtime-generated UI does not overlap existing scene widgets.

