using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Audio
{
    /// <summary>
    /// Centralized clip library — assign ONE instance as ScriptableObject asset.
    /// AudioManager holds a reference to this; all audio requests go through AudioManager.
    ///
    /// CREATE: Right-click Project → NightHunt/Audio/Audio Library → name it "AudioLibrary"
    /// PLACE:  Assets/_Night_Hunt/Audio/AudioLibrary.asset
    /// ASSIGN: Drag into AudioManager._library field in the Inspector.
    ///
    /// FOOTSTEP RANDOMIZATION:
    ///   Footstep arrays should have at least 4 variants per surface type to avoid repetition.
    ///   CharacterAudioController picks a random index AND applies ±10% pitch shift.
    ///
    /// WEAPON AUDIO:
    ///   Per-weapon class audio is stored in WeaponAudioProfile ScriptableObjects and
    ///   referenced by WeaponAudioController — NOT here.
    ///   This library only stores shared fallback + non-weapon gameplay clips.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "NightHunt/Audio/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        // ── UI Sounds (2D) ──────────────────────────────────────────────────
        [Header("UI Sounds (2D — spatialBlend=0)")]
        [Tooltip("Generic button click — used by UIAudioTrigger on all buttons.")]
        public AudioClip uiClick;

        [Tooltip("Button/element hover (pointer enter).")]
        public AudioClip uiHover;

        [Tooltip("Panel open / screen transition in.")]
        public AudioClip uiPanelOpen;

        [Tooltip("Panel close / screen transition out.")]
        public AudioClip uiPanelClose;

        [Tooltip("Notification ping — friend request, invite, toast.")]
        public AudioClip uiNotification;

        [Tooltip("Countdown beep (lobby countdown, match start 3-2-1).")]
        public AudioClip uiCountdownBeep;

        [Tooltip("Final countdown beep (louder/different pitch for '1').")]
        public AudioClip uiCountdownFinal;

        [Tooltip("Match found / confirm success sound.")]
        public AudioClip uiMatchFound;

        // ── Hit Feedback (2D) ───────────────────────────────────────────────
        [Header("Hit Feedback (2D)")]
        [Tooltip("Short tick when a bullet hits an enemy — local player only.")]
        public AudioClip hitMarkerTick;

        [Tooltip("Distinct sound for headshot kill confirmation.")]
        public AudioClip hitMarkerHeadshot;

        [Tooltip("Played (2D, UI group) when the active weapon's magazine AND reserve ammo are both fully depleted.")]
        public AudioClip weaponDepleted;

        // ── Announcer / Voice (2D) ──────────────────────────────────────────
        [Header("Announcer / Voice (2D)")]
        [Tooltip("'Kill confirmed' or short clip played when local player scores a kill.")]
        public AudioClip killConfirm;

        [Tooltip("Multi-kill announcement (double kill, triple kill…). Index = kill streak - 2.")]
        public AudioClip[] multiKillClips;

        [Tooltip("Looping heartbeat played while health < lowHealthThreshold.")]
        public AudioClip lowHealthHeartbeat;

        [Tooltip("Health percentage threshold (0–1) that activates lowHealthHeartbeat.")]
        [Range(0f, 0.5f)]
        public float lowHealthThreshold = 0.3f;

        [Tooltip("Played on this player's death (client-only, 2D).")]
        public AudioClip playerDeathStinger;

        // ── Background Music (2D) ───────────────────────────────────────────
        [Header("Background Music (2D)")]
        [Tooltip("Home screen background music (lobby, menu).")]
        public AudioClip bgmHome;

        [Tooltip("In-match ambient/action music.")]
        public AudioClip bgmMatch;

        [Tooltip("Intense/boss encounter music — triggered by CombatAudioController.")]
        public AudioClip bgmIntense;

        [Tooltip("End-of-match results screen music.")]
        public AudioClip bgmResults;

        // ── Footstep (3D) ───────────────────────────────────────────────────
        [Header("Footstep (3D — spatialBlend=1)")]
        [Tooltip("Walk cycle footstep variants (min 4 for good randomization).")]
        public AudioClip[] footstepWalk;

        [Tooltip("Run cycle footstep variants — heavier than walk. Falls back to walk if empty.")]
        public AudioClip[] footstepRun;

        [Tooltip("Sprint cycle footstep variants — heaviest impact. Falls back to run → walk if empty.")]
        public AudioClip[] footstepSprint;

        [Tooltip("Landing sound after jump/fall.")]
        public AudioClip[] footstepLand;

        [Tooltip("Jump effort grunt / whoosh.")]
        public AudioClip footstepJump;

        [Tooltip("Roll/dodge landing.")]
        public AudioClip footstepRoll;

        // ── Explosion (3D) ──────────────────────────────────────────────────
        [Header("Explosion (3D — spatialBlend=1)")]
        [Tooltip("Grenade / throwable explosion. Replaces ProjectileNetworked.PlayClipAtPoint.")]
        public AudioClip explosionGrenade;

        [Tooltip("Rocket / large explosion.")]
        public AudioClip explosionRocket;

        [Tooltip("Small impact / bullet hit on hard surface.")]
        public AudioClip bulletImpact;

        // ── Consumable (3D) ─────────────────────────────────────────────────
        [Header("Consumable (3D — spatialBlend=1)")]
        [Tooltip("Generic consumable use (medkit, stim, etc.).")]
        public AudioClip consumableUse;

        [Tooltip("Grenade/throwable pin pull before throw.")]
        public AudioClip throwablePull;

        // ── Ambience (3D or 2D depending on scene) ──────────────────────────
        [Header("Ambience")]
        [Tooltip("Looping environmental ambience placed in scene. Leave null if scene has its own.")]
        public AudioClip[] ambienceVariants;

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Random walk footstep. Fallback chain: walk → run → sprint.
        /// </summary>
        public AudioClip GetRandomFootstepWalk()
        {
            if (footstepWalk   is { Length: > 0 }) return footstepWalk  [Random.Range(0, footstepWalk.Length)];
            if (footstepRun    is { Length: > 0 }) return footstepRun   [Random.Range(0, footstepRun.Length)];
            if (footstepSprint is { Length: > 0 }) return footstepSprint[Random.Range(0, footstepSprint.Length)];
            return null;
        }

        /// <summary>
        /// Random run footstep. Fallback chain: run → sprint → walk.
        /// </summary>
        public AudioClip GetRandomFootstepRun()
        {
            if (footstepRun    is { Length: > 0 }) return footstepRun   [Random.Range(0, footstepRun.Length)];
            if (footstepSprint is { Length: > 0 }) return footstepSprint[Random.Range(0, footstepSprint.Length)];
            if (footstepWalk   is { Length: > 0 }) return footstepWalk  [Random.Range(0, footstepWalk.Length)];
            return null;
        }

        /// <summary>
        /// Random sprint footstep. Fallback chain: sprint → run → walk.
        /// </summary>
        public AudioClip GetRandomFootstepSprint()
        {
            if (footstepSprint is { Length: > 0 }) return footstepSprint[Random.Range(0, footstepSprint.Length)];
            if (footstepRun    is { Length: > 0 }) return footstepRun   [Random.Range(0, footstepRun.Length)];
            if (footstepWalk   is { Length: > 0 }) return footstepWalk  [Random.Range(0, footstepWalk.Length)];
            return null;
        }

        /// <summary>
        /// Random landing clip.
        /// </summary>
        public AudioClip GetRandomFootstepLand()
            => footstepLand is { Length: > 0 }
                ? footstepLand[Random.Range(0, footstepLand.Length)]
                : null;

        /// <summary>
        /// Multi-kill clip by streak count (2=double, 3=triple, etc.). Clamps to array length.
        /// </summary>
        public AudioClip GetMultiKillClip(int killStreak)
        {
            if (multiKillClips == null || multiKillClips.Length == 0) return null;
            int idx = Mathf.Clamp(killStreak - 2, 0, multiKillClips.Length - 1);
            return multiKillClips[idx];
        }
    }
}
