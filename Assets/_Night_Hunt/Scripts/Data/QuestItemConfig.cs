using System;

namespace NightHunt.Data
{
    /// <summary>
    /// Quest item configuration
    /// Extends BaseItemConfig with quest-specific fields
    /// </summary>
    [Serializable]
    public class QuestItemConfig : BaseItemConfig
    {
        public string QuestId;
        public bool CannotDrop; // Override CanDrop = false
        public string ObjectiveText;

        public QuestItemConfig()
        {
            Type = ItemType.QuestItem;
            CanDrop = false;
            CannotDrop = true;
        }
    }
}

