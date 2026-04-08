using UnityEngine;
using NightHunt.Utilities;
using NightHunt.Gameplay.Character;

namespace NightHunt.Audio
{
    /// <summary>
    /// CharacterAudioController — footstep, jump, land, roll sounds (3D).
    ///
    /// PLACEMENT:
    ///   Add to the SAME GameObject as BaseCharacterPredictedMovement
    ///   (i.e. the player prefab root or "Movement" child GO).
    ///
    /// FOOTSTEP TRIGGERS (two methods — use whichever fits your animator setup):
    ///   METHOD A — Animation Events (RECOMMENDED, most accurate timing):
    ///     In the Walk/Run Animator clip → add Event at foot-strike frame:
    ///       Function: "OnAnimEventFootstep"   (no parameters)
    ///     CharacterAnimationController's animator must have this component
    ///     reachable — place both on same GO or use GetComponentInParent.
    ///
    ///   METHOD B — Time-based (fallback if anim events not set up yet):
    ///     Set footstepMethod = TimeBased in Inspector.
    ///     Controller fires footsteps based on movement speed intervals.
    ///
    /// MULTIPLAYER:
    ///   Works on ALL clients automatically:
    ///   - Animator sync (FishNet NetworkAnimator) plays footstep events on remote
    ///     clients too → sounds fire at correct position with 3D spatialBlend.
    ///   - Jump/Roll events fire via BaseCharacterPredictedMovement C# events
    ///     which are currently owner-only. For remote clients jumping, fire
    ///     a separate ObserversRpc in BaseCharacterPredictedMovement (future).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAudioController : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Footstep Method")]
        [Tooltip("AnimEvent = recommended (accurate). TimeBased = fallback (no anim event needed).")]
        [SerializeField] private FootstepMethod footstepMethod = FootstepMethod.AnimEvent;

        [Header("Time-Based Footstep (only used if FootstepMethod == TimeBased)")]
        [Tooltip("Seconds between footstep sounds at walk speed.")]
        [SerializeField, Min(0.1f)] private float walkStepInterval   = 0.45f;
        [Tooltip("Seconds between footstep sounds at run speed.")]
        [SerializeField, Min(0.05f)] private float runStepInterval   = 0.36f;
        [Tooltip("Seconds between footstep sounds at sprint speed.")]
        [SerializeField, Min(0.05f)] private float sprintStepInterval = 0.28f;
        [Tooltip("Minimum speed (m/s) to trigger any footstep sound.")]
        [SerializeField, Min(0f)] private float minMoveSpeed = 0.5f;
        [Tooltip("Speed (m/s) at or above which movement is treated as Run (not Walk).")]
        [SerializeField, Min(0f)] private float runSpeedThreshold    = 3.5f;
        [Tooltip("Speed (m/s) at or above which movement is treated as Sprint.")]
        [SerializeField, Min(0f)] private float sprintSpeedThreshold = 5.5f;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float footstepVolume  = 0.75f;
        [SerializeField, Range(0f, 1f)] private float jumpVolume      = 0.8f;
        [SerializeField, Range(0f, 1f)] private float landVolume      = 0.9f;
        [SerializeField, Range(0f, 1f)] private float rollVolume      = 0.7f;

        [Header("Pitch Variance")]
        [Tooltip("±pitch range for footsteps — adds subtle realism without feeling fake.")]
        [SerializeField, Range(0f, 0.15f)] private float pitchVariance = 0.08f;

        // ── Runtime ────────────────────────────────────────────────────────────
        private BaseCharacterPredictedMovement _movement;
        private PlayerModelLoader              _modelLoader;
        private Transform                      _footLeft;          // bound via CharacterRig at runtime
        private Transform                      _footRight;         // bound via CharacterRig at runtime
        private float _stepTimer;
        private bool  _lastLeft = true;   // alternates per time-based step
        private float _rollSuppressTimer; // suppresses footsteps for roll duration

        // ── Debug ──────────────────────────────────────────────────────────────
        // Filter tag: "[CAC]"  — use Console search box or:
        //   Debug.Log filter: "[CAC]"
        [Header("Debug")]
        [SerializeField] private bool _debugLog;
        private void Log(string msg)   { if (_debugLog) Debug.Log   ($"[CAC] {msg}",  this); }
        private void LogWarn(string msg){ if (_debugLog) Debug.LogWarning($"[CAC] {msg}", this); }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _movement = ComponentResolver.Find<BaseCharacterPredictedMovement>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[CharacterAudioController] BaseCharacterPredictedMovement not found — jump/roll audio disabled")
                .Resolve();

            _modelLoader = ComponentResolver.Find<PlayerModelLoader>(this)
                .OnSelf().OnRoot().InRootChildren()
                .OrLogWarning("[CharacterAudioController] PlayerModelLoader not found — foot bones won't be auto-bound")
                .Resolve();

            if (_modelLoader != null)
            {
                _modelLoader.OnModelReady += BindModel;
                // Handle case where model was already loaded before this Awake ran
                if (_modelLoader.CurrentModelInstance != null)
                    BindModel(_modelLoader.CurrentModelInstance);
            }
        }

        private void OnEnable()
        {
            if (_movement != null)
            {
                _movement.OnJumpTriggered += HandleJump;
                _movement.OnRollTriggered += HandleRoll;
            }
        }

        private void OnDisable()
        {
            if (_movement != null)
            {
                _movement.OnJumpTriggered -= HandleJump;
                _movement.OnRollTriggered -= HandleRoll;
            }
        }

        private void OnDestroy()
        {
            if (_modelLoader != null)
                _modelLoader.OnModelReady -= BindModel;
        }

        private void Update()
        {
            _rollSuppressTimer = Mathf.Max(0f, _rollSuppressTimer - Time.deltaTime);

            if (footstepMethod != FootstepMethod.TimeBased) return;
            if (!AudioManager.HasInstance) return;
            if (_movement == null) return;

            // Suppress footsteps when airborne or rolling
            bool grounded = _movement.IsGroundedPublic;
            if (!grounded || _rollSuppressTimer > 0f)
            {
                _stepTimer = 0f; // reset so first step after landing isn't instant
                return;
            }

            float speed = _movement.GetCurrentMoveSpeed();
            if (speed < minMoveSpeed)
            {
                _stepTimer = 0f;
                return;
            }

            FootstepType type = GetFootstepTypeFromSpeed(speed);
            float interval = type == FootstepType.Sprint ? sprintStepInterval
                           : type == FootstepType.Run    ? runStepInterval
                           : walkStepInterval;

            _stepTimer += Time.deltaTime;
            if (_stepTimer >= interval)
            {
                _stepTimer = 0f;
                Transform foot = _lastLeft ? _footLeft : _footRight;
                _lastLeft = !_lastLeft;
                Log($"TimeBased footstep speed={speed:F1} type={type} foot={(!_lastLeft ? "L" : "R")}");
                PlayFootstep(type, foot);
            }
        }

        // ── Animation Event callbacks (METHOD A) ───────────────────────────────

        /// <summary>
        /// Called by Animator Animation Event on footstrike frame.<br/>
        /// <b>Function name to use in clip:</b> <c>OnAnimEventFootstep</c> (alternates L/R automatically).<br/>
        /// Also called internally by FootstepAnimEventRelay when package clips fire FootStep(string).
        /// </summary>
        public void OnAnimEventFootstep()
        {
            if (!AudioManager.HasInstance) return;
            // Guard: anim blend transitions can fire events 1-2 frames after leaving walk state.
            if (_movement != null && !_movement.IsGroundedPublic) return;
            if (_rollSuppressTimer > 0f) return;

            Transform foot = _lastLeft ? _footLeft : _footRight;
            _lastLeft = !_lastLeft;
            FootstepType type = GetFootstepTypeFromSpeed(_movement?.GetCurrentMoveSpeed() ?? 1f);
            Log($"AnimEvent Footstep type={type} foot={(!_lastLeft ? "L" : "R")}");
            PlayFootstep(type, foot);
        }

        /// <summary>
        /// Called by <see cref="FootstepAnimEventRelay"/> when a package clip fires <c>FootStep(string)</c>.<br/>
        /// The step type is parsed from the animation data string — more precise than speed detection.
        /// </summary>
        public void OnAnimEventFootstepTyped(FootstepType type)
        {
            if (!AudioManager.HasInstance) return;
            if (_movement != null && !_movement.IsGroundedPublic) return;
            if (_rollSuppressTimer > 0f) return;

            Transform foot = _lastLeft ? _footLeft : _footRight;
            _lastLeft = !_lastLeft;
            Log($"AnimEvent FootstepTyped type={type} foot={(!_lastLeft ? "L" : "R")}");
            PlayFootstep(type, foot);
        }

        /// <summary>
        /// Called by Animator Animation Event for LEFT foot explicitly.<br/>
        /// <b>Function name:</b> <c>OnAnimEventFootstepL</c>
        /// </summary>
        public void OnAnimEventFootstepL()
        {
            if (!AudioManager.HasInstance) return;
            if (_movement != null && !_movement.IsGroundedPublic) return;
            if (_rollSuppressTimer > 0f) return;
            FootstepType type = GetFootstepTypeFromSpeed(_movement?.GetCurrentMoveSpeed() ?? 1f);
            Log($"AnimEvent FootstepL type={type}");
            PlayFootstep(type, _footLeft);
        }

        /// <summary>
        /// Called by Animator Animation Event for RIGHT foot explicitly.<br/>
        /// <b>Function name:</b> <c>OnAnimEventFootstepR</c>
        /// </summary>
        public void OnAnimEventFootstepR()
        {
            if (!AudioManager.HasInstance) return;
            if (_movement != null && !_movement.IsGroundedPublic) return;
            if (_rollSuppressTimer > 0f) return;
            FootstepType type = GetFootstepTypeFromSpeed(_movement?.GetCurrentMoveSpeed() ?? 1f);
            Log($"AnimEvent FootstepR type={type}");
            PlayFootstep(type, _footRight);
        }

        // ── Event Handlers ─────────────────────────────────────────────────────

        private void HandleJump()
        {
            if (!AudioManager.HasInstance) return;
            var clip = AudioManager.Instance.Library?.footstepJump;
            if (clip == null) return;

            _stepTimer = 0f; // prevent footstep firing immediately after jump
            Log("Jump sound played");
            AudioManager.Instance.Play3D(clip, transform.position,
                AudioManager.Instance.GroupFootstep,
                jumpVolume,
                RandomPitch());
        }

        private void HandleRoll()
        {
            if (!AudioManager.HasInstance) return;
            var clip = AudioManager.Instance.Library?.footstepRoll;
            if (clip == null) return;

            _rollSuppressTimer = 0.7f; // suppress footsteps for roll duration
            _stepTimer = 0f;
            Log($"Roll sound played, suppressing footsteps for {_rollSuppressTimer}s");
            AudioManager.Instance.Play3D(clip, transform.position,
                AudioManager.Instance.GroupFootstep,
                rollVolume,
                RandomPitch());
        }

        // ── Landing detection ──────────────────────────────────────────────────
        // BaseCharacterPredictedMovement raises OnJumpTriggered for jump start.
        // Landing is detected by watching the grounded state flip in LateUpdate.

        private bool _wasGrounded = true;
        private float _airTime    = 0f;
        private const float MinAirTimeForLand = 0.2f; // ignore tiny bumps

        private void LateUpdate()
        {
            if (_movement == null) return;

            bool grounded = _movement.IsGroundedPublic;

            if (!grounded)
            {
                _airTime += Time.deltaTime;
            }
            else if (!_wasGrounded && _airTime >= MinAirTimeForLand)
            {
                // Just landed
                PlayLand();
                _stepTimer = 0f; // prevent immediate footstep on land
                _airTime   = 0f;
            }

            _wasGrounded = grounded;
        }

        private void PlayLand()
        {
            if (!AudioManager.HasInstance) return;
            var clip = AudioManager.Instance.Library?.GetRandomFootstepLand();
            if (clip == null) return;

            Log($"Land sound played airTime={_airTime:F2}");
            AudioManager.Instance.Play3D(clip, transform.position,
                AudioManager.Instance.GroupFootstep,
                landVolume,
                RandomPitch());
        }

        // ── Model binding ──────────────────────────────────────────────────────

        private void BindModel(GameObject modelRoot)
        {
            // PRIMARY: read from CharacterRig — explicit Inspector-assigned bone refs on the model prefab.
            var rig = modelRoot.GetComponentInChildren<CharacterRig>();
            if (rig != null)
            {
                if (_footLeft  == null) _footLeft  = rig.ankleLeft;
                if (_footRight == null) _footRight = rig.ankleRight;
                Log($"BindModel '{modelRoot.name}' — ankleL={_footLeft?.name ?? "NULL"} ankleR={_footRight?.name ?? "NULL"}");
            }
            else
            {
                Debug.LogWarning($"[CAC] Model '{modelRoot.name}' has no CharacterRig. " +
                                 $"Add CharacterRig to the model prefab and assign ankleLeft/ankleRight. " +
                                 $"Footstep audio will play from character root until fixed.", this);
            }

            if (_footLeft  == null) Debug.LogWarning($"[CAC] ankleLeft not bound on '{modelRoot.name}'.", this);
            if (_footRight == null) Debug.LogWarning($"[CAC] ankleRight not bound on '{modelRoot.name}'.", this);

            // Silence PrCharacter's direct AudioSource footstep bypass.
            var prChar = modelRoot.GetComponentInChildren<PrCharacter>();
            if (prChar != null)
            {
                prChar.Footsteps = System.Array.Empty<AudioClip>();
                Log($"PrCharacter.Footsteps cleared on '{modelRoot.name}'");
            }

            // Add relay: forwards FootStep(string) animation events from model Animator
            // to this controller's OnAnimEventFootstep() — no package modification needed.
            if (modelRoot.GetComponent<FootstepAnimEventRelay>() == null)
            {
                var relay = modelRoot.AddComponent<FootstepAnimEventRelay>();
                relay.audioController = this;
                Log($"FootstepAnimEventRelay added to '{modelRoot.name}'");
            }
        }

        // ── Internals ──────────────────────────────────────────────────────────

        private void PlayFootstep(FootstepType type, Transform foot)
        {
            var lib = AudioManager.Instance.Library;
            if (lib == null) return;

            AudioClip clip = type switch
            {
                FootstepType.Sprint => lib.GetRandomFootstepSprint(),
                FootstepType.Run    => lib.GetRandomFootstepRun(),
                _                   => lib.GetRandomFootstepWalk(),
            };
            if (clip == null) return;

            Vector3 pos = foot != null ? foot.position : transform.position;
            float vol = footstepVolume * (type == FootstepType.Sprint ? 1.15f
                                        : type == FootstepType.Run    ? 1.07f
                                        : 1f);

            Log($"Footstep type={type} clip={clip.name} pos={pos}");
            AudioManager.Instance.Play3D(clip, pos,
                AudioManager.Instance.GroupFootstep,
                Mathf.Min(vol, 1f),
                RandomPitch());
        }

        private FootstepType GetFootstepTypeFromSpeed(float speed)
        {
            if (speed >= sprintSpeedThreshold) return FootstepType.Sprint;
            if (speed >= runSpeedThreshold)    return FootstepType.Run;
            return FootstepType.Walk;
        }

        internal static FootstepType ParseFootstepType(string data)
        {
            if (data == null) return FootstepType.Walk;
            if (data.IndexOf("Sprint", System.StringComparison.OrdinalIgnoreCase) >= 0) return FootstepType.Sprint;
            if (data.IndexOf("Run",    System.StringComparison.OrdinalIgnoreCase) >= 0) return FootstepType.Run;
            return FootstepType.Walk;
        }

        private float RandomPitch()
            => 1f + Random.Range(-pitchVariance, pitchVariance);

        // ── Enums ──────────────────────────────────────────────────────────────

        public enum FootstepMethod { AnimEvent, TimeBased }

        /// <summary>Walk uses lightest clips; Run uses medium; Sprint uses heaviest. Drives AudioLibrary lookup + volume scaling.</summary>
        public enum FootstepType { Walk, Run, Sprint }
    }

    /// <summary>
    /// Added at runtime to the model root by <see cref="CharacterAudioController.BindModel"/>.
    ///
    /// WHY THIS EXISTS:
    ///   <see cref="CharacterAudioController"/> lives on the PLAYER PREFAB ROOT.
    ///   The Animator lives on the MODEL ROOT (a dynamic child).
    ///   Unity Animation Events call SendMessage on the Animator's own GameObject only —
    ///   they do NOT propagate to parents. This relay bridges that gap.
    ///
    /// HANDLES TWO EVENT PATHS:
    ///   1. Package clips (SciFi Soldier) fire <c>FootStep(string)</c> → relay forwards to <c>OnAnimEventFootstep</c>
    ///   2. Custom clips use <c>OnAnimEventFootstepL</c> / <c>OnAnimEventFootstepR</c> directly
    ///      → relay still needed to forward from model GO to player root GO.
    /// </summary>
    internal sealed class FootstepAnimEventRelay : MonoBehaviour
    {
        internal CharacterAudioController audioController;

        // ── Package path: SciFi Soldier animation clips call FootStep(string) ───────────────
        // stepType = animation event data string, e.g. "Rifle Walk", "Rifle Run", "Rifle Sprint"
        public void FootStep(string stepType)
        {
            if (audioController == null) return;
            var type = CharacterAudioController.ParseFootstepType(stepType);
            audioController.OnAnimEventFootstepTyped(type);
        }

        // ── Custom clip path: add Animation Events in Unity Editor calling these names ──────
        public void OnAnimEventFootstep()  => audioController?.OnAnimEventFootstep();
        public void OnAnimEventFootstepL() => audioController?.OnAnimEventFootstepL();
        public void OnAnimEventFootstepR() => audioController?.OnAnimEventFootstepR();
    }
}
