using System.Collections.Generic;
using FishNet.Object;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Utilities;
using UnityEngine;

namespace NightHunt.Gameplay.FogOfWar
{
    /// <summary>
    /// Runtime debug tool cho FOW team visibility.
    /// Uses khi test offline (start server + client thủ công, not available backend).
    ///
    /// SETUP:
    ///   Attach vào bất kỳ GO nào trong scene (ví dụ: DebugTools).
    ///   Run chỉ trên server hoặc host — các method ServerRpc đảm bảo thay đổi propagate đến mọi client.
    ///
    /// USAGE — Inspector Context Menu (right-click component):
    ///   "Print All Player Teams"   → log danh sách tất cả player + team của họ
    ///   "Force Player 0 → Team 0"  → ép player đầu tiên vào team 0
    ///   "Force Player 0 → Team 1"  → ép player đầu tiên vào team 1
    ///   "Swap All Teams"           → đảo team tất cả player (0→1, 1→0)
    ///
    /// USAGE — Keyboard (Play Mode, chỉ owner / host):
    ///   F9  → Print All Player Teams
    ///   F10 → Swap All Teams
    /// </summary>
    [DisallowMultipleComponent]
    public class FogTeamDebugController : NetworkBehaviour
    {
        [Header("Debug")]
        [Tooltip("Hiện overlay tất cả player teams trên screen.")]
        [SerializeField] private bool _showOnScreenOverlay = true;

        // ── Server helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Trả về tất cả NetworkPlayer hiện spawning trong scene.
        /// </summary>
        private static List<NetworkPlayer> GetAllNetworkPlayers()
        {
            var result = new List<NetworkPlayer>();
            foreach (var np in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                result.Add(np);
            return result;
        }

        // ── Context Menu ────────────────────────────────────────────────────────

        [ContextMenu("Print All Player Teams")]
        public void PrintAllPlayerTeams()
        {
            var players = GetAllNetworkPlayers();
            if (players.Count == 0) { Debug.Log("[FOWDebug] No NetworkPlayers found."); return; }

            Debug.Log($"[FOWDebug] === Player Teams ({players.Count} players) ===");
            for (int i = 0; i < players.Count; i++)
            {
                var np = players[i];
                Debug.Log($"  [{i}] ObjId={np.ObjectId}  Name={np.DisplayName}  Team={np.TeamId}  IsLocal={np.IsLocalPlayer}",
                    np.gameObject);
            }
        }

        [ContextMenu("Force Player 0 → Team 0")]
        public void ForcePlayer0ToTeam0() => ForcePlayerTeam(0, 0);

        [ContextMenu("Force Player 0 → Team 1")]
        public void ForcePlayer0ToTeam1() => ForcePlayerTeam(0, 1);

        [ContextMenu("Force Player 1 → Team 0")]
        public void ForcePlayer1ToTeam0() => ForcePlayerTeam(1, 0);

        [ContextMenu("Force Player 1 → Team 1")]
        public void ForcePlayer1ToTeam1() => ForcePlayerTeam(1, 1);

        [ContextMenu("Swap All Teams")]
        public void SwapAllTeams()
        {
            if (!IsServerStarted)
            {
                Debug.LogWarning("[FOWDebug] SwapAllTeams must run on the server/host.");
                return;
            }

            var players = GetAllNetworkPlayers();
            foreach (var np in players)
            {
                int newTeam = np.TeamId == 0 ? 1 : 0;
                ApplyTeamOverride(np, newTeam);
            }

            Debug.Log("[FOWDebug] Swapped all teams.");
        }

        /// <summary>
        /// Ép player tại index <paramref name="playerIndex"/> sang team <paramref name="newTeamId"/>.
        /// </summary>
        public void ForcePlayerTeam(int playerIndex, int newTeamId)
        {
            if (!IsServerStarted)
            {
                Debug.LogWarning("[FOWDebug] ForcePlayerTeam must run on the server/host.");
                return;
            }

            var players = GetAllNetworkPlayers();
            if (playerIndex < 0 || playerIndex >= players.Count)
            {
                Debug.LogWarning($"[FOWDebug] Player index {playerIndex} out of range (found {players.Count} players).");
                return;
            }

            ApplyTeamOverride(players[playerIndex], newTeamId);
        }

        /// <summary>
        /// Core: thay đổi TeamId trên server → SyncVar broadcast → FogTeamVisibilityBinder.RefreshVisibilityForLocalTeam() tự chạy.
        /// </summary>
        [Server]
        private void ApplyTeamOverride(NetworkPlayer np, int newTeamId)
        {
            var prev = new PlayerPublicData
            {
                DisplayName         = np.DisplayName,
                TeamId              = np.TeamId,
                Status              = PlayerConnectionStatus.InGame,
                CharacterModelIndex = 0
            };
            var next = prev;
            next.TeamId = newTeamId;

            np.SetPublicData(next);

            Debug.Log($"[FOWDebug] '{np.DisplayName}' (ObjId={np.ObjectId}) Team: {prev.TeamId} → {newTeamId}");
        }

        // ── Keyboard shortcuts (owner / host only) ──────────────────────────────

        private void Update()
        {
            if (!IsServerStarted) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
                PrintAllPlayerTeams();

            if (UnityEngine.Input.GetKeyDown(KeyCode.F10))
                SwapAllTeams();
        }

        // ── On-screen overlay ───────────────────────────────────────────────────

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        private void OnGUI()
        {
            if (!_showOnScreenOverlay) return;

            // Build styles once
            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box);
                _boxStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.72f));
                _boxStyle.padding = new RectOffset(12, 12, 8, 8);
            }
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label);
                _labelStyle.fontSize = 22;
                _labelStyle.richText = true;
                _labelStyle.normal.textColor = Color.white;
            }

            const float ROW_H   = 34f;
            const float BOX_W   = 520f;
            const float PAD_X   = 10f;
            const float PAD_Y   = 10f;

            var players = GetAllNetworkPlayers();
            int lineCount = 1 + Mathf.Max(players.Count, 1);
            float boxH = lineCount * ROW_H + PAD_Y * 2f;

            GUI.Box(new Rect(PAD_X, PAD_Y, BOX_W, boxH), GUIContent.none, _boxStyle);

            float y = PAD_Y + 6f;
            GUI.Label(new Rect(PAD_X + 8f, y, BOX_W - 16f, ROW_H),
                $"<b>[FOW Debug]</b>  Players: {players.Count}  Server: {IsServerStarted}", _labelStyle);
            y += ROW_H;

            if (players.Count == 0)
            {
                _labelStyle.normal.textColor = Color.grey;
                GUI.Label(new Rect(PAD_X + 8f, y, BOX_W - 16f, ROW_H), "  (no NetworkPlayers spawned yet)", _labelStyle);
                _labelStyle.normal.textColor = Color.white;
                return;
            }

            foreach (var np in players)
            {
                string local = np.IsLocalPlayer ? " \u2605" : "";
                string label = $"  {np.DisplayName}{local}  \u2192  Team {np.TeamId}";
                _labelStyle.normal.textColor = np.TeamId == 0 ? Color.cyan : Color.yellow;
                GUI.Label(new Rect(PAD_X + 8f, y, BOX_W - 16f, ROW_H), label, _labelStyle);
                y += ROW_H;
            }
            _labelStyle.normal.textColor = Color.white;
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
