using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Config;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using UnityEngine;

namespace NightHunt.Services.Config
{
    /// <summary>
    /// GameConfigService — fetches game modes and maps from the backend at startup
    /// and populates <see cref="GameModeConfig"/> and <see cref="MapConfig"/>
    /// so the rest of the client always reads from those ScriptableObject singletons.
    ///
    /// Call <see cref="FetchAsync"/> once during LoadingManager.InitFlow()
    /// after the backend health check passes, before AutoLogin.
    ///
    /// If the fetch fails (network error, server down) the ScriptableObject defaults
    /// remain in place — the game degrades gracefully.
    /// </summary>
    public class GameConfigService : MonoBehaviour
    {
        private IBackendClient _backendClient;

        private void Awake()
        {
            if (_backendClient == null && Core.GameManager.Instance != null)
                _backendClient = Core.GameManager.Instance.BackendClient;

            if (_backendClient == null)
                _backendClient = FindFirstObjectByType<BackendHttpClient>();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Fetch game modes and maps from backend in parallel.
        /// Populates GameModeConfig and MapConfig with live server data.
        /// Returns true if both fetches succeeded; false on any failure (logs warning).
        /// </summary>
        public async Task<bool> FetchAsync()
        {
            if (_backendClient == null)
            {
                Debug.LogWarning("[GameConfigService] BackendClient not available — skipping remote config fetch.");
                return false;
            }

            var modesTask = _backendClient.GetAsync<List<GameModeResponseDTO>>(Constants.API_GAME_MODES);
            var mapsTask  = _backendClient.GetAsync<List<GameMapResponseDTO>>(Constants.API_MAPS);

            await Task.WhenAll(modesTask, mapsTask);

            bool ok = true;

            var modesResult = modesTask.Result;
            if (modesResult.Success && modesResult.Data != null)
            {
                GameModeConfig.LoadFromRemote(modesResult.Data.ToArray());
                Debug.Log($"[GameConfigService] Loaded {modesResult.Data.Count} game modes from server.");
            }
            else
            {
                Debug.LogWarning($"[GameConfigService] Game modes fetch failed: {modesResult.Message} — using local defaults.");
                ok = false;
            }

            var mapsResult = mapsTask.Result;
            if (mapsResult.Success && mapsResult.Data != null)
            {
                MapConfig.LoadFromRemote(mapsResult.Data.ToArray());
                Debug.Log($"[GameConfigService] Loaded {mapsResult.Data.Count} maps from server.");
            }
            else
            {
                Debug.LogWarning($"[GameConfigService] Maps fetch failed: {mapsResult.Message} — using local defaults.");
                ok = false;
            }

            return ok;
        }
    }
}
