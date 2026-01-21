using System;
using System.Collections.Generic;

namespace NightHunt.Data
{
    /// <summary>
    /// Attachment item configuration (for weapon attachments)
    /// Extends BaseItemConfig with attachment-specific fields
    /// </summary>
    [Serializable]
    public class AttachmentItemConfig : BaseItemConfig
    {
        public string SocketType; // VD: "ScopeSlot", "BarrelSlot"
        public string[] CompatibleWeaponCategories; // VD: ["Rifle", "SMG"]
        public Dictionary<string, float> WeaponModifiers; // VD: {"Damage": 0.15f, "Accuracy": 0.2f}

        public AttachmentItemConfig()
        {
            Type = ItemType.Attachment;
            WeaponModifiers = new Dictionary<string, float>();
        }
    }
}

