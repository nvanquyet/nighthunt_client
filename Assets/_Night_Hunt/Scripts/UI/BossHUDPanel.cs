using System.Collections;
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
    ///   2. Assign _panel, _hpSlider, _bossNameText, _hpText in Inspector.
    ///   3. Panel starts hidden; shown when BossSpawnedEvent fires.
    ///
    /// Data flow (server-authoritative, client reads SyncVar):
    ///   BossSpawnedEvent → show panel, subscribe BossController.OnHealthChanged
    ///   OnHealthChanged  → update HP slider and text live (no Update polling)
    ///   BossKilledEvent  → show "Boss Defeated!", then hide panel after delay
    /// </summary>
    public class BossHUDPanel : MonoBehaviour
    {
        private const string KilledMessage  = "Boss Defeated!";
        private const string HpSectionLabel = "BOSS HP";

        [Header("UI References")]
        [SerializeField] private GameObject         _panel;
        [SerializeField] private Slider             _hpSlider;
        [SerializeField] private TextMeshProUGUI    _bossNameText;
        [SerializeField] private TextMeshProUGUI    _hpText;
        [SerializeField] private TextMeshProUGUI    _hpLabelText;

        [Header("Settings")]
        [SerializeField] private string _defaultBossName = "BOSS";
        [SerializeField] private float  _defeatedDisplayDuration = 2f;

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
            UnsubscribeHealth();
        }

        // ── Event Handlers ──────────────────────────────────────────────────

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            UnsubscribeHealth();

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

            if (_bossController != null)
                _bossController.OnHealthChanged += OnBossHpChanged;

            if (_bossNameText != null)
                _bossNameText.text = string.IsNullOrEmpty(evt.BossId) ? _defaultBossName : evt.BossId.ToUpper();

            if (_hpLabelText != null)
                _hpLabelText.text = HpSectionLabel;

            // Sync slider to current HP immediately (SyncVar may already have a value).
            if (_bossController != null)
                OnBossHpChanged(_bossController.CurrentHp, _bossController.MaxHp);

            Show();
        }

        private void OnBossKilled(BossKilledEvent evt)
        {
            UnsubscribeHealth();
            StopAllCoroutines();
            StartCoroutine(ShowDefeatedThenHide());
        }

        private void OnBossHpChanged(float current, float max)
        {
            float fraction = max > 0f ? current / max : 0f;
            if (_hpSlider != null) _hpSlider.value = fraction;
            if (_hpText   != null) _hpText.text    = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private void UnsubscribeHealth()
        {
            if (_bossController != null)
            {
                _bossController.OnHealthChanged -= OnBossHpChanged;
                _bossController = null;
            }
        }

        private IEnumerator ShowDefeatedThenHide()
        {
            if (_bossNameText != null) _bossNameText.text = KilledMessage;
            if (_hpSlider     != null) _hpSlider.value   = 0f;
            if (_hpText       != null) _hpText.text       = string.Empty;
            yield return new WaitForSeconds(_defeatedDisplayDuration);
            Hide();
        }

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
