// using System;
// using NightHunt.Inventory.Stats;
// using UnityEngine;
//
// namespace NightHunt.Gameplay.Zone
// {
//     /// <summary>
//     /// Zone configuration data
//     /// </summary>
//     [Serializable]
//     public class ZoneConfig
//     {
//         public string ZoneId;
//         public string ZoneName;
//         public Vector3 Center;
//         public float Radius;
//         public ZoneType Type;
//         public ZoneBuffData[] Buffs;
//         public ZoneNerf[] Nerfs;
//     }
//
//     /// <summary>
//     /// Zone type
//     /// </summary>
//     public enum ZoneType
//     {
//         Safe,
//         Danger,
//         Lockdown,
//         Buff,
//         Debuff
//     }
//
//     /// <summary>
//     /// Zone buff data (for config)
//     /// </summary>
//     [Serializable]
//     public class ZoneBuffData
//     {
//         public string StatName;
//         public float Value;
//         //public ModifierType Type;
//     }
//
//     /// <summary>
//     /// Zone nerf
//     /// </summary>
//     [Serializable]
//     public class ZoneNerf
//     {
//         public string StatName;
//         public float Value;
//         //public ModifierType Type;
//     }
// }
//
