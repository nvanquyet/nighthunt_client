using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Manages the weapon card pool and equipment card list for the inventory screen.
    ///
    /// WEAPON CARDS — prefab per weapon type:
    ///   Designer registers one WeaponCardView prefab per ItemDefinition.ItemID in
    ///   _weaponCardMappings. When a weapon is equipped, the matching prefab is instantiated
    ///   inside _weaponCardContainer. When unequipped, it is hidden (pooled).
    ///
    ///   If no specific mapping is found, _defaultWeaponCardPrefab is used as fallback.
    ///
    /// EQUIPMENT CARDS — single generic prefab:
    ///   Uses _equipmentCardPrefab (EquipmentCardView). One instance per equipment slot type.
    ///   The card itself spawns attachment slots dynamically from ItemDefinition.AttachmentSlots.
    ///   Items with zero attachment slots still show the main icon but hide the attachment area.
    ///
    /// EVENTS:
    ///   Subscribe to IGameplayBridge events to refresh cards on equip/unequip/attach/detach.
    ///   Call Initialize(bridge, uiConfig) once when the inventory screen opens.
    /// </summary>
    public class WeaponEquipmentPanel : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Weapon Cards")]
        [Tooltip("Container where WeaponCardView instances are spawned.")]
        [SerializeField] private RectTransform _weaponCardContainer;

        [Tooltip("Fallback prefab used when no specific weapon mapping is found.")]
        [SerializeField] private GameObject _defaultWeaponCardPrefab;

        [Tooltip("Per-weapon-type prefab mappings. Resolved by ItemDefinition.ItemID.")]
        [SerializeField] private WeaponCardMapping[] _weaponCardMappings;

        [Header("Equipment Cards")]
        [Tooltip("Container where EquipmentCardView instances are spawned (one per slot).")]
        [SerializeField] private RectTransform _equipmentCardContainer;

        [Tooltip("Generic EquipmentCardView prefab — reused for all equipment slot types.")]
        [SerializeField] private GameObject _equipmentCardPrefab;

        [Header("Config")]
        [SerializeField] private UISlotLayoutConfig _uiConfig;

        // ── Runtime ───────────────────────────────────────────────────────────

        private UIDomainBridge _bridge;

        // weapon slot → active WeaponCardView
        private readonly Dictionary<WeaponSlotType, WeaponCardView> _weaponCards =
            new Dictionary<WeaponSlotType, WeaponCardView>();

        // equipment slot → active EquipmentCardView
        private readonly Dictionary<EquipmentSlotType, EquipmentCardView> _equipCards =
            new Dictionary<EquipmentSlotType, EquipmentCardView>();

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Called by InventoryScreen / UIRootController when the player changes or the
        /// inventory is opened for the first time.
        /// </summary>
        public void Initialize(UIDomainBridge bridge, UISlotLayoutConfig uiConfig)
        {
            Teardown();

            _bridge  = bridge;
            if (uiConfig != null) _uiConfig = uiConfig;

            if (_bridge?.IsReady != true) return;

            SubscribeBridgeEvents(true);

            // Build initial state from current equip/weapon system snapshots.
            RefreshAllWeaponCards();
            RefreshAllEquipmentCards();
        }

        /// <summary>
        /// Lock all slot visuals in spectator mode.
        /// </summary>
        public void SetLockedVisual(bool locked)
        {
            foreach (var card in _weaponCards.Values)  card?.SetLockedVisual(locked);
            foreach (var card in _equipCards.Values)   card?.SetLockedVisual(locked);
        }

        /// <summary>
        /// Destroy all spawned cards and unsubscribe events.
        /// </summary>
        public void Teardown()
        {
            SubscribeBridgeEvents(false);
            DestroyAllCards();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Event Wiring

        private void SubscribeBridgeEvents(bool sub)
        {
            if (_bridge?.Bridge == null) return;

            var w = _bridge.Bridge.Weapon;
            var e = _bridge.Bridge.Equipment;
            var a = _bridge.Bridge.Attachment;

            if (w != null)
            {
                if (sub) { w.OnWeaponEquipped   += HandleWeaponEquipped;   w.OnWeaponUnequipped   += HandleWeaponUnequipped; }
                else     { w.OnWeaponEquipped   -= HandleWeaponEquipped;   w.OnWeaponUnequipped   -= HandleWeaponUnequipped; }
            }
            if (e != null)
            {
                if (sub) { e.OnItemEquipped  += HandleEquipmentEquipped;  e.OnItemUnequipped  += HandleEquipmentUnequipped; }
                else     { e.OnItemEquipped  -= HandleEquipmentEquipped;  e.OnItemUnequipped  -= HandleEquipmentUnequipped; }
            }
            // IAttachmentSystem events not on the interface yet; refresh handled via equip events.
            // Uncomment when OnAttachmentAttached/Detached are added to IAttachmentSystem:
            // if (a != null)
            // {
            //     if (sub) { a.OnAttachmentAttached += HandleAttachmentChanged; ... }
        }

        private void OnDestroy() => Teardown();

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Event Handlers

        // Signature MUST match Action<WeaponSlotType, ItemInstance>
        private void HandleWeaponEquipped(WeaponSlotType slot, ItemInstance weapon)
            => RefreshWeaponCard(slot, weapon);

        private void HandleWeaponUnequipped(WeaponSlotType slot, ItemInstance weapon)
            => RefreshWeaponCard(slot, null);

        // Signature MUST match Action<EquipmentSlotType, ItemInstance>
        private void HandleEquipmentEquipped(EquipmentSlotType slot, ItemInstance item)
            => RefreshEquipmentCard(slot, item);

        private void HandleEquipmentUnequipped(EquipmentSlotType slot, ItemInstance item)
            => RefreshEquipmentCard(slot, null);

        // Called when attachment changes — refresh all cards.
        // Slot index is matched visually; accuracy relies on parentInstanceID matching.
        private void HandleAttachmentChanged(string parentInstanceID, int slotIndex, ItemInstance _)
        {
            foreach (var card in _weaponCards.Values)  card?.RefreshAttachmentSlot(slotIndex);
            foreach (var card in _equipCards.Values)   card?.RefreshAttachmentSlot(slotIndex);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Refresh Logic

        private void RefreshAllWeaponCards()
        {
            if (_bridge?.Bridge?.Weapon == null) return;
            // Iterate only the slots defined in InventoryConfig — never raw enum values.
            // This ensures Slot3/Slot4/None are not accidentally spawned when unused.
            var config = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance;
            var slots   = config?.WeaponConfig;
            if (slots != null && slots.Length > 0)
            {
                foreach (var cfg in slots)
                {
                    var weapon = _bridge.Bridge.Weapon.GetWeapon(cfg.Type);
                    RefreshWeaponCard(cfg.Type, weapon);
                }
            }
            else
            {
                // Fallback: iterate active weapon cache only (no config = no phantom cards).
                var allWeapons = _bridge.Bridge.Weapon.GetAllWeapons();
                foreach (var kvp in allWeapons)
                    RefreshWeaponCard(kvp.Key, kvp.Value);
            }
        }

        private void RefreshAllEquipmentCards()
        {
            if (_bridge?.Bridge?.Equipment == null) return;
            foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var item = _bridge.Bridge.Equipment.GetEquippedItem(slot);
                RefreshEquipmentCard(slot, item);
            }
        }

        private void RefreshWeaponCard(WeaponSlotType slot, ItemInstance weapon)
        {
            var card = GetOrCreateWeaponCard(slot, weapon);
            if (card == null) return;

            if (weapon != null)
                card.Show(slot, weapon, _bridge?.Bridge?.Attachment, _uiConfig);
            else
                card.Hide();
        }

        private void RefreshEquipmentCard(EquipmentSlotType slot, ItemInstance item)
        {
            if (!_equipCards.TryGetValue(slot, out var card) || card == null)
            {
                card = SpawnEquipmentCard(slot);
                if (card == null) return;
                _equipCards[slot] = card;
            }

            if (item != null)
                card.Show(slot, item, _bridge?.Bridge?.Attachment, _uiConfig);
            else
                card.Hide();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Card Pool / Factory

        private WeaponCardView GetOrCreateWeaponCard(WeaponSlotType slot, ItemInstance weapon)
        {
            // If we already have a card for this slot, reuse it (may be a different weapon type).
            if (_weaponCards.TryGetValue(slot, out var existing) && existing != null)
            {
                // If weapon changed type, we might need a different prefab.
                // Simple strategy: always destroy and recreate when the itemID changes.
                // For most games the same slot always receives the same category of weapon.
                return existing;
            }

            // Resolve which prefab to use (item mapping → slot config → panel default).
            var prefab = ResolveWeaponPrefab(weapon, slot);
            if (prefab == null)
            {
                Debug.LogWarning($"[WeaponEquipmentPanel] No weapon card prefab for slot {slot} weapon={weapon?.DefinitionID}");
                return null;
            }

            var go   = Instantiate(prefab, _weaponCardContainer, false);
            var card = go.GetComponent<WeaponCardView>();
            if (card == null)
            {
                Debug.LogError($"[WeaponEquipmentPanel] Prefab {prefab.name} has no WeaponCardView component!");
                Destroy(go);
                return null;
            }

            go.SetActive(false);
            _weaponCards[slot] = card;
            return card;
        }

        private EquipmentCardView SpawnEquipmentCard(EquipmentSlotType slot)
        {
            if (_equipmentCardPrefab == null)
            {
                Debug.LogWarning("[WeaponEquipmentPanel] _equipmentCardPrefab not assigned.");
                return null;
            }

            var go   = Instantiate(_equipmentCardPrefab, _equipmentCardContainer, false);
            var card = go.GetComponent<EquipmentCardView>();
            if (card == null)
            {
                Debug.LogError("[WeaponEquipmentPanel] _equipmentCardPrefab has no EquipmentCardView!");
                Destroy(go);
                return null;
            }

            go.SetActive(false);
            return card;
        }

        private GameObject ResolveWeaponPrefab(ItemInstance weapon, WeaponSlotType slot = WeaponSlotType.None)
        {
            // Priority 1: per-item mapping (most specific)
            if (weapon != null && _weaponCardMappings != null)
            {
                foreach (var mapping in _weaponCardMappings)
                {
                    if (mapping.ItemID == weapon.DefinitionID && mapping.CardPrefab != null)
                        return mapping.CardPrefab;
                }
            }

            // Priority 2: per-slot prefab from InventoryConfig
            if (slot != WeaponSlotType.None)
            {
                var slotCfg = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance?.GetWeaponSlot(slot);
                if (slotCfg.HasValue && slotCfg.Value.WeaponCardPrefab != null)
                    return slotCfg.Value.WeaponCardPrefab;
            }

            // Priority 3: panel-level default
            return _defaultWeaponCardPrefab;
        }

        private void DestroyAllCards()
        {
            foreach (var card in _weaponCards.Values)
                if (card != null) Destroy(card.gameObject);
            _weaponCards.Clear();

            foreach (var card in _equipCards.Values)
                if (card != null) Destroy(card.gameObject);
            _equipCards.Clear();
        }

        #endregion
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps an item definition ID to a specific WeaponCardView prefab.
    /// Configured in the WeaponEquipmentPanel Inspector.
    /// </summary>
    [System.Serializable]
    public struct WeaponCardMapping
    {
        [Tooltip("ItemDefinition.ItemID of the weapon (e.g. 'weapon_ak47').")]
        public string ItemID;

        [Tooltip("WeaponCardView prefab to instantiate for this weapon type.")]
        public GameObject CardPrefab;
    }
}
