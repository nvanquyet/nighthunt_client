using UnityEngine;

namespace NightHunt.GameplaySystems.Core.Configs
{
    /// <summary>
    /// Configuration for lootable/interactable objects
    /// Defines how players interact with WorldItem, WorldContainer, WorldCorpse, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "LootableConfig", menuName = "NightHunt/Gameplay/Lootable Config")]
    public class LootableConfig : ScriptableObject
    {
        [Header("Interaction Settings")]
        [Tooltip("Instant: Bấm 1 phát là loot\nHold: Giữ nút để loot")]
        public LootInteractionMode InteractionMode = LootInteractionMode.Instant;

        [Tooltip("Thời gian giữ nút (seconds) - chỉ dùng khi InteractionMode = Hold")]
        [Min(0.1f)]
        public float HoldDuration = 1.0f;

        [Header("Auto Loot")]
        [Tooltip("Allows auto loot (chỉ áp dụng cho WorldItem với Instant mode)")]
        public bool AllowAutoLoot = true;

        [Header("UI Settings")]
        [Tooltip("Hiển thị prompt text khi có thể interact")]
        public bool ShowPrompt = true;

        [Tooltip("Prompt text display (ví dụ: 'Press E to Open', 'Hold E to Loot')")]
        [TextArea(1, 2)]
        public string PromptText = "Press E to Interact";

        [Header("Distance")]
        [Tooltip("Khoảng cách tối đa để interact (meters)")]
        [Min(0.5f)]
        public float MaxInteractDistance = 3f;

        [Header("Despawn")]
        [Tooltip("Seconds until a lootable object (corpse, container) auto-despawns. 0 = never.")]
        [Min(0f)]
        public float DespawnTime = 300f;
    }
}
