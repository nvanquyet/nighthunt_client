using NightHunt.Common;
using NightHunt.Gameplay.Character.Data;
using NightHunt.State;
using UnityEngine;

namespace NightHunt.Networking.Player
{
    public static class PlayerIdentityFactory
    {
        /// <summary>
        /// Builds local identity before any player-owned NetworkObject exists.
        /// Custom_Relay uses this through connection-level broadcast; legacy paths can reuse it.
        /// </summary>
        public static PlayerRegistryData BuildLocalPlayerData()
        {
            var session = SessionState.Instance;
            int characterModelIndex = ResolveCharacterModelIndex();

            if (session != null && session.IsAuthenticated)
            {
                int requestedTeamId = ResolveRequestedGameplayTeamId(session.UserId);
                return new PlayerRegistryData
                {
                    BackendPlayerId = session.UserId.ToString(),
                    DisplayName = !string.IsNullOrEmpty(session.Username) ? session.Username : $"Player_{session.UserId}",
                    TeamId = requestedTeamId,
                    Status = PlayerConnectionStatus.Connected,
                    CharacterModelIndex = characterModelIndex
                };
            }

            Debug.LogWarning("[NH_HANDSHAKE][IDENTITY_FACTORY] No authenticated session found; using fallback guest data.");
            return new PlayerRegistryData
            {
                BackendPlayerId = $"guest_{Random.Range(1000, 9999)}",
                DisplayName = $"Guest_{Random.Range(1, 100)}",
                TeamId = -1,
                Status = PlayerConnectionStatus.Connected,
                CharacterModelIndex = characterModelIndex
            };
        }

        private static int ResolveCharacterModelIndex()
        {
            string savedCharacterId = PlayerPrefs.GetString(Constants.PREFS_SELECTED_CHARACTER_ID, "");
            if (string.IsNullOrEmpty(savedCharacterId) || CharacterDatabase.Instance == null)
                return 0;

            int resolved = CharacterDatabase.Instance.GetIndexById(savedCharacterId);
            if (resolved >= 0)
                return resolved;

            Debug.LogWarning($"[NH_HANDSHAKE][IDENTITY_FACTORY] Unknown character ID '{savedCharacterId}'; falling back to index 0.");
            return 0;
        }

        private static int ResolveRequestedGameplayTeamId(long userId)
        {
            var players = RoomState.Instance?.CurrentRoom?.players;
            var player = players?.Find(p => p.userId == userId);
            if (player == null)
                return -1;

            if (player.team == Constants.TEAM_1)
                return 0;
            if (player.team == Constants.TEAM_2)
                return 1;

            return player.team >= 0 ? player.team : -1;
        }
    }
}
