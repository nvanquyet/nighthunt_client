using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Config;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using NightHunt.Utils;
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
                Debug.LogError("[GameConfigService] BackendClient not available — ensure GameManager is initialized before GameConfigService.");
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

            // NOTE: Use T[] (array) not List<T> — Unity's JsonUtility cannot deserialize
            // generic List<T> inside a generic ApiResult<T> wrapper. Arrays work correctly.
            var modesTask = _backendClient.GetAsync<GameModeResponseDTO[]>(Constants.API_GAME_MODES);
            var mapsTask  = _backendClient.GetAsync<GameMapResponseDTO[]>(Constants.API_MAPS);

            await Task.WhenAll(modesTask, mapsTask);

            bool ok = true;

            var modesResult = modesTask.Result;
            if (modesResult.Success && modesResult.Data != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Server returned {modesResult.Data.Length} game modes:");
                foreach (var m in modesResult.Data)
                    sb.AppendLine($"  > key={m.modeKey}  display=\"{m.displayName}\"  allowFill={m.allowFill}  status={m.modeStatus}  matchmakingEnabled={m.matchmakingEnabled}  isDevMode={m.isDevMode}");
                ConditionalLogger.Log("gameconfig", sb.ToString());

                GameModeConfig.LoadFromRemote(modesResult.Data);
            }
            else
            {
                Debug.LogWarning($"[GameConfigService] Game modes fetch failed: {modesResult.Message} — using local .asset defaults.");
                ConditionalLogger.LogWarning("gameconfig", $"Fetch failed: {modesResult.Message}");
                ok = false;
            }

            var mapsResult = mapsTask.Result;
            if (mapsResult.Success && mapsResult.Data != null)
            {
                MapConfig.LoadFromRemote(mapsResult.Data);
                ConditionalLogger.Log("gameconfig", $"Loaded {mapsResult.Data.Length} maps from server.");
                Debug.Log($"[GameConfigService] Loaded {mapsResult.Data.Length} maps from server.");
            }
            else
            {
                Debug.LogWarning($"[GameConfigService] Maps fetch failed: {mapsResult.Message} — using local defaults.");
                ConditionalLogger.LogWarning("gameconfig", $"Maps fetch failed: {mapsResult.Message}");
                ok = false;
            }

            return ok;
        }
    }
}
