using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Networking.Player;

namespace NightHunt.UI
{
    /// <summary>
    /// Team HUD coordinator.
    ///
    /// Preferred flow:
    /// - Owner row uses the same TeamMemberRow contract as every teammate row.
    /// - Teammate rows are spawned from the shared prefab.
    /// - Spectator state only marks the currently observed row; it does not replace the row schema.
    ///
    /// Legacy note:
    /// If the scene has not assigned <see cref="_ownerRow"/>, teammate rows still work and the
    /// owner row simply remains on the old scene setup until the prefab hookup is migrated.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TeamMemberPanel : MonoBehaviour
    {
        [Header("Owner Row")]
        [Tooltip("Owner row should use the same TeamMemberRow contract as teammate rows.")]
        [SerializeField] private TeamMemberRow _ownerRow;

        [Header("Team Member Rows")]
        [SerializeField] private GameObject _memberRowPrefab;
        [SerializeField] private Transform _membersContainer;

        private NetworkPlayer _owner;
        private UIPlayerContext _context;
        private int _ownerTeamId = -1;
        private readonly List<TeamMemberRow> _memberRows = new();
        private bool _teamsPopulated;
        private NetworkPlayer _observedPlayer;

        public void Initialize(NetworkPlayer owner, UIPlayerContext context)
        {
            Cleanup();

            _owner = owner;
            _context = context;
            _ownerTeamId = owner != null ? owner.TeamId : -1;
            if (_ownerRow == null)
                _ownerRow = GetComponentInChildren<TeamMemberRow>(includeInactive: true);

            if (_ownerRow != null && owner != null)
                _ownerRow.Bind(owner);

            GameplayEventBus.Instance?.Subscribe<AllPlayersReadyEvent>(OnAllPlayersReady);

            if (PlayerPublicRegistry.Instance != null)
                StartCoroutine(PopulateTeammatesNextFrame());
        }

        public void SetObservedPlayer(NetworkPlayer observedPlayer)
        {
            _observedPlayer = observedPlayer;
            RefreshSpectatorMarkers();
        }

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            GameplayEventBus.Instance?.Unsubscribe<AllPlayersReadyEvent>(OnAllPlayersReady);

            _ownerRow?.Unbind();
            ClearMemberRows();

            _owner = null;
            _context = null;
            _ownerTeamId = -1;
            _teamsPopulated = false;
            _observedPlayer = null;
        }

        private void OnAllPlayersReady(AllPlayersReadyEvent _)
        {
            if (_teamsPopulated) return;
            _teamsPopulated = true;
            StartCoroutine(PopulateTeammatesNextFrame());
        }

        private IEnumerator PopulateTeammatesNextFrame()
        {
            yield return new WaitForSeconds(0.3f);

            var registry = PlayerPublicRegistry.Instance;
            if (registry == null)
            {
                _teamsPopulated = false;
                yield break;
            }

            ClearMemberRows();

            if (_memberRowPrefab == null || _membersContainer == null || _owner == null)
                yield break;

            var teammates = _ownerTeamId >= 0
                ? registry.GetPlayersByTeam(_ownerTeamId)
                : new List<NetworkPlayer>();

            foreach (var player in teammates)
            {
                if (player == null || player == _owner) continue;

                var go = Instantiate(_memberRowPrefab, _membersContainer);
                var row = go.GetComponent<TeamMemberRow>();
                if (row == null)
                {
                    Destroy(go);
                    continue;
                }

                row.Bind(player);
                _memberRows.Add(row);
            }

            RefreshSpectatorMarkers();
        }

        private void ClearMemberRows()
        {
            foreach (var row in _memberRows)
            {
                if (row == null) continue;
                row.Unbind();
                Destroy(row.gameObject);
            }

            _memberRows.Clear();
        }

        private void RefreshSpectatorMarkers()
        {
            bool markObserved = _observedPlayer != null && _observedPlayer != _owner;

            if (_ownerRow != null)
                _ownerRow.SetSpectating(_observedPlayer != null && _observedPlayer == _owner);

            foreach (var row in _memberRows)
            {
                if (row == null) continue;
                row.SetSpectating(markObserved && row.BoundPlayer == _observedPlayer);
            }
        }
    }
}
