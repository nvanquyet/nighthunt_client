using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Cam = UnityEngine.Camera;

namespace NightHunt.Gameplay.Feedback
{
    /// <summary>
    /// Shows damage numbers and hit indicators
    /// </summary>
    public class DamageFeedbackSystem : MonoBehaviour
    {
        [Header("Damage Number Settings")]
        [SerializeField] private GameObject damageNumberPrefab;
        [SerializeField] private float numberLifetime = 2f;
        [SerializeField] private float numberSpeed = 2f;
        [SerializeField] private Color normalDamageColor = Color.white;
        [SerializeField] private Color criticalDamageColor = Color.yellow;
        [SerializeField] private Color headshotColor = Color.red;

        [Header("Hit Indicator Settings")]
        [SerializeField] private GameObject hitIndicatorPrefab;
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
        public void ShowDamageNumber(Vector3 worldPosition, float damage, bool isHeadshot = false, bool isCritical = false)
        {
            if (damageNumberPrefab == null) return;

            // Convert world position to screen position
            Vector3 screenPos = playerCamera.WorldToScreenPoint(worldPosition);
            if (screenPos.z < 0) return; // Behind camera

            GameObject numberObj = Instantiate(damageNumberPrefab, transform);
            DamageNumber number = numberObj.GetComponent<DamageNumber>();

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
            HitIndicator indicator = indicatorObj.GetComponent<HitIndicator>();

            if (indicator == null)
            {
                indicator = indicatorObj.AddComponent<HitIndicator>();
            }

            indicator.Initialize(hitDirection, indicatorLifetime);
        }
    }

    /// <summary>
    /// Damage number component
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        private TextMeshProUGUI text;
        private RectTransform rectTransform;
        private float lifetime;
        private float speed;
        private Vector3 startPosition;

        public void Initialize(Vector3 screenPosition, float damage, Color color, float lifeTime, float moveSpeed)
        {
            text = GetComponentInChildren<TextMeshProUGUI>();
            rectTransform = GetComponent<RectTransform>();

            if (text != null)
            {
                text.text = Mathf.CeilToInt(damage).ToString();
                text.color = color;
            }

            if (rectTransform != null)
            {
                rectTransform.position = screenPosition;
                startPosition = screenPosition;
            }

            lifetime = lifeTime;
            speed = moveSpeed;

            StartCoroutine(AnimateNumber());
        }

        private IEnumerator AnimateNumber()
        {
            float elapsed = 0f;
            Vector3 randomOffset = new Vector3(Random.Range(-50f, 50f), 0f, 0f);

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / lifetime;

                // Move up
                if (rectTransform != null)
                {
                    Vector3 position = startPosition + Vector3.up * (speed * elapsed * 100f) + randomOffset;
                    rectTransform.position = position;

                    // Fade out
                    if (text != null)
                    {
                        Color color = text.color;
                        color.a = 1f - progress;
                        text.color = color;
                    }
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Hit indicator component
    /// </summary>
    public class HitIndicator : MonoBehaviour
    {
        private Image image;
        private float lifetime;

        public void Initialize(Vector3 direction, float lifeTime)
        {
            image = GetComponent<Image>();
            lifetime = lifeTime;

            // Rotate to face hit direction
            if (image != null)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - (elapsed / lifetime);

                if (image != null)
                {
                    Color color = image.color;
                    color.a = alpha;
                    image.color = color;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}

