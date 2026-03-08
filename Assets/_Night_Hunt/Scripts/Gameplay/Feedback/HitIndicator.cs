using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace NightHunt.Gameplay.Feedback
{
    /// <summary>
    /// Hit indicator component
    /// </summary>
    public class HitIndicator : MonoBehaviour
    {
         [SerializeField] private Image image;
        private float lifetime;

        public void Initialize(Vector3 direction, float lifeTime)
        {
            image ??= GetComponent<Image>();
            lifetime = lifeTime;

            // Rotate to face hit direction
            if (image != null)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

			//Active object
			this.gameObject.SetActive(true);

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
