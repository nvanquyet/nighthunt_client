using UnityEngine;
using UnityEngine.UI;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Dynamic crosshair that expands based on weapon spread / movement.
    ///
    /// Inspector setup:
    ///   â€¢ Assign the four line RectTransforms (top / bottom / left / right).
    ///   â€¢ Call <see cref="Bind"/> once CombatHUDPanel has an IWeaponSystem ready.
    ///   â€¢ Spread also driven from outside via <see cref="AddMovementSpread"/>.
    ///
    /// Color states:
    ///   â€¢ White  â€” normal
    ///   â€¢ Orange â€” reloading
    ///   â€¢ Red    â€” depleted (no mag + no reserve)
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        // â”€â”€ Inspector â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Lines")] [SerializeField] private RectTransform _topLine;
        [SerializeField] private RectTransform _bottomLine;
        [SerializeField] private RectTransform _leftLine;
        [SerializeField] private RectTransform _rightLine;

        [Header("Center dot (optional)")] [SerializeField]
        private GameObject _centerDot;

        [Header("Spread settings")] [Tooltip("Gap from center when fully still")] [SerializeField]
        private float _minSpread = 12f;

        [Tooltip("Maximum extra spread added on shots / movement")] [SerializeField]
        private float _maxAdditionalSpread = 80f;

        [Tooltip("How fast spread relaxes back to min (units/sec)")] [SerializeField]
        private float _relaxSpeed = 6f;

        [Tooltip("How fast spread interpolates toward target")] [SerializeField]
        private float _lerpSpeed = 12f;

        [Tooltip("Spread burst added per shot fired")] [SerializeField]
        private float _spreadPerShot = 20f;

        [Header("Color")] [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _reloadColor = new Color(1f, 0.6f, 0f);
        [SerializeField] private Color _noAmmoColor = Color.red;

        // â”€â”€ Runtime â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private IWeaponSystem _weaponSystem;
        private float _currentSpread;
        private float _targetSpread;
        private bool _isReloading;
        private bool _isDepleted;
        private int _lastMag = -1; // detect shots by mag decreasing
        private bool _isBound;

        // â”€â”€ Binding â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public void Bind(IWeaponSystem weaponSystem)
        {
            if (_isBound) Unbind();
            _weaponSystem = weaponSystem;
            if (_weaponSystem == null) return;

            _weaponSystem.OnAmmoChanged += HandleAmmoChanged;
            _weaponSystem.OnReloadStateChanged += HandleReloadState;
            _weaponSystem.OnActiveWeaponChanged += HandleWeaponSwitch;
            _isBound = true;
        }

        public void Unbind()
        {
            if (_weaponSystem != null)
            {
                _weaponSystem.OnAmmoChanged -= HandleAmmoChanged;
                _weaponSystem.OnReloadStateChanged -= HandleReloadState;
                _weaponSystem.OnActiveWeaponChanged -= HandleWeaponSwitch;
            }

            _weaponSystem = null;
            _isBound = false;
        }

        /// <summary>
        /// Call each frame from movement with normalized speed (0 = still, 1 = full sprint).
        /// </summary>
        public void AddMovementSpread(float normalizedSpeed)
        {
            _targetSpread = Mathf.Min(
                _targetSpread + normalizedSpeed * _maxAdditionalSpread * Time.deltaTime * 3f,
                _maxAdditionalSpread);
        }

        // â”€â”€ Unity lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void OnDestroy() => Unbind();

        private void Update()
        {
            _targetSpread = Mathf.Max(0f, _targetSpread - _relaxSpeed * Time.deltaTime);
            _currentSpread = Mathf.Lerp(_currentSpread, _targetSpread, _lerpSpeed * Time.deltaTime);

            float gap = _minSpread + _currentSpread;
            ApplyLines(gap);
            ApplyColor();
        }

        // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void ApplyLines(float gap)
        {
            if (_topLine != null) _topLine.anchoredPosition = new Vector2(0f, gap);
            if (_bottomLine != null) _bottomLine.anchoredPosition = new Vector2(0f, -gap);
            if (_leftLine != null) _leftLine.anchoredPosition = new Vector2(-gap, 0f);
            if (_rightLine != null) _rightLine.anchoredPosition = new Vector2(gap, 0f);
        }

        private void ApplyColor()
        {
            Color c = _isReloading ? _reloadColor
                : _isDepleted ? _noAmmoColor
                : _normalColor;
            SetAllLines(c);
        }

        private void SetAllLines(Color c)
        {
            SetColor(_topLine, c);
            SetColor(_bottomLine, c);
            SetColor(_leftLine, c);
            SetColor(_rightLine, c);
        }

        private static void SetColor(RectTransform rt, Color c)
        {
            if (rt == null) return;
            var img = ComponentResolver.Find<Image>(rt)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] Image not found")
                .Resolve();
            if (img != null) img.color = c;
        }

        // â”€â”€ Event handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        // currentMag, totalReserve, magazineCapacity
        private void HandleAmmoChanged(int currentMag, int totalReserve, int capacity)
        {
            // Detect firing: magazine decreased (and not during reload)
            if (_lastMag > 0 && currentMag < _lastMag && !_isReloading)
            {
                _targetSpread = Mathf.Min(_targetSpread + _spreadPerShot, _maxAdditionalSpread);
            }

            _lastMag = currentMag;
            _isDepleted = currentMag == 0 && totalReserve == 0;
        }

        private void HandleReloadState(bool reloading)
        {
            _isReloading = reloading;
            if (reloading)
            {
                _targetSpread = 0f;
                _currentSpread = 0f;
            }
        }

        private void HandleWeaponSwitch(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            _targetSpread = 0f;
            _currentSpread = 0f;
            _isReloading = false;
            _lastMag = -1;
        }
    }
}