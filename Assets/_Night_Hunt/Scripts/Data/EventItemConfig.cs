using System;

namespace NightHunt.Data
{
    /// <summary>
    /// Event item configuration
    /// Extends BaseItemConfig with event-specific fields
    /// </summary>
    [Serializable]
    public class EventItemConfig : BaseItemConfig
    {
        public string EventId;
        public bool CannotDrop; // Override CanDrop = false
        public string ObjectiveText;

        public EventItemConfig()
        {
            Type = ItemType.EventItem;
            CanDrop = false;
            CannotDrop = true;
        }
    }
}

