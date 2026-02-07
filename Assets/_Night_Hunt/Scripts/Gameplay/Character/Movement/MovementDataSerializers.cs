// using FishNet.Serializing;
// using NightHunt.Gameplay.Character.Movement;
// using UnityEngine;
//
// namespace _Night_Hunt.Scripts.Gameplay.Character.Movement
// {
//     /// <summary>
//     /// ✅ CRITICAL: Custom serialization cho FishNet
//     /// Phải có class này để FishNet biết cách serialize/deserialize
//     /// </summary>
//     public static class MovementDataSerializers
//     {
//         // ===== REPLICATE DATA SERIALIZATION =====
//         
//         public static void WriteMovementReplicateData(this Writer writer, MovementReplicateData value)
//         {
//             writer.WriteVector2(value.Move);
//             writer.WriteSingle(value.Yaw);
//             writer.WriteBoolean(value.Sprint);
//             writer.WriteBoolean(value.Crouch);
//             writer.WriteBoolean(value.CameraLocked);
//         }
//
//         public static MovementReplicateData ReadMovementReplicateData(this Reader reader)
//         {
//             Vector2 move = reader.ReadVector2();
//             float yaw = reader.ReadSingle();
//             bool sprint = reader.ReadBoolean();
//             bool crouch = reader.ReadBoolean();
//             bool cameraLocked = reader.ReadBoolean();
//
//             return new MovementReplicateData(move, yaw, sprint, crouch, cameraLocked);
//         }
//
//         // ===== RECONCILE DATA SERIALIZATION =====
//         
//         public static void WriteMovementReconcileData(this Writer writer, MovementReconcileData value)
//         {
//             writer.WriteVector3(value.Position);
//             writer.WriteQuaternion(value.Rotation);
//             writer.WriteVector3(value.Velocity);
//             writer.WriteSingle(value.Stamina);
//         }
//
//         public static MovementReconcileData ReadMovementReconcileData(this Reader reader)
//         {
//             Vector3 position = reader.ReadVector3();
//             Quaternion rotation = reader.ReadQuaternion();
//             Vector3 velocity = reader.ReadVector3();
//             float stamina = reader.ReadSingle();
//
//             return new MovementReconcileData(position, rotation, velocity, stamina);
//         }
//     }
// }