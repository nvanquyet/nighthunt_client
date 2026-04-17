using UnityEngine;

namespace NightHunt.GameplaySystems.Core.Configs
{
    /// <summary>
    /// Loại interaction của static world object (Door, Switch, Button, Terminal...).
    /// </summary>
    public enum InteractionType
    {
        Generic,    // Trigger bất kỳ (default)
        Door,       // Mở / Close có animation
        Switch,     // Toggle on/off
        Button,     // One-shot trigger event
        Terminal,   // Mở UI
    }

    /// <summary>
    /// ScriptableObject config cho các static world interactable object
    /// (Door, Switch, Button...) — đặt sẵn trên scene, không spawn dynamic.
    ///
    /// Khác với <see cref="LootableConfig"/> (cho pickup/loot dynamic),
    /// InteractableConfig dùng cho object có state riêng (mở/đóng, bật/tắt...).
    ///
    /// Usage: Create → GameplaySystems → Config → Interactable Config
    ///        → Gán trực tiếp vào component WorldDoor / WorldSwitch / WorldButton.
    /// </summary>
    [CreateAssetMenu(fileName = "InteractableConfig",
                     menuName = "NightHunt/Gameplay/Interactable Config")]
    public class InteractableConfig : ScriptableObject
    {
        [Header("Type")]
        [Tooltip("Loại object — ảnh hưởng animation, sound, và logic trigger.")]
        public InteractionType InteractionType = InteractionType.Generic;

        [Header("Interaction Mode")]
        [Tooltip("Instant: nhấn 1 phát\nHold: giữ nút HoldDuration giây")]
        public LootInteractionMode InteractionMode = LootInteractionMode.Instant;

        [Tooltip("Thời gian giữ nút (giây) — chỉ dùng khi InteractionMode = Hold.")]
        [Min(0.1f)]
        public float HoldDuration = 1.0f;

        [Header("Distance")]
        [Tooltip("Khoảng cách tối đa để interact (meters).")]
        [Min(0.5f)]
        public float MaxInteractDistance = 3f;

        [Header("Restrictions")]
        [Tooltip("Chỉ dùng được 1 lần (Button, bẫy...).")]
        public bool OneTimeUse = false;

        [Tooltip("Yêu cầu player có item nhất định để interact (chìa khóa, thẻ từ...).")]
        public bool RequiresItem = false;

        [Tooltip("ItemID của item cần có — chỉ dùng khi RequiresItem = true.")]
        public string RequiredItemID = "";

        [Header("Auto Reset")]
        [Tooltip("Object tự động reset trạng thái về mặc định sau một khoảng thời gian.\n" +
                 "  false → state giữ nguyên (cửa mở mãi, switch bật mãi...)\n" +
                 "  true  → sau AutoResetDelay giây, trạng thái tự reset (cửa tự đóng, switch tự tắt)")]
        public bool AutoReset = false;

        [Tooltip("Thời gian (giây) before tự reset.\nChỉ dùng khi AutoReset = true.")]
        [Min(0.5f)]
        public float AutoResetDelay = 10f;

        [Header("UI")]
        [Tooltip("Hiển thị prompt trên HUD khi nhắm vào.")]
        public bool ShowPrompt = true;

        [Tooltip("Prompt mặc định khi object có thể tương tác.")]
        [TextArea(1, 2)]
        public string PromptDefault = "[E] Interact";

        [Tooltip("Prompt khi object bị khoá / đã dùng rồi.")]
        [TextArea(1, 2)]
        public string PromptLocked = "[E] Locked";

        [Tooltip("Prompt khi đang giữ nút (Hold mode).")]
        [TextArea(1, 2)]
        public string PromptHolding = "[Hold E] …";
    }
}
