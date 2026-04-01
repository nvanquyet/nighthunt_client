using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Boss;
using NightHunt.Gameplay.Core.Events;

namespace NightHunt.UI
{
    /// <summary>
    /// Shows boss HP bar when a boss is alive during Phase 2.
    ///
    /// Setup:
    ///   1. Place this panel in the GameHUD canvas.
    ///   2. Assign _panel, _hpSlider, _bossNameText in Inspector.
    ///   3. Panel starts hidden; shown when BossSpawnedEvent fires.
    ///
    /// Data flow (server-authoritative, client reads SyncVar):
    ///   BossSpawnedEvent → show panel, cache BossController ref
    ///   Update() polling → BossController.CurrentHp / MaxHp → slider
    ///   BossKilledEvent  → hide panel
    /// </summary>
    public class BossHUDPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject         _panel;
        [SerializeField] private Slider             _hpSlider;
        [SerializeField] private TextMeshProUGUI    _bossNameText;
        [SerializeField] private TextMeshProUGUI    _hpText;

        [Header("Settings")]
        [SerializeField] private string _defaultBossName = "BOSS";

        private BossController _bossController;

        // ── Unity Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            Hide();
        }

        private void OnEnable()
        {
            GameplayEventBus.Instance?.Subscribe<BossSpawnedEvent>(OnBossSpawned);
            GameplayEventBus.Instance?.Subscribe<BossKilledEvent>(OnBossKilled);
        }

        private void OnDisable()
        {
            GameplayEventBus.Instance?.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
            GameplayEventBus.Instance?.Unsubscribe<BossKilledEvent>(OnBossKilled);
        }

        private void Update()
        {
            if (_bossController == null || _bossController.IsDead) return;

            float fraction = _bossController.MaxHp > 0f
                ? _bossController.CurrentHp / _bossController.MaxHp
                : 0f;

            if (_hpSlider   != null) _hpSlider.value = fraction;
            if (_hpText     != null) _hpText.text    =
                $"{Mathf.CeilToInt(_bossController.CurrentHp)} / {Mathf.CeilToInt(_bossController.MaxHp)}";
        }

        // ── Event Handlers ──────────────────────────────────────────────────

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            // Find the spawned BossController by ID — scan all spawned bosses.
            var allBosses = FindObjectsByType<BossController>(FindObjectsSortMode.None);
            foreach (var boss in allBosses)
            {
                if (boss != null && boss.BossId == evt.BossId)
                {
                    _bossController = boss;
                    break;
                }
            }

            if (_bossController == null)
            {
                // Fallback: take whichever boss is alive if IDs mismatch.
                _bossController = FindFirstObjectByType<BossController>();
            }

            if (_bossNameText != null)
                _bossNameText.text = string.IsNullOrEmpty(evt.BossId) ? _defaultBossName : evt.BossId.ToUpper();

            Show();
        }

        private void OnBossKilled(BossKilledEvent evt)
        {
            _bossController = null;
            Hide();
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private void Show()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        private void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
