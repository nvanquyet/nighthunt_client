using System.Collections;
using UnityEngine;

namespace NightHunt.InteractionSystem.Pickup
{
    public class PickupAnimator : MonoBehaviour
    {
        [Header("Animation")] [SerializeField] private float animationDuration = 0.3f;
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private Transform targetPoint; // Player chest position

        [Header("Effects")] [SerializeField] private ParticleSystem pickupParticles;
        [SerializeField] private AudioClip pickupSound;

        public void PlayPickupAnimation(Vector3 itemPosition)
        {
            StartCoroutine(AnimatePickup(itemPosition));
        }

        private IEnumerator AnimatePickup(Vector3 startPosition)
        {
            // Create temporary visual
            GameObject visual = CreatePickupVisual(startPosition);

            float elapsed = 0f;
            Vector3 endPosition = targetPoint.position;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                float curveValue = moveCurve.Evaluate(t);

                visual.transform.position = Vector3.Lerp(startPosition, endPosition, curveValue);
                visual.transform.localScale = Vector3.one * (1f - t * 0.5f);

                yield return null;
            }

            // Cleanup
            Destroy(visual);

            // Effects
            if (pickupParticles != null)
            {
                pickupParticles.Play();
            }

            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, endPosition, 0.5f);
            }
        }

        private GameObject CreatePickupVisual(Vector3 position)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.transform.position = position;
            visual.transform.localScale = Vector3.one * 0.2f;

            // Make it glow
            Renderer renderer = visual.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.yellow);
            renderer.material = mat;

            // No collision
            Destroy(visual.GetComponent<Collider>());

            return visual;
        }
    }
}