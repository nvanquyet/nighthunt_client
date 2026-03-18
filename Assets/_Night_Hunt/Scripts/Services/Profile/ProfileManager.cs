using System;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using NightHunt.State;
using NightHunt.Core;
using UnityEngine;

namespace NightHunt.Services.Profile
{
    /// <summary>
    /// Manages the player's backend profile (GET /api/profile, PUT /api/profile/character).
    ///
    /// Character selection flow:
    ///   1. UI calls <see cref="SetCharacter"/> with the CharacterDefinition.CharacterId string.
    ///   2. Backend persists it and confirms with an updated ProfileResponse.
    ///   3. SessionState.SelectedCharacterId is updated + synced to PlayerPrefs.
    ///   4. Next time ClientNetworkHandler.GetLocalPlayerData() runs it reads the new value.
    /// </summary>
    public class ProfileManager : MonoBehaviour
    {
        [SerializeField] private IBackendClient backendClient;
        [SerializeField] private SessionState   sessionState;

        private void Awake()
        {
            if (backendClient == null && GameManager.Instance != null)
                backendClient = GameManager.Instance.BackendClient;
            if (backendClient == null)
            {
#if UNITY_2023_1_OR_NEWER
                backendClient = FindFirstObjectByType<BackendHttpClient>();
#else
                backendClient = FindObjectOfType<BackendHttpClient>();
#endif
            }
            if (sessionState == null)
                sessionState = SessionState.Instance;
        }

        // ─── Get Profile ──────────────────────────────────────────────────────

        /// <summary>
        /// Fetches the latest profile from the backend and syncs it to SessionState.
        /// Call this if you need fresh data (e.g. after returning from a character-select scene).
        /// </summary>
        public async Task<ApiResult<ProfileResponse>> FetchProfile()
        {
            var result = await backendClient.GetAsync<ProfileResponse>(Constants.API_PROFILE_GET);
            if (result.Success && result.Data != null)
            {
                sessionState.SetSelectedCharacterId(result.Data.selectedCharacterId);
                Debug.Log($"[ProfileManager] Profile fetched — selectedCharacterId={result.Data.selectedCharacterId ?? "null"}");
            }
            else
            {
                Debug.LogWarning($"[ProfileManager] FetchProfile failed: {result.Message}");
            }
            return result;
        }

        // ─── Update Character ─────────────────────────────────────────────────

        /// <summary>
        /// Persists the player's chosen character to the backend.
        ///
        /// <para>Usage from a character-select UI button:</para>
        /// <code>
        ///   await ProfileManager.Instance.SetCharacter(definition.CharacterId);
        /// </code>
        /// </summary>
        /// <param name="characterId">
        ///   CharacterDefinition.CharacterId string (e.g. "character_02").
        ///   Must be a valid, non-empty ID registered in CharacterDatabase.
        /// </param>
        public async Task<ApiResult<ProfileResponse>> SetCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
            {
                Debug.LogError("[ProfileManager] SetCharacter called with null/empty characterId.");
                return ApiResult<ProfileResponse>.Error("characterId cannot be empty");
            }

            var request = new UpdateCharacterRequest { selectedCharacterId = characterId };
            var result  = await backendClient.PutAsync<ProfileResponse>(
                Constants.API_PROFILE_SET_CHARACTER, request);

            if (result.Success && result.Data != null)
            {
                sessionState.SetSelectedCharacterId(result.Data.selectedCharacterId);
                Debug.Log($"[ProfileManager] Character updated → {result.Data.selectedCharacterId}");
            }
            else
            {
                Debug.LogWarning($"[ProfileManager] SetCharacter failed: {result.Message}");
            }

            return result;
        }
    }
}
