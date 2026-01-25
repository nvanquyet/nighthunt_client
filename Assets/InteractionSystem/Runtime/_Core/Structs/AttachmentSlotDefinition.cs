using System;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core
{
    [Serializable]
    public struct AttachmentSlotDefinition
    {
        public AttachmentSlotType slotType;
        public AttachmentType[] acceptedTypes;
        public Vector3 attachmentPointOffset;
        public Quaternion attachmentPointRotation;
        public bool isRequired; // E.g., magazine slot
    }

    public enum AttachmentSlotType
    {
        // Weapon
        Scope,
        Barrel,
        Magazine,
        Grip,
        Stock,

        // Armor
        PlateCarrier,
        Pouch1,
        Pouch2,
        Pouch3,

        // Helmet
        NightVision,
        Flashlight,
        Camera,

        // Backpack
        Hydration,
        ExternalPouch
    }

    public enum AttachmentType
    {
        Optic,
        Suppressor,
        Magazine,
        Foregrip,
        Stock,
        Plate,
        Pouch,
        NVG,
        Tactical,
        Camera,
        Hydration
    }
}