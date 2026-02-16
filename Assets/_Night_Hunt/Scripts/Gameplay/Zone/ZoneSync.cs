// using FishNet.Object;
// using FishNet.Object.Synchronizing;
// using UnityEngine;
//
// namespace NightHunt.Gameplay.Zone
// {
//     /// <summary>
//     /// Network sync for zones
//     /// </summary>
//     public class ZoneSync : NetworkBehaviour
//     {
//         private readonly SyncVar<Vector3> networkZoneCenter = new SyncVar<Vector3>();
//         private readonly SyncVar<float> networkZoneRadius = new SyncVar<float>();
//         private readonly SyncVar<bool> networkIsActive = new SyncVar<bool>();
//
//         private ZoneSystem zoneSystem;
//
//         private void Awake()
//         {
//             zoneSystem = GetComponent<ZoneSystem>();
//         }
//
//         public override void OnStartNetwork()
//         {
//             base.OnStartNetwork();
//             networkZoneCenter.OnChange += OnZoneCenterChanged;
//             networkZoneRadius.OnChange += OnZoneRadiusChanged;
//             networkIsActive.OnChange += OnZoneActiveChanged;
//         }
//
//         public override void OnStopNetwork()
//         {
//             base.OnStopNetwork();
//             if (networkZoneCenter != null)
//                 networkZoneCenter.OnChange -= OnZoneCenterChanged;
//             if (networkZoneRadius != null)
//                 networkZoneRadius.OnChange -= OnZoneRadiusChanged;
//             if (networkIsActive != null)
//                 networkIsActive.OnChange -= OnZoneActiveChanged;
//         }
//
//         private void Update()
//         {
//             if (!IsServer || zoneSystem == null) return;
//
//             // Sync zone state
//             networkZoneCenter.Value = zoneSystem.GetZoneCenter();
//             networkZoneRadius.Value = zoneSystem.GetZoneRadius();
//             networkIsActive.Value = zoneSystem.IsActive();
//         }
//
//         private void OnZoneCenterChanged(Vector3 oldCenter, Vector3 newCenter, bool asServer)
//         {
//             if (!asServer && zoneSystem != null)
//             {
//                 zoneSystem.SetZoneCenter(newCenter);
//             }
//         }
//
//         private void OnZoneRadiusChanged(float oldRadius, float newRadius, bool asServer)
//         {
//             if (!asServer && zoneSystem != null)
//             {
//                 zoneSystem.SetZoneRadius(newRadius);
//             }
//         }
//
//         private void OnZoneActiveChanged(bool oldActive, bool newActive, bool asServer)
//         {
//             if (!asServer && zoneSystem != null)
//             {
//                 zoneSystem.SetActive(newActive);
//             }
//         }
//     }
// }
//
