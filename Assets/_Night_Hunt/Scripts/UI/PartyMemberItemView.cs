using System;
using NightHunt.Data.DTOs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Single party member row inside the Party Panel.
    ///
    /// Shows username + host badge. Host can kick or transfer leadership to non-host members.
    ///
    /// SETUP (Prefab):
    ///   PartyMemberItem (this script)
    ///   ├── UsernameText          (TMP)
    ///   ├── LeaderBadge           (TMP — active when member.isHost)
    ///   ├── KickButton            (Button — visible to leader, hidden for own slot)
    ///   └── TransferLeaderButton  (Button — visible to leader, hidden for own slot)
    /// </summary>
    public class PartyMemberItemView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI leaderBadge;
        [SerializeField] private Button          kickButton;
        [SerializeField] private Button          transferLeaderButton;

        private PartyMemberResponse _member;
        private Action<long>        _onKick;
        private Action<long>        _onTransferLeader;

        public void Setup(PartyMemberResponse member, bool isViewerLeader,
                          Action<long> onKick, Action<long> onTransferLeader)
        {
            _member           = member;
            _onKick           = onKick;
            _onTransferLeader = onTransferLeader;

            if (usernameText != null) usernameText.text = member.username;
            if (leaderBadge  != null) leaderBadge.gameObject.SetActive(member.isHost);

            // Kick / transfer only when viewer is leader and this member is not the leader
            bool showActions = isViewerLeader && !member.isHost;

            if (kickButton != null)
            {
                kickButton.gameObject.SetActive(showActions);
                kickButton.onClick.RemoveAllListeners();
                kickButton.onClick.AddListener(() => _onKick?.Invoke(member.userId));
            }

            if (transferLeaderButton != null)
            {
                transferLeaderButton.gameObject.SetActive(showActions);
                transferLeaderButton.onClick.RemoveAllListeners();
                transferLeaderButton.onClick.AddListener(() => _onTransferLeader?.Invoke(member.userId));
            }
        }
    }
}
