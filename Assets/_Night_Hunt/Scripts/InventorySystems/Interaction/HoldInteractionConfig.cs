// using System;
// using UnityEngine;
//
// namespace NightHunt.Inventory.Interaction
// {
//     /// <summary>
//     /// Configuration for hold interaction system.
//     /// </summary>
//     [Serializable]
//     public class HoldInteractionConfig
//     {
//         [Header("Hold Settings")]
//         [Tooltip("Duration required to hold in seconds")]
//         public float holdDuration = 2f;
//         
//         [Tooltip("Maximum distance player can move before cancelling")]
//         public float maxMoveDistance = 1f;
//         
//         [Header("Interrupt Events")]
//         [Tooltip("Cancel hold when player takes damage")]
//         public bool interruptOnDamage = true;
//         
//         [Tooltip("Cancel hold when player moves too far")]
//         public bool interruptOnMoveTooFar = true;
//         
//         [Tooltip("Cancel hold when player releases key")]
//         public bool interruptOnReleaseKey = true;
//     }
// }