using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.UI;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Manages the weapon card pool and equipment card list for the inventory screen.
    ///
    /// FIXES applied:
    ///   - GetOrCreateWeaponCard now detects weapon type (DefinitionID) changes and
    ///     destroys/recreates the card when the prefab mapping differs, preventing the AR
    ///     card from being reused to display an SMG with a different prefab layout.
    ///   - WeaponCardView.Show(slot, null) keeps the empty weapon slot visible as a drop target.
    /// </summary>
    public class WeaponEquipmentPanel : MonoBehaviour
    {
        private const string LogPrefix = "[INV_FIX][WeaponEquipmentPanel]";

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

        // ── Runtime ───────────────────────────────────────────────────────────

        private UIPlayerContext _bridge;

        // weapon slot → active WeaponCardView
        private readonly Dictionary<WeaponSlotType, WeaponCardView> _weaponCards =
            new Dictionary<WeaponSlotType, WeaponCardView>();

        // FIX: Track which prefab was used to spawn each weapon card so we can detect
        // when the weapon type changes and requires a different prefab layout.
        private readonly Dictionary<WeaponSlotType, GameObject> _weaponCardPrefabs =
            new Dictionary<WeaponSlotType, GameObject>();

        // equipment slot → active EquipmentCardView
        private readonly Dictionary<EquipmentSlotType, EquipmentCardView> _equipCards =
            new Dictionary<EquipmentSlotType, EquipmentCardView>();

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Called by InventoryScreen / UIRootController when the player changes or the
        /// inventory is opened for the first time.
        /// </summary>
        public void Initialize(UIPlayerContext bridge)
        {
            Teardown();

            _bridge = bridge;

            if (_bridge?.IsReady != true) return;

            SubscribeBridgeEvents(true);

            RefreshAllWeaponCards();
            RefreshAllEquipmentCards();
        }

        /// <summary>Lock all slot visuals in spectator mode.</summary>
        public void SetLockedVisual(bool locked)
        {
            foreach (var card in _weaponCards.Values)  card?.SetLockedVisual(locked);
            foreach (var card in _equipCards.Values)   card?.SetLockedVisual(locked);
        }

        /// <summary>Destroy all spawned cards and unsubscribe events.</summary>
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
        }

        private void OnDestroy() => Teardown();

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Event Handlers

        private void HandleWeaponEquipped(WeaponSlotType slot, ItemInstance weapon)
            => RefreshWeaponCard(slot, weapon);

        private void HandleWeaponUnequipped(WeaponSlotType slot, ItemInstance weapon)
            => RefreshWeaponCard(slot, null);

        private void HandleEquipmentEquipped(EquipmentSlotType slot, ItemInstance item)
            => RefreshEquipmentCard(slot, item);

        private void HandleEquipmentUnequipped(EquipmentSlotType slot, ItemInstance item)
            => RefreshEquipmentCard(slot, null);

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
            var slots = new[] { WeaponSlotType.Primary, WeaponSlotType.Secondary, WeaponSlotType.Melee };
            foreach (var slot in slots)
            {
                var weapon = _bridge.Bridge.Weapon.GetWeapon(slot);
                RefreshWeaponCard(slot, weapon);
            }
        }

        private void RefreshAllEquipmentCards()
        {
            if (_bridge?.Bridge?.Equipment == null) return;
            foreach (EquipmentSlotType slot in GetConfiguredEquipmentSlots())
            {
                var item = _bridge.Bridge.Equipment.GetEquippedItem(slot);
                RefreshEquipmentCard(slot, item);
            }
        }

        private static IEnumerable<EquipmentSlotType> GetConfiguredEquipmentSlots()
        {
            var config = InventoryConfig.Instance;
            if (config?.EquipmentConfig != null && config.EquipmentConfig.Length > 0)
            {
                var seen = new HashSet<EquipmentSlotType>();
                foreach (var slotConfig in config.EquipmentConfig)
                {
                    if (seen.Add(slotConfig.Type))
                        yield return slotConfig.Type;
                }
                yield break;
            }

            foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
                yield return slot;
        }

        private void RefreshWeaponCard(WeaponSlotType slot, ItemInstance weapon)
        {
            var card = GetOrCreateWeaponCard(slot, weapon);
            if (card == null) return;

            if (weapon != null)
            {
                card.Show(slot, weapon, _bridge?.Bridge?.Attachment);
                Debug.Log($"{LogPrefix} Weapon card show slot={slot} weapon={weapon.DefinitionID}");
            }
            else
            {
                card.Show(slot, null, _bridge?.Bridge?.Attachment);
                Debug.Log($"{LogPrefix} Weapon card placeholder slot={slot}");
            }
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
            {
                card.Show(slot, item, _bridge?.Bridge?.Attachment);
                Debug.Log($"{LogPrefix} Equipment card show slot={slot} item={item.DefinitionID}");
            }
            else
            {
                card.Show(slot, null, _bridge?.Bridge?.Attachment);
                card.SetExpanded(false);
                Debug.Log($"{LogPrefix} Equipment card placeholder slot={slot}");
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Card Pool / Factory

        private WeaponCardView GetOrCreateWeaponCard(WeaponSlotType slot, ItemInstance weapon)
        {
            // Resolve the prefab we SHOULD use for this weapon.
            var neededPrefab = ResolveWeaponPrefab(weapon, slot);
            if (neededPrefab == null)
            {
                Debug.LogWarning($"[WeaponEquipmentPanel] No weapon card prefab for slot {slot} weapon={weapon?.DefinitionID}");
                return null;
            }

            // FIX: If there is already a card in this slot, check whether it was spawned
            // from the SAME prefab. If the weapon type changed (e.g. pistol → rifle with a
            // different prefab), we must destroy the old card and spawn a fresh one so the
            // pre-placed attachment slot layout matches the new weapon.
            if (_weaponCards.TryGetValue(slot, out var existing) && existing != null)
            {
                bool prefabChanged = !_weaponCardPrefabs.TryGetValue(slot, out var usedPrefab) ||
                                     usedPrefab != neededPrefab;

                if (!prefabChanged)
                {
                    // Same prefab — reuse the existing card.
                    return existing;
                }

                // Different prefab — destroy old card and fall through to spawn a new one.
                Debug.Log($"{LogPrefix} Weapon prefab changed for slot={slot}, rebuilding card.");
                existing.OnCardDoubleClicked -= HandleWeaponCardDoubleClicked;
                Destroy(existing.gameObject);
                _weaponCards.Remove(slot);
                _weaponCardPrefabs.Remove(slot);
            }

            var go   = Instantiate(neededPrefab, _weaponCardContainer, false);
            var card = go.GetComponent<WeaponCardView>();
            if (card == null)
            {
                Debug.LogError($"[WeaponEquipmentPanel] Prefab {neededPrefab.name} has no WeaponCardView component!");
                Destroy(go);
                return null;
            }

            card.OnCardDoubleClicked += HandleWeaponCardDoubleClicked;
            _weaponCards[slot]       = card;
            _weaponCardPrefabs[slot] = neededPrefab;
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
                Debug.LogError($"[WeaponEquipmentPanel] Prefab {_equipmentCardPrefab.name} has no EquipmentCardView component!");
                Destroy(go);
                return null;
            }

            card.OnCardClicked       += HandleEquipmentCardClicked;
            card.OnCardDoubleClicked += HandleEquipmentCardDoubleClicked;

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

            // Fallback: panel-level default
            return _defaultWeaponCardPrefab;
        }

        private void DestroyAllCards()
        {
            foreach (var card in _weaponCards.Values)
            {
                if (card != null)
                {
                    card.OnCardDoubleClicked -= HandleWeaponCardDoubleClicked;
                    Destroy(card.gameObject);
                }
            }
            _weaponCards.Clear();
            _weaponCardPrefabs.Clear();

            foreach (var card in _equipCards.Values)
            {
                if (card != null)
                {
                    card.OnCardClicked       -= HandleEquipmentCardClicked;
                    card.OnCardDoubleClicked -= HandleEquipmentCardDoubleClicked;
                    Destroy(card.gameObject);
                }
            }
            _equipCards.Clear();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Input Logic (Accordion & Unequip)

        private void HandleEquipmentCardClicked(EquipmentCardView clickedCard)
        {
            if (clickedCard == null) return;

            // Accordion logic: Toggle the clicked card, collapse all others.
            foreach (var card in _equipCards.Values)
            {
                if (card == null) continue;

                if (card == clickedCard)
                    card.ToggleExpanded();
                else
                    card.SetExpanded(false);
            }
        }

        private void HandleEquipmentCardDoubleClicked(EquipmentSlotType slot)
        {
            _bridge?.Bridge?.Equipment?.UnequipItem(slot);
        }

        private void HandleWeaponCardDoubleClicked(WeaponSlotType slot)
        {
            _bridge?.Bridge?.Weapon?.UnequipWeapon(slot);
        }

        #endregion
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps an item definition ID to a specific WeaponCardView prefab.
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
