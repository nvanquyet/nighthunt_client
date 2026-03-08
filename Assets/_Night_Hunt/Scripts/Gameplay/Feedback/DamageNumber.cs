using UnityEngine;
using TMPro;
using System.Collections;

namespace NightHunt.Gameplay.Feedback
{
    /// <summary>
    /// Damage number component
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        private RectTransform rectTransform;
        private float lifetime;
        private float speed;
        private Vector3 startPosition;

        public void Initialize(Vector3 screenPosition, float damage, Color color, float lifeTime, float moveSpeed)
        {
            if (_text == null) _text = GetComponentInChildren<TextMeshProUGUI>();
            rectTransform = GetComponent<RectTransform>();

            if (_text != null)
            {
                _text.text = Mathf.CeilToInt(damage).ToString();
                _text.color = color;
            }

            if (rectTransform != null)
            {
                rectTransform.position = screenPosition;
                startPosition = screenPosition;
            }

            lifetime = lifeTime;
            speed = moveSpeed;

			//Active object
			this.gameObject.SetActive(true);

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
                    if (_text != null)
                    {
                        Color color = _text.color;
                        color.a = 1f - progress;
                        _text.color = color;
                    }
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
