using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using NightHunt.Core;

namespace NightHunt.Audio
{
    /// <summary>
    /// AudioManager — Production-grade centralized audio system.
    ///
    /// ARCHITECTURE (mirrors ShiftUI QualityManager pattern):
    ///   • Persistent singleton (DontDestroyOnLoad).
    ///   • Reads/writes volume via PlayerPrefs (same dB formula as QualityManager).
    ///   • AudioMixer exposes: "MasterVol", "MusicVol", "SFXVol", "UIVol",
    ///     "WeaponVol", "FootstepVol", "ExplosionVol", "VoiceVol", "AmbienceVol".
    ///   • 2D sources: Music (×2 for crossfade), UI, Voice, Heartbeat (loop).
    ///   • 3D sources: AudioPool3D (16 slots) for all world sounds.
    ///
    /// SETUP (scene: persistent "Systems" GO or AudioManager prefab):
    ///   1. Add AudioManager component → assign NH_Master.mixer + AudioLibrary asset.
    ///   2. AudioManager bootstraps AudioPool3D internally — no extra setup.
    ///   3. Settings panel calls SetVolume(param, 0-1) — mirrors QualityManager.
    ///
    /// USAGE:
    ///   AudioManager.Instance.PlayUI(clip);
    ///   AudioManager.Instance.Play3D(clip, worldPos);
    ///   AudioManager.Instance.PlayMusic(library.bgmMatch);
    ///   AudioManager.Instance.SetVolume("WeaponVol", 0.8f);
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : SingletonPersistent<AudioManager>
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Mixer — assign NH_Master.mixer")]
        [SerializeField] private AudioMixer mixer;

        [Header("Audio Library — assign AudioLibrary.asset")]
        [SerializeField] private AudioLibrary library;

        [Header("Mixer Group References")]
        [Tooltip("SFX/UI group from NH_Master.mixer")]
        [SerializeField] private AudioMixerGroup groupUI;
        [Tooltip("Music group from NH_Master.mixer")]
        [SerializeField] private AudioMixerGroup groupMusic;
        [Tooltip("SFX/Weapon group from NH_Master.mixer")]
        [SerializeField] private AudioMixerGroup groupWeapon;
        [Tooltip("SFX/Footstep group from NH_Master.mixer")]
        [SerializeField] private AudioMixerGroup groupFootstep;
        [Tooltip("SFX/Explosion group from NH_Master.mixer")]
        [SerializeField] private AudioMixerGroup groupExplosion;
        [Tooltip("SFX/Voice group from NH_Master.mixer")]
        [SerializeField] private AudioMixerGroup groupVoice;
        [Tooltip("Ambience group from NH_Master.mixer")]
        [SerializeField] private AudioMixerGroup groupAmbience;

        [Header("Pool")]
        [SerializeField, Range(8, 32)] private int pool3DSize = 16;

        [Header("Music Crossfade")]
        [SerializeField, Min(0.1f)] private float musicFadeDuration = 1.5f;

        // ── Exposed Mixer Param Keys (must match AudioMixer exposed params) ────
        public const string ParamMaster    = "MasterVol";
        public const string ParamMusic     = "MusicVol";
        public const string ParamSFX       = "SFXVol";
        public const string ParamUI        = "UIVol";
        public const string ParamWeapon    = "WeaponVol";
        public const string ParamFootstep  = "FootstepVol";
        public const string ParamExplosion = "ExplosionVol";
        public const string ParamVoice     = "VoiceVol";
        public const string ParamAmbience  = "AmbienceVol";

        // Default volumes (0-1 linear)
        private const float DefaultMaster    = 1.0f;
        private const float DefaultMusic     = 0.7f;
        private const float DefaultSFX       = 1.0f;
        private const float DefaultUI        = 0.8f;
        private const float DefaultWeapon    = 1.0f;
        private const float DefaultFootstep  = 0.8f;
        private const float DefaultExplosion = 1.0f;
        private const float DefaultVoice     = 0.9f;
        private const float DefaultAmbience  = 0.5f;

        // ── Runtime ────────────────────────────────────────────────────────────
        private AudioPool3D _pool3D;

        // 2 music sources for crossfade
        private AudioSource _musicA;
        private AudioSource _musicB;
        private bool        _musicOnA = true;

        // Dedicated 2D sources
        private AudioSource _uiSource;
        private AudioSource _voiceSource;
        private AudioSource _heartbeatSource;

        // Heartbeat state
        private bool _heartbeatActive;

        // Active crossfade coroutine (cancel if new music requested during fade)
        private Coroutine _musicFadeCoroutine;

        /// <summary>Read-only reference to AudioLibrary for components that need clip data.</summary>
        public AudioLibrary Library => library;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            BuildSources();
            _pool3D = new AudioPool3D(pool3DSize, transform);
            LoadAllVolumes();
        }

        private void BuildSources()
        {
            _musicA          = CreateSource2D("MusicA",     groupMusic);
            _musicB          = CreateSource2D("MusicB",     groupMusic);
            _uiSource        = CreateSource2D("UI",         groupUI);
            _voiceSource     = CreateSource2D("Voice",      groupVoice);
            _heartbeatSource = CreateSource2D("Heartbeat",  groupVoice);

            _musicA.loop         = true;
            _musicB.loop         = true;
            _heartbeatSource.loop = true;
        }

        private AudioSource CreateSource2D(string label, AudioMixerGroup group)
        {
            var go = new GameObject(label) { hideFlags = HideFlags.HideInHierarchy };
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend           = 0f;
            src.playOnAwake            = false;
            src.outputAudioMixerGroup  = group;
            return src;
        }

        // ── Volume (mirrors QualityManager dB formula exactly) ─────────────────

        /// <summary>
        /// Set a mixer exposed parameter from a 0–1 linear value.
        /// Persists to PlayerPrefs using key = "NH_Audio_" + param.
        /// </summary>
        public void SetVolume(string exposedParam, float value01)
        {
            float clamped = Mathf.Clamp(value01, 0.001f, 1f);
            mixer.SetFloat(exposedParam, Mathf.Log10(clamped) * 20f);
            PlayerPrefs.SetFloat("NH_Audio_" + exposedParam, value01);
        }

        /// <summary>Return saved 0–1 linear volume for a param.</summary>
        public float GetVolume(string exposedParam, float defaultValue)
            => PlayerPrefs.GetFloat("NH_Audio_" + exposedParam, defaultValue);

        private void LoadAllVolumes()
        {
            ApplyStored(ParamMaster,    DefaultMaster);
            ApplyStored(ParamMusic,     DefaultMusic);
            ApplyStored(ParamSFX,       DefaultSFX);
            ApplyStored(ParamUI,        DefaultUI);
            ApplyStored(ParamWeapon,    DefaultWeapon);
            ApplyStored(ParamFootstep,  DefaultFootstep);
            ApplyStored(ParamExplosion, DefaultExplosion);
            ApplyStored(ParamVoice,     DefaultVoice);
            ApplyStored(ParamAmbience,  DefaultAmbience);
        }

        private void ApplyStored(string param, float defaultVal)
        {
            float stored = PlayerPrefs.GetFloat("NH_Audio_" + param, defaultVal);
            float clamped = Mathf.Clamp(stored, 0.001f, 1f);
            mixer.SetFloat(param, Mathf.Log10(clamped) * 20f);
        }

        // ── 2D Playback ────────────────────────────────────────────────────────

        /// <summary>Play a one-shot UI sound (2D, non-interruptible).</summary>
        public void PlayUI(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _uiSource == null) return;
            _uiSource.PlayOneShot(clip, volume);
        }

        /// <summary>Play a voice/announcer clip (2D, interrupts previous).</summary>
        public void PlayAnnouncer(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _voiceSource == null) return;
            _voiceSource.Stop();
            _voiceSource.clip   = clip;
            _voiceSource.volume = volume;
            _voiceSource.Play();
        }

        // ── Music (crossfade dual-source) ──────────────────────────────────────

        /// <summary>
        /// Crossfade to a new music track.
        /// Pass null to fade out current music silently.
        /// </summary>
        public void PlayMusic(AudioClip clip, float fadeDuration = -1f)
        {
            if (fadeDuration < 0f) fadeDuration = musicFadeDuration;

            if (_musicFadeCoroutine != null)
                StopCoroutine(_musicFadeCoroutine);

            _musicFadeCoroutine = StartCoroutine(CrossfadeMusic(clip, fadeDuration));
        }

        /// <summary>Fade out current music and stop.</summary>
        public void StopMusic(float fadeDuration = -1f)
            => PlayMusic(null, fadeDuration < 0f ? musicFadeDuration : fadeDuration);

        private IEnumerator CrossfadeMusic(AudioClip incoming, float duration)
        {
            AudioSource outSrc = _musicOnA ? _musicA : _musicB;
            AudioSource inSrc  = _musicOnA ? _musicB : _musicA;
            _musicOnA = !_musicOnA;

            if (incoming != null)
            {
                inSrc.clip   = incoming;
                inSrc.volume = 0f;
                inSrc.Play();
            }

            float elapsed = 0f;
            float startOutVol = outSrc.volume;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                outSrc.volume = Mathf.Lerp(startOutVol, 0f, t);
                if (incoming != null)
                    inSrc.volume = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }

            outSrc.Stop();
            outSrc.volume = 1f;
            _musicFadeCoroutine = null;
        }

        // ── 3D World Sounds ────────────────────────────────────────────────────

        /// <summary>
        /// Play a 3D pooled sound at a world position.
        /// group: use AudioManager.GetGroup_*() helpers or pass mixer group directly.
        /// </summary>
        public void Play3D(AudioClip clip, Vector3 worldPos,
                           AudioMixerGroup group = null,
                           float volume = 1f,
                           float pitch  = 1f)
        {
            if (clip == null) return;
            _pool3D.Play(clip, worldPos, group ?? groupWeapon, volume, pitch);
        }

        // ── Group Accessors (for components that assign groups) ────────────────
        public AudioMixerGroup GroupUI        => groupUI;
        public AudioMixerGroup GroupWeapon    => groupWeapon;
        public AudioMixerGroup GroupFootstep  => groupFootstep;
        public AudioMixerGroup GroupExplosion => groupExplosion;
        public AudioMixerGroup GroupVoice     => groupVoice;
        public AudioMixerGroup GroupAmbience  => groupAmbience;
        public AudioMixerGroup GroupMusic     => groupMusic;

        // ── Heartbeat (looping low-health feedback) ────────────────────────────

        /// <summary>Start looping heartbeat. Ignored if already active.</summary>
        public void StartHeartbeat()
        {
            if (_heartbeatActive) return;
            if (library == null || library.lowHealthHeartbeat == null) return;
            _heartbeatSource.clip = library.lowHealthHeartbeat;
            _heartbeatSource.Play();
            _heartbeatActive = true;
        }

        /// <summary>Stop heartbeat loop.</summary>
        public void StopHeartbeat()
        {
            if (!_heartbeatActive) return;
            _heartbeatSource.Stop();
            _heartbeatActive = false;
        }

        // ── Convenience: Library shorthand ────────────────────────────────────

        /// <summary>Play UI click sound.</summary>
        public void PlayUIClick()   => PlayUI(library?.uiClick);

        /// <summary>Play UI hover sound.</summary>
        public void PlayUIHover()   => PlayUI(library?.uiHover, 0.6f);

        /// <summary>Play notification ping.</summary>
        public void PlayNotification() => PlayUI(library?.uiNotification);

        /// <summary>Play hit marker. isHeadshot plays a special clip.</summary>
        public void PlayHitMarker(bool isHeadshot = false)
        {
            if (library == null) return;
            PlayUI(isHeadshot ? library.hitMarkerHeadshot : library.hitMarkerTick);
        }

        /// <summary>Play kill confirm stinger.</summary>
        public void PlayKillConfirm() => PlayAnnouncer(library?.killConfirm);

        /// <summary>Play multi-kill stinger by streak count (2=double, 3=triple, …).</summary>
        public void PlayMultiKill(int streak) => PlayAnnouncer(library?.GetMultiKillClip(streak));

        /// <summary>Play grenade explosion at world position.</summary>
        public void PlayExplosionGrenade(Vector3 pos)
            => Play3D(library?.explosionGrenade, pos, groupExplosion);

        /// <summary>Play bullet impact at world position.</summary>
        public void PlayBulletImpact(Vector3 pos)
            => Play3D(library?.bulletImpact, pos, groupWeapon);
    }

    // ── AudioPool3D ────────────────────────────────────────────────────────────
    // Defined here (same file as AudioManager) so Unity always compiles it.
    // AudioPool3D.cs is kept as an empty stub to avoid duplicate-class errors.

    /// <summary>
    /// Fixed-size pool of 3D AudioSources. Used internally by AudioManager.
    /// Steals the oldest slot when the pool is exhausted.
    /// </summary>
    public sealed class AudioPool3D
    {
        private readonly AudioSource[] _sources;
        private readonly float[]       _startTimes;
        private readonly int           _size;

        private const float MinDistance = 3f;
        private const float MaxDistance = 80f;

        public AudioPool3D(int size, Transform parent)
        {
            _size       = Mathf.Max(1, size);
            _sources    = new AudioSource[_size];
            _startTimes = new float[_size];

            for (int i = 0; i < _size; i++)
            {
                var go = new GameObject($"Pool3D_{i:00}") { hideFlags = HideFlags.HideInHierarchy };
                go.transform.SetParent(parent, false);

                var src = go.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.rolloffMode  = AudioRolloffMode.Logarithmic;
                src.minDistance  = MinDistance;
                src.maxDistance  = MaxDistance;
                src.playOnAwake  = false;
                src.loop         = false;

                _sources[i]    = src;
                _startTimes[i] = -999f;
            }
        }

        public void Play(AudioClip clip, Vector3 position,
                         AudioMixerGroup group,
                         float volume = 1f,
                         float pitch  = 1f)
        {
            if (clip == null) return;
            int slot = GetFreeSlot();
            var src = _sources[slot];
            src.transform.position    = position;
            src.outputAudioMixerGroup = group;
            src.clip   = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch  = Mathf.Clamp(pitch, -3f, 3f);
            src.Play();
            _startTimes[slot] = Time.realtimeSinceStartup;
        }

        private int GetFreeSlot()
        {
            for (int i = 0; i < _size; i++)
                if (!_sources[i].isPlaying) return i;

            // steal oldest
            int   oldest   = 0;
            float minStart = _startTimes[0];
            for (int i = 1; i < _size; i++)
            {
                if (_startTimes[i] < minStart)
                {
                    minStart = _startTimes[i];
                    oldest   = i;
                }
            }
            return oldest;
        }
    }
}
