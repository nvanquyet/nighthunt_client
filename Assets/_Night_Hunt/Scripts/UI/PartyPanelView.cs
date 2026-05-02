using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Party;
using NightHunt.Services.Friend;
using NightHunt.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Party Panel View— displays party members, invites, and party actions.
    /// 
    /// Features:
    /// - Create/leave party
    /// - Invite friends to party
    /// - Accept/reject party invites
    /// - Kick members (leader only)
    /// - Transfer leadership (leader only)
    /// - Queue party for matchmaking
    /// - Join custom lobby with party
    /// </summary>
    public class PartyPanelView : MonoBehaviour
    {
        // ── Panel References ──────────────────────────────────────────────────
        [Header("Panels")]
        [SerializeField] private GameObject noPartyPanel;
        [SerializeField] private GameObject activePartyPanel;
        [SerializeField] private GameObject inviteListPanel;

        // ── No Party Panel ────────────────────────────────────────────────────
        [Header("No Party Panel")]
        [SerializeField] private Button createPartyButton;
        [SerializeField] private Transform pendingInvitesContainer;
        [SerializeField] private GameObject inviteItemPrefab;

        // ── Active Party Panel ────────────────────────────────────────────────
        [Header("Active Party Panel")]
        [SerializeField] private Transform partyMembersContainer;
        [SerializeField] private GameObject memberItemPrefab;
        [SerializeField] private TextMeshProUGUI partyStatusText;
        [SerializeField] private Button leavePartyButton;
        [SerializeField] private Button inviteFriendButton;
        [SerializeField] private Button refreshPartyButton;

        // ── Leader Actions ────────────────────────────────────────────────────
        [Header("Leader Actions")]
        [SerializeField] private GameObject leaderActionsPanel;
        [SerializeField] private Button queueMatchmakingButton;
        [SerializeField] private Button cancelQueueButton;
        [SerializeField] private Button joinCustomLobbyButton;

        // ── Invite Friend Dialog ──────────────────────────────────────────────
        [Header("Invite Friend Dialog")]
        [SerializeField] private GameObject inviteFriendDialog;
        [SerializeField] private Transform friendListContainer;
        [SerializeField] private GameObject friendSelectItemPrefab;
        [SerializeField] private Button closeInviteDialogButton;

        // ── Game Mode Selection ───────────────────────────────────────────────
        [Header("Game Mode Selection")]
        [SerializeField] private GameModeSelectionView gameModeSelectionView;

        [Header("Room Code Dialog")]
        [SerializeField] private GameObject roomCodeDialog;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button confirmJoinRoomButton;
        [SerializeField] private Button cancelJoinRoomButton;

        // ── Services ──────────────────────────────────────────────────────────
        private PartyService _partyService;
        private FriendService _friendService;

        // ── State ─────────────────────────────────────────────────────────────
        private PartyResponse _currentParty;
        private bool _isLeader;

        // ──────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                _partyService = GameManager.Instance.PartyService;
                _friendService = GameManager.Instance.FriendService;
            }

            // No party panel
            if (createPartyButton != null) createPartyButton.onClick.AddListener(OnCreatePartyClicked);

            // Active party panel
            if (leavePartyButton != null) leavePartyButton.onClick.AddListener(OnLeavePartyClicked);
            if (inviteFriendButton != null) inviteFriendButton.onClick.AddListener(OnInviteFriendClicked);
            if (refreshPartyButton != null) refreshPartyButton.onClick.AddListener(OnRefreshPartyClicked);

            // Leader actions
            if (queueMatchmakingButton != null) queueMatchmakingButton.onClick.AddListener(OnQueueMatchmakingClicked);
            if (cancelQueueButton != null) cancelQueueButton.onClick.AddListener(OnCancelQueueClicked);
            if (joinCustomLobbyButton != null) joinCustomLobbyButton.onClick.AddListener(OnJoinCustomLobbyClicked);

            // Dialogs
            if (closeInviteDialogButton != null) closeInviteDialogButton.onClick.AddListener(() => inviteFriendDialog?.SetActive(false));
            if (confirmJoinRoomButton != null) confirmJoinRoomButton.onClick.AddListener(OnConfirmJoinRoomClicked);
            if (cancelJoinRoomButton != null) cancelJoinRoomButton.onClick.AddListener(() => roomCodeDialog?.SetActive(false));

            // Hide dialogs by default
            if (inviteFriendDialog != null) inviteFriendDialog.SetActive(false);
            if (roomCodeDialog != null) roomCodeDialog.SetActive(false);
        }

        private void Start()
        {
            LoadParty();
            LoadPendingInvites();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Party Management

        private async void LoadParty()
        {
            if (_partyService == null) return;

            var result = await _partyService.GetParty();
            if (result.Success && result.Data != null)
            {
                _currentParty = result.Data;
                _isLeader = _currentParty.hostUserId == GameManager.Instance?.SessionState?.UserId;
                DisplayParty(_currentParty);
                ShowPartyPanel(true);
            }
            else
            {
                _currentParty = null;
                _isLeader = false;
                ShowPartyPanel(false);
            }
        }

        private void ShowPartyPanel(bool hasParty)
        {
            if (noPartyPanel != null) noPartyPanel.SetActive(!hasParty);
            if (activePartyPanel != null) activePartyPanel.SetActive(hasParty);
        }

        private void DisplayParty(PartyResponse party)
        {
            // Update status text
            if (partyStatusText != null)
            {
                partyStatusText.text = $"Status: {party.partyStatus} | Members: {party.members.Count}/{party.maxMembers}";
            }

            // Show/hide leader actions
            if (leaderActionsPanel != null)
            {
                leaderActionsPanel.SetActive(_isLeader);
            }

            // Show/hide queue buttons based on status
            if (queueMatchmakingButton != null) queueMatchmakingButton.gameObject.SetActive(party.partyStatus == "IDLE");
            if (cancelQueueButton != null) cancelQueueButton.gameObject.SetActive(party.partyStatus == "IN_QUEUE");

            // Clear existing member items
            foreach (Transform child in partyMembersContainer)
            {
                Destroy(child.gameObject);
            }

            // Create new member items
            foreach (var member in party.members)
            {
                var item = Instantiate(memberItemPrefab, partyMembersContainer);
                var itemView = item.GetComponent<PartyMemberItemView>();
                if (itemView != null)
                {
                    itemView.Setup(member, _isLeader, OnKickMember, OnTransferLeader);
                }
            }
        }

        private async void OnCreatePartyClicked()
        {
            if (_partyService == null) return;

            var result = await _partyService.CreateParty();
            if (result.Success && result.Data != null)
            {
                Debug.Log("[PartyPanel] Party created successfully");
                LoadParty(); // Refresh
            }
            else
            {
                Debug.LogError($"[PartyPanel] Failed to create party: {result.Message}");
            }
        }

        private void OnLeavePartyClicked()
        {
            GameModalWindow.Instance?.ShowConfirm(
                "Leave Party?",
                "Bạn có chắc muốn rời khỏi party?",
                onConfirm: async () =>
                {
                    if (_partyService == null) return;
                    var result = await _partyService.LeaveParty();
                    if (result.Success)
                    {
                        Debug.Log("[PartyPanel] Left party successfully");
                        LoadParty();
                    }
                    else
                    {
                        Debug.LogError($"[PartyPanel] Failed to leave party: {result.Message}");
                    }
                },
                confirmText: "Leave Party", cancelText: "Cancel");
        }

        private void OnRefreshPartyClicked()
        {
            LoadParty();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Party Member Actions

        private async void OnKickMember(long memberId)
        {
            if (_partyService == null) return;

            var result = await _partyService.KickMember(memberId);
            if (result.Success)
            {
                Debug.Log($"[PartyPanel] Member kicked: {memberId}");
                LoadParty(); // Refresh
            }
            else
            {
                Debug.LogError($"[PartyPanel] Failed to kick member: {result.Message}");
            }
        }

        private async void OnTransferLeader(long memberId)
        {
            if (_partyService == null) return;
            var result = await _partyService.TransferLeader(memberId);
            if (result.Success)
            {
                ConditionalLogger.Log("PartyPanel", $"Leader transferred to userId={memberId}");
                LoadParty(); // Refresh party view
            }
            else
            {
                ConditionalLogger.LogError("PartyPanel", $"TransferLeader failed: {result.Message}");
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Party Invites

        private async void LoadPendingInvites()
        {
            if (_partyService == null) return;

            var result = await _partyService.GetPendingInvitations();
            if (result.Success && result.Data != null)
            {
                DisplayPendingInvites(result.Data);
            }
        }

        private void DisplayPendingInvites(List<PartyInviteResponse> invites)
        {
            // Clear existing items
            foreach (Transform child in pendingInvitesContainer)
            {
                Destroy(child.gameObject);
            }

            // Create new items (only show PENDING invites)
            var pendingInvites = invites.FindAll(inv => inv.invitationStatus == "PENDING");
            foreach (var invite in pendingInvites)
            {
                var item = Instantiate(inviteItemPrefab, pendingInvitesContainer);
                var itemView = item.GetComponent<PartyInviteItemView>();
                if (itemView != null)
                {
                    itemView.Setup(invite, OnAcceptInvite, OnRejectInvite);
                }
            }
        }

        private async void OnAcceptInvite(long inviteId)
        {
            if (_partyService == null) return;

            var result = await _partyService.AcceptInvitation(inviteId);
            if (result.Success)
            {
                Debug.Log("[PartyPanel] Party invite accepted");
                LoadParty(); // Refresh (will show active party)
                LoadPendingInvites(); // Refresh invites
            }
            else
            {
                Debug.LogError($"[PartyPanel] Failed to accept invite: {result.Message}");
            }
        }

        private async void OnRejectInvite(long inviteId)
        {
            if (_partyService == null) return;

            var result = await _partyService.DeclineInvitation(inviteId);
            if (result.Success)
            {
                Debug.Log("[PartyPanel] Party invite rejected");
                LoadPendingInvites(); // Refresh invites
            }
            else
            {
                Debug.LogError($"[PartyPanel] Failed to reject invite: {result.Message}");
            }
        }

        private async void OnInviteFriendClicked()
        {
            if (_friendService == null || inviteFriendDialog == null) return;

            // Load friend list
            var result = await _friendService.GetFriends();
            if (result.Success && result.Data != null)
            {
                DisplayFriendList(result.Data);
                inviteFriendDialog.SetActive(true);
            }
            else
            {
                Debug.LogError($"[PartyPanel] Failed to load friends: {result.Message}");
            }
        }

        private void DisplayFriendList(List<FriendResponse> friends)
        {
            // Clear existing items
            foreach (Transform child in friendListContainer)
            {
                Destroy(child.gameObject);
            }

            // Create new items
            foreach (var friend in friends)
            {
                var item = Instantiate(friendSelectItemPrefab, friendListContainer);
                var itemView = item.GetComponent<FriendSelectItemView>();
                if (itemView != null)
                {
                    itemView.Setup(friend, OnInviteFriendSelected);
                }
            }
        }

        private async void OnInviteFriendSelected(long friendId)
        {
            if (_partyService == null) return;

            var result = await _partyService.InviteToParty(friendId);
            if (result.Success)
            {
                Debug.Log($"[PartyPanel] Friend invited: {friendId}");
                if (inviteFriendDialog != null) inviteFriendDialog.SetActive(false);
            }
            else
            {
                Debug.LogError($"[PartyPanel] Failed to invite friend: {result.Message}");
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Party Matchmaking

        private async void OnQueueMatchmakingClicked()
        {
            if (_partyService == null || _currentParty == null) return;

            // Show game mode selection dialog for party
            if (gameModeSelectionView != null)
            {
                int partySize = _currentParty.members?.Count ?? 1;
                gameModeSelectionView.ShowForParty(partySize, OnGameModeSelected);
            }
            else
            {
                Debug.LogWarning("[PartyPanel] GameModeSelectionView not assigned, using default mode");
                await QueuePartyForMode("2v2", true);
            }
        }

        private async void OnGameModeSelected(string gameMode, bool allowFill)
        {
            await QueuePartyForMode(gameMode, allowFill);
        }

        private async Task QueuePartyForMode(string gameMode, bool allowFill)
        {
            if (_partyService == null) return;

            var result = await _partyService.QueueParty(gameMode, allowFill);
            if (result.Success)
            {
                Debug.Log($"[PartyPanel] Party queued for {gameMode} matchmaking (allowFill={allowFill})");
                LoadParty(); // Refresh to show IN_QUEUE status
            }
            else
            {
                Debug.LogError($"[PartyPanel] Failed to queue party: {result.Message}");
            }
        }

        private async void OnCancelQueueClicked()
        {
            if (_partyService == null) return;

            var result = await _partyService.CancelQueue();
            if (result.Success)
            {
                Debug.Log("[PartyPanel] Party queue cancelled");
                LoadParty(); // Refresh to show IDLE status
            }
            else
            {
                Debug.LogError($"[PartyPanel] Failed to cancel queue: {result.Message}");
            }
        }

        private void OnJoinCustomLobbyClicked()
        {
            if (roomCodeDialog != null) roomCodeDialog.SetActive(true);
            if (roomCodeInput != null) roomCodeInput.text = "";
        }

        private async void OnConfirmJoinRoomClicked()
        {
            if (roomCodeInput == null || _partyService == null) return;

            string code = roomCodeInput.text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("[PartyPanel] Room code is empty");
                return;
            }

            roomCodeDialog?.SetActive(false);
            Debug.Log($"[PartyPanel] Joining room {code} with party...");

            var result = await _partyService.JoinRoomWithParty(code, null);
            if (result.Success)
            {
                Debug.Log("[PartyPanel] Successfully joined room with party");
                UINavigator.Instance?.GoLobby();
            }
            else
            {
                Debug.LogError($"[PartyPanel] Failed to join room with party: {result.Message}");
            }
        }

        #endregion

#if UNITY_EDITOR
        // ── Editor — Context Menu: Create Party UI Prefab Templates ───────────

        [ContextMenu("NightHunt/Create Party Prefab Templates")]
        private void Editor_CreatePartyPrefabs()
        {
            const string parent = "Assets/_Night_Hunt/Prefabs";
            const string dir    = parent + "/UI";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder(parent, "UI");

            bool changed = false;
            changed |= Editor_CreatePartyRowPrefab(ref inviteItemPrefab,        dir + "/PartyInviteItem_Template.prefab",       "PartyInviteItem_Template",      new UnityEngine.Vector2(300f, 60f));
            changed |= Editor_CreatePartyRowPrefab(ref memberItemPrefab,        dir + "/PartyMemberItem_Template.prefab",      "PartyMemberItem_Template",      new UnityEngine.Vector2(300f, 60f));
            changed |= Editor_CreatePartyRowPrefab(ref friendSelectItemPrefab,  dir + "/FriendSelectItem_Template.prefab",     "FriendSelectItem_Template",     new UnityEngine.Vector2(300f, 50f));

            if (changed) UnityEditor.EditorUtility.SetDirty(this);
        }

        private bool Editor_CreatePartyRowPrefab(ref GameObject field, string path, string goName, UnityEngine.Vector2 size)
        {
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPanelView] Prefab already exists: {path}");
                return false;
            }

            var go = new GameObject(goName);
            go.AddComponent<UnityEngine.RectTransform>().sizeDelta = size;
            go.AddComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.12f, 0.12f, 0.12f, 0.85f);
            var hlg = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.spacing = 6f;
            hlg.padding = new UnityEngine.RectOffset(8, 8, 4, 4);

            var avatarGo = new GameObject("Avatar", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
            avatarGo.transform.SetParent(go.transform, false);
            avatarGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 44f;

            var nameGo  = new GameObject("NameText", typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
            nameGo.transform.SetParent(go.transform, false);
            nameGo.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            nameGo.GetComponent<TMPro.TextMeshProUGUI>().text = "Member Name";

            var actionBtn = new GameObject("ActionButton", typeof(UnityEngine.RectTransform),
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            actionBtn.transform.SetParent(go.transform, false);
            actionBtn.GetComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.2f, 0.5f, 0.8f, 1f);
            actionBtn.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 70f;

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);

            if (field == null) { field = saved; Debug.Log($"[PartyPanelView] Created & assigned: {path}"); return true; }
            Debug.Log($"[PartyPanelView] Created (not assigned — field already set): {path}");
            return false;
        }
#endif
    }
}
