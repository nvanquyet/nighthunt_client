# NightHunt Prediction (FishNet)

Client-side prediction toolkit for FishNet, packaged for reuse. Provides SOLID-friendly abstractions and modules for movement, attack, and interaction with Replicate/Reconcile.

## Requirements
- Unity 2021.3+
- FishNet (com.firstgeargames.fishnet)

## Quick Start
1. Package path (project-local): `Assets/_Night_Hunt/Scripts/Networking/Prediction/Package`.
2. Ensure FishNet Prediction is enabled on your `NetworkManager`.
3. Add a predicted behaviour (movement/attack/interaction) derived from `FishNetPredictedBehaviour` (TickNetworkBehaviour).
4. Collect input each frame, cache it, and let the component send `[Replicate]` during tick; server returns `[Reconcile]`.

## Contents
- `Runtime/`: Core abstractions, buffers, strategies, FishNet adapters.
- `Runtime/Modules/Movement`: Transform-based predicted movement.
- `Runtime/Modules/Attack`: Predicted firing with rollback hooks.
- `Runtime/Modules/Interaction`: Optimistic interaction with server confirmation.
- `Editor/`: Inspectors/debug windows (placeholder).
- `Samples~/`: Minimal usage scripts per module.
- `Documentation~/`: Extended docs and architecture notes.

## Migration Notes
- Keep gameplay glue outside the package; this layer is generic/exportable.
- Shared utilities (history, buffers, reconciliation) live in `Runtime/`.
- Add VFX/SFX/validation in derived behaviours without modifying core; use tick callbacks for network sends.

