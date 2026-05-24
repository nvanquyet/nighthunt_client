using System;
using System.Collections.Generic;
using NightHunt.Data.DTOs;
using NightHunt.State;
using UnityEngine;

namespace NightHunt.UI
{
    /// <summary>
    /// PartyModelListView - center party model display on the Home screen.
    ///
    /// Spawns exactly <c>members.Count</c> <see cref="PartyModelSlotView"/> GameObjects —
    /// one per actual party member. There are NO empty/placeholder slots; the list
    /// grows when a member joins and shrinks when one leaves.
    ///
    /// When a slot is clicked the view calls
    /// <see cref="SharedPartyContextMenu.Show"/> on the shared context-menu
    /// instance (a single GO placed as the last sibling of the Home panel root).
    ///
    /// Call <see cref="Refresh"/> every time the party or game mode changes.
    ///
    /// SETUP (Prefab hierarchy):
    ///   PartyModelList (this script)
    ///   +-- Container  <- HorizontalLayoutGroup, set in Inspector as <c>container</c>
    ///       +-- (PartyModelSlot prefab clones spawned at runtime)
    /// </summary>
    public class PartyModelListView : MonoBehaviour
    {
        [Header("Spawning")]
        [Tooltip("Prefab with PartyModelSlotView component on root or child.")]
        [SerializeField] private GameObject slotPrefab;

        [Tooltip("Parent transform for spawned slots (HorizontalLayoutGroup recommended).")]
        [SerializeField] private Transform container;

        [Header("Shared Context Menu")]
        [Tooltip("Assign the SharedPartyContextMenu instance that lives as last sibling of the Home panel root.")]
        [SerializeField] private SharedPartyContextMenu sharedContextMenu;

        // -- Runtime ----------------------------------------------------------

        private readonly List<PartyModelSlotView> _slots = new();
        private Action<long> _onKick;
        private Action       _onLeave;

        // -- Public API -------------------------------------------------------

        /// <summary>
        /// Rebuild the center model display.
        /// Only real members are shown — no empty/placeholder slots.
        /// </summary>
        /// <param name="party">Current party, or null if solo.</param>
        /// <param name="iAmHost">True if the local player is the party host.</param>
        /// <param name="onKick">Fires with userId when host kicks a member.</param>
        /// <param name="onLeave">Fires when local player taps Leave.</param>
        public void Refresh(PartyResponse party,
                            bool         iAmHost = false,
                            Action<long> onKick  = null,
                            Action       onLeave = null)
        {
            _onKick  = onKick;
            _onLeave = onLeave;

            // Sort members by joinOrder so slot 0 = host
            var members = new List<PartyMemberResponse>();
            if (party?.members != null && party.members.Count > 0)
            {
                members.AddRange(party.members);
                members.Sort((a, b) => a.joinOrder.CompareTo(b.joinOrder));
            }
            else
            {
                // Solo (no party) — always show the local player so the model area is never empty.
                var session = SessionState.Instance;
                var selfSlot = new PartyMemberResponse
                {
                    userId              = session?.UserId ?? 0L,
                    username            = session?.Username ?? "Me",
                    isHost              = true,
                    joinOrder           = 0,
                    onlineStatus        = "ONLINE",
                    selectedCharacterId = session?.SelectedCharacterId ?? "",
                };
                members.Add(selfSlot);
                iAmHost = true; // solo player is always "host"
                Debug.Log($"[PartyModelListView] Solo mode — injecting local player slot: userId={selfSlot.userId} username={selfSlot.username} charId={selfSlot.selectedCharacterId}");
            }
            // Grow / shrink to exactly members.Count — no empty slots
            Debug.Log($"[PartyModelListView] EnsureSlotCount({members.Count}) _slots.Count={_slots.Count}");
            EnsureSlotCount(members.Count);
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetMember(members[i], iAmHost, OnModelSlotClicked);
        }

        // -- Private ----------------------------------------------------------

        private void OnModelSlotClicked(PartyModelSlotView slot)
        {
            if (slot == null) return;

            var anchor     = slot.GetComponent<RectTransform>();
            bool showKick  = slot.IAmHost && !slot.IsLocalPlayer;
            bool showLeave = slot.IsLocalPlayer;

            sharedContextMenu?.Show(anchor, showKick, showLeave,
                                    slot.MemberId, _onKick, _onLeave);
        }

        private void EnsureSlotCount(int count)
        {
            // Remove excess
            while (_slots.Count > count)
            {
                var last = _slots[_slots.Count - 1];
                _slots.RemoveAt(_slots.Count - 1);
                if (last != null) Destroy(last.gameObject);
            }

            // Add missing
            while (_slots.Count < count)
            {
                if (slotPrefab == null || container == null) break;

                // slotPrefab may be a scene object whose parent is inactive (used as a
                // hidden template). Temporarily activate the parent chain so Unity can
                // Instantiate it, then restore the original state afterwards.

                var go = Instantiate(slotPrefab, container);
                go.gameObject.SetActive(true); // ensure clone is always active regardless of template state


                var slot = go.GetComponentInChildren<PartyModelSlotView>(includeInactive: true)
                           ?? go.GetComponent<PartyModelSlotView>();

                if (slot == null)
                {
                    Destroy(go);
                    break;
                }

                slot.SetEmpty();
                _slots.Add(slot);
            }
        }

        private void OnDestroy()
        {
            sharedContextMenu?.Hide();
            foreach (var s in _slots)
                if (s != null) Destroy(s.gameObject);
            _slots.Clear();
        }

#if UNITY_EDITOR
        // ── Editor — Context Menu: Create PartyModelSlot Template Prefab ────

        [ContextMenu("NightHunt/Create PartyModelSlot Template Prefab")]
        private void Editor_CreatePartyModelSlotPrefab()
        {
            const string parent = "Assets/_Night_Hunt/Prefabs";
            const string dir    = parent + "/UI";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder(parent, "UI");

            const string path = dir + "/PartyModelSlot_Template.prefab";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyModelListView] PartyModelSlot_Template already exists at {path}");
                return;
            }

            // Root: slot container (100 × 160)
            var go  = new GameObject("PartyModelSlot_Template");
            var rt  = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 160f);

            // Model area background (top 120 px)
            var modelArea = new GameObject("ModelArea", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            modelArea.transform.SetParent(go.transform, false);
            var maRt = modelArea.GetComponent<RectTransform>();
            maRt.anchorMin = new Vector2(0f, 0.25f);
            maRt.anchorMax = Vector2.one;
            maRt.offsetMin = maRt.offsetMax = Vector2.zero;
            modelArea.GetComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.6f);

            // Name label (bottom 40 px)
            var nameLabelGo = new GameObject("NameText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            nameLabelGo.transform.SetParent(go.transform, false);
            var nlRt = nameLabelGo.GetComponent<RectTransform>();
            nlRt.anchorMin = Vector2.zero;
            nlRt.anchorMax = new Vector2(1f, 0.25f);
            nlRt.offsetMin = nlRt.offsetMax = Vector2.zero;
            var nlTmp = nameLabelGo.GetComponent<TMPro.TextMeshProUGUI>();
            nlTmp.text = "Player";
            nlTmp.alignment = TMPro.TextAlignmentOptions.Center;
            nlTmp.fontSize  = 14f;

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);

            if (slotPrefab == null)
            {
                slotPrefab = saved;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            Debug.Log($"[PartyModelListView] Created PartyModelSlot_Template at {path}. " +
                      "Add the PartyModelSlotView component to the root and wire up NameText.");
        }
#endif
    }
}
