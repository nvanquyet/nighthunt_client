using UnityEngine;

namespace NightHunt.GameplaySystems.Core.Data
{
    public enum DeployableKind
    {
        Generic = 0,
        VisionNode = 1,
        LightPoint = 2,
        ExplosiveMine = 3,
        ShockField = 4,
    }

    /// <summary>
    /// Definition for placeable items. VisualPrefab is inherited from
    /// PhysicalItemDefinition and must stay pure visual; NetworkDeployablePrefab is
    /// the server-spawned network object used after placement is confirmed.
    /// </summary>
    [CreateAssetMenu(fileName = "Deployable_", menuName = "NightHunt/Items/Deployable Definition")]
    public sealed class DeployableDefinition : PhysicalItemDefinition
    {
        public override ItemType Type => ItemType.Deployable;

        [Header("Deployable")]
        public DeployableKind DeployableKind = DeployableKind.Generic;

        [Tooltip("NetworkObject prefab spawned by the server when placement is confirmed.")]
        public GameObject NetworkDeployablePrefab;

        [Tooltip("Optional placement preview. If empty, VisualPrefab is used.")]
        public GameObject PlacementPreviewPrefab;

        [Min(1)] public int MaxHP = 100;

        [Header("Placement")]
        [Tooltip("Fallback placement distance when AimSystem/VisionRange is unavailable. Runtime placement is clamped by visible range.")]
        [Min(0.25f)] public float PlacementDistance = 3f;
        [Min(0.05f)] public float PlacementCheckRadius = 0.5f;
        public LayerMask PlacementLayerMask = ~0;

        [Range(0f, 60f)]
        public float MaxPlacementSlope = 30f;

        [Tooltip("Yaw offset applied after facing the aim cursor. Use 180 when the prefab model's forward axis points backward.")]
        public float PlacementYawOffsetDegrees = 180f;

        [Header("Deployment Timing")]
        [Tooltip("Seconds after release before the deployable is actually placed. The player can cancel during this time.")]
        [Min(0f)] public float DeployDuration = 1.25f;

        [SerializeField] private bool _canCancelDeploy = true;

        public override bool CanCancelUsage { get => _canCancelDeploy; set => _canCancelDeploy = value; }

        [Header("Vision Ward")]
        [Tooltip("View radius for VisionWard deployables. 0 = use the prefab's default visionRadius value.")]
        [Min(0f)] public float VisionRadius = 0f;
    }
}
