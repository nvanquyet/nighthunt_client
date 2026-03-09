using UnityEngine;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Drives the player Animator each frame from movement state.
    /// Uses transform position-delta as velocity source so it works correctly
    /// for BOTH the owner (client-predicted) and remote clients (position
    /// replicated by FishNet's NetworkTransform).
    ///
    /// EXPECTED ANIMATOR PARAMETERS (create these in your Animator Controller):
    ///   Float  "Speed"       — horizontal speed in m/s
    ///   Bool   "OnGround"    — true while grounded
    ///   Float  "VerticalVel" — vertical velocity (positive = ascending)
    ///
    /// INSPECTOR SETUP:
    ///   Attach to the root player prefab.
    ///   Also add PlayerModelLoader to the root \u2014 it will inject _actorUtils at runtime.
    ///   DO NOT drag PrActorUtils in the Inspector; it lives on the dynamically-spawned
    ///   model child and is bound automatically via PlayerModelLoader.OnModelReady.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationController : MonoBehaviour
    {
        // NOT a SerializeField \u2014 bound at runtime via PlayerModelLoader.OnModelReady.
        private PrActorUtils _actorUtils;

        [Header("Ground Detection")]
        [Tooltip("Sphere radius for ground check cast from the character base.")]
        [SerializeField] private float _groundCheckRadius = 0.25f;
        [SerializeField] private float _groundCheckOffset = 0.05f;
        [SerializeField] private LayerMask _groundLayers = ~0;

        [Header("Smoothing")]
        [Tooltip("Speed parameter is lerped each frame to avoid animation snapping.")]
        [SerializeField] private float _speedSmoothing = 12f;

        // Cached Animator parameter hashes (avoids string lookup each frame).
        private static readonly int SpeedHash    = Animator.StringToHash("Speed");
        private static readonly int OnGroundHash = Animator.StringToHash("OnGround");
        private static readonly int VerticalHash = Animator.StringToHash("VerticalVel");

        private Vector3 _prevPosition;
        private float   _smoothedSpeed;
        private bool    _started;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            // Subscribe to model loader so we get PrActorUtils once the mesh is ready.
            var modelLoader = GetComponent<PlayerModelLoader>();
            if (modelLoader != null)
                modelLoader.OnModelReady += BindModel;
            else
                Debug.LogWarning("[CharacterAnimationController] PlayerModelLoader not found on root. " +
                                 "Animation will not drive until model is bound manually.");
        }

        private void OnDestroy()
        {
            var modelLoader = GetComponent<PlayerModelLoader>();
            if (modelLoader != null)
                modelLoader.OnModelReady -= BindModel;
        }

        private void Start()
        {
            _prevPosition = transform.position;
            _started      = true;
        }

        /// <summary>
        /// Called by PlayerModelLoader.OnModelReady once the character mesh is instantiated.
        /// Extracts PrActorUtils (which holds the Animator reference) from the model root.
        /// </summary>
        private void BindModel(GameObject modelRoot)
        {
            _actorUtils = modelRoot.GetComponentInChildren<PrActorUtils>(true);
            if (_actorUtils == null)
                Debug.LogWarning($"[CharacterAnimationController] PrActorUtils not found on model '{modelRoot.name}'. " +
                                 "Animator parameters will not be driven.");
        }

        private void Update()
        {
            if (!_started) return;

            Animator anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                _prevPosition = transform.position;
                return;
            }

            // -- Velocity from position delta (works for owner + remote) -------
            Vector3 delta  = transform.position - _prevPosition;
            float   hSpeed = new Vector3(delta.x, 0f, delta.z).magnitude / dt;
            float   vVel   = delta.y / dt;

            // Smooth horizontal speed to prevent rapid flicker on minor corrections.
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, hSpeed, _speedSmoothing * dt);

            // -- Grounded check ------------------------------------------------
            bool grounded = Physics.CheckSphere(
                transform.position + Vector3.up * _groundCheckOffset,
                _groundCheckRadius,
                _groundLayers,
                QueryTriggerInteraction.Ignore);

            // -- Push to Animator ----------------------------------------------
            anim.SetFloat(SpeedHash,    _smoothedSpeed);
            anim.SetBool (OnGroundHash, grounded);
            anim.SetFloat(VerticalHash, vVel);

            _prevPosition = transform.position;
        }
    }
}
