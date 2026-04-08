using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Networking;
using NightHunt.Gameplay.Character.Data;

namespace NightHunt.UI
{
    /// <summary>
    /// Single row in the team member list.
    /// Bind() once after AllPlayersReadyEvent — alive state self-updates via event.
    /// </summary>
    public class TeamMemberRow : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private Image           _avatarIcon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private GameObject      _aliveIndicator;  // green dot
        [SerializeField] private GameObject      _deadIndicator;   // skull / grey

        private NetworkPlayer _player;

        // ── Bind ─────────────────────────────────────────────────────────────

        public void Bind(NetworkPlayer player)
        {
            if (_player != null)
                _player.OnAliveChanged -= OnAliveChanged;

            _player = player;
            _player.OnAliveChanged += OnAliveChanged;

            // Name
            if (_nameText != null)
                _nameText.text = player.DisplayName;

            // Avatar — look up Icon from CharacterDatabase by CharacterModelIndex
            if (_avatarIcon != null)
            {
                var def = CharacterDatabase.Instance?.GetByIndex(player.CharacterModelIndex);
                _avatarIcon.sprite  = def?.Icon;
                _avatarIcon.enabled = _avatarIcon.sprite != null;
            }

            // Alive state khởi tạo
            RefreshAliveVisual(player.IsAlive);
        }

        public void Unbind()
        {
            if (_player != null)
                _player.OnAliveChanged -= OnAliveChanged;
            _player = null;
        }

        private void OnDestroy() => Unbind();

        // ── Alive ─────────────────────────────────────────────────────────────

        private void OnAliveChanged(bool isAlive) => RefreshAliveVisual(isAlive);

        private void RefreshAliveVisual(bool isAlive)
        {
            if (_aliveIndicator != null) _aliveIndicator.SetActive(isAlive);
            if (_deadIndicator  != null) _deadIndicator.SetActive(!isAlive);

            // Dim toàn bộ row khi chết
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = isAlive ? 1f : 0.45f;
        }
    }
}