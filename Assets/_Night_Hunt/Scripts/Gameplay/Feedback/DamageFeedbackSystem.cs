using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Cam = UnityEngine.Camera;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Feedback
{
    /// <summary>
    /// Shows damage numbers and hit indicators
    /// </summary>
    public class DamageFeedbackSystem : MonoBehaviour
    {
        [Header("Damage Number Settings")] [SerializeField]
        private GameObject damageNumberPrefab;

        [SerializeField] private float numberLifetime = 2f;
        [SerializeField] private float numberSpeed = 2f;
        [SerializeField] private Color normalDamageColor = Color.white;
        [SerializeField] private Color criticalDamageColor = Color.yellow;
        [SerializeField] private Color headshotColor = Color.red;

        [Header("Hit Indicator Settings")] [SerializeField]
        private GameObject hitIndicatorPrefab;

        [SerializeField] private float indicatorLifetime = 0.5f;

        private Cam playerCamera;

        private void Start()
        {
            playerCamera = Cam.main;
            if (playerCamera == null)
            {
                playerCamera = FindObjectOfType<Cam>();
            }
        }

        /// <summary>
        /// Show damage number
        /// </summary>
        public void ShowDamageNumber(Vector3 worldPosition, float damage, bool isHeadshot = false,
            bool isCritical = false)
        {
            if (damageNumberPrefab == null) return;

            // Convert world position to screen position
            Vector3 screenPos = playerCamera.WorldToScreenPoint(worldPosition);
            if (screenPos.z < 0) return; // Behind camera

            GameObject numberObj = Instantiate(damageNumberPrefab, transform);
            DamageNumber number = ComponentResolver.Find<DamageNumber>(numberObj)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] DamageNumber not found")
                .Resolve();

            if (number == null)
            {
                number = numberObj.AddComponent<DamageNumber>();
            }

            Color color = normalDamageColor;
            if (isHeadshot)
            {
                color = headshotColor;
            }
            else if (isCritical)
            {
                color = criticalDamageColor;
            }

            number.Initialize(screenPos, damage, color, numberLifetime, numberSpeed);
        }

        /// <summary>
        /// Show hit indicator
        /// </summary>
        public void ShowHitIndicator(Vector3 hitDirection)
        {
            if (hitIndicatorPrefab == null) return;

            GameObject indicatorObj = Instantiate(hitIndicatorPrefab, transform);
            HitIndicator indicator = ComponentResolver.Find<HitIndicator>(indicatorObj)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] HitIndicator not found")
                .Resolve();

            if (indicator == null)
            {
                indicator = indicatorObj.AddComponent<HitIndicator>();
            }

            indicator.Initialize(hitDirection, indicatorLifetime);
        }
    }
}