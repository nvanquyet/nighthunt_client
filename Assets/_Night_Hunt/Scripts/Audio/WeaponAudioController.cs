using UnityEngine;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Character.Combat.Weapons;

namespace NightHunt.Audio
{
    /// <summary>
    /// Weapon audio — subscribes to IWeaponSystem events and plays 3D sounds via AudioManager.
    ///
    /// PLACEMENT:
    ///   Add to the same GO as WeaponSystem (or a "WeaponSystem" child of the player prefab).
    ///   ComponentResolver auto-finds IWeaponSystem in parent/self hierarchy.
    ///
    /// PROFILES:
    ///   Assign one WeaponAudioProfile per WeaponClass in the Inspector array.
    ///   WeaponAudioController selects the matching profile on weapon change.
    ///   Falls back to DefaultProfile if no match found.
    ///
    /// 2D vs 3D:
    ///   All sounds here are 3D (spatialBlend=1) routed through AudioManager.Play3D().
    ///   The AudioManager pool handles position update per call — no persistent AudioSource needed.
    ///
    /// REMOTE CLIENT SUPPORT:
    ///   IWeaponSystem events fire on the LOCAL owner only for fire/reload.
    ///   Remote-client gunshot sounds: WeaponSystem.ShowProjectileOnClientsRpc already
    ///   handles remote visual — for remote gunshot AUDIO use a separate
    ///   [ObserversRpc] "RpcPlayGunshot(pos, weaponClass)" added to WeaponSystem (Phase 2).
    ///   For Phase 1 this controller covers owner-side audio perfectly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponAudioController : MonoBehaviour
    {
        [Header("Weapon Audio Profiles")]
        [Tooltip("Map WeaponClass to WeaponAudioProfile. Controller finds matching profile on weapon change.")]
        [SerializeField] private WeaponAudioProfileEntry[] profiles;

        [Tooltip("Fallback profile used when no matching WeaponClass found in profiles array.")]
        [SerializeField] private WeaponAudioProfile defaultProfile;

        [Header("Muzzle Reference")]
        [Tooltip("Weapon muzzle transform for 3D audio position. Auto-resolved via WeaponModelController if null.")]
        [SerializeField] private Transform muzzlePoint;

        // ── Runtime ────────────────────────────────────────────────────────────
        private IWeaponSystem        _weaponSystem;
        private WeaponSystem         _weaponSystemConcrete;
        private WeaponModelController _modelController;
        private PlayerModelLoader     _modelLoader;
        private WeaponAudioProfile   _activeProfile;
        private bool                 _isOwner;

        // ── Debug ──────────────────────────────────────────────────────────────
        // Filter tag: "[WAC]"  — use Console search box or:
        //   Debug.Log filter: "[WAC]"
        [Header("Debug")]
        [SerializeField] private bool _debugLog;
        private void Log(string msg)    { if (_debugLog) Debug.Log    ($"[WAC] {msg}", this); }
        private void LogWarn(string msg){ if (_debugLog) Debug.LogWarning($"[WAC] {msg}", this); }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _weaponSystemConcrete = ComponentResolver.Find<WeaponSystem>(this)
                .OnSelf().InParent().InRootChildren()
                .OrLogWarning("[WAC] WeaponSystem not found")
                .Resolve();

            _weaponSystem = _weaponSystemConcrete as IWeaponSystem;

            _modelController = ComponentResolver.Find<WeaponModelController>(this)
                .OnSelf().InParent().InRootChildren()
                .OrDefault(null)
                .Resolve();

            _modelLoader = ComponentResolver.Find<PlayerModelLoader>(this)
                .OnSelf().OnRoot().InRootChildren()
                .OrDefault(null)
                .Resolve();

            if (_modelLoader != null)
            {
                _modelLoader.OnModelReady += BindModel;
                if (_modelLoader.CurrentModelInstance != null)
                    BindModel(_modelLoader.CurrentModelInstance);
            }
            else
            {
                Debug.LogWarning("[WAC] PlayerModelLoader not found — reload animation events won't fire.", this);
            }
        }

        private void OnEnable()
        {
            if (_weaponSystem == null) return;
            _weaponSystem.OnShotFired            += HandleShotFired;
            _weaponSystem.OnReloadStateChanged   += HandleReloadStateChanged;
            _weaponSystem.OnActiveWeaponChanged  += HandleWeaponChanged;
            _weaponSystem.OnWeaponDepleted       += HandleWeaponDepleted;
            _weaponSystem.OnHitscanResult        += HandleHitscanResult;

            if (_modelController != null)
                _modelController.OnWeaponModelChanged += HandleModelChanged;
        }

        private void OnDisable()
        {
            if (_weaponSystem != null)
            {
                _weaponSystem.OnShotFired            -= HandleShotFired;
                _weaponSystem.OnReloadStateChanged   -= HandleReloadStateChanged;
                _weaponSystem.OnActiveWeaponChanged  -= HandleWeaponChanged;
                _weaponSystem.OnWeaponDepleted       -= HandleWeaponDepleted;
                _weaponSystem.OnHitscanResult        -= HandleHitscanResult;
            }

            if (_modelController != null)
                _modelController.OnWeaponModelChanged -= HandleModelChanged;

            if (_modelLoader != null)
                _modelLoader.OnModelReady -= BindModel;
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleShotFired(WeaponSlotType slot, Vector3 aimDir)
        {
            if (_activeProfile == null) return;
            if (!AudioManager.HasInstance) return;

            Vector3 pos = GetMuzzlePosition();
            AudioClip clip = _activeProfile.GetFireClip();
            float pitch    = _activeProfile.GetFirePitch();

            Log($"ShotFired slot={slot} clip={clip?.name ?? "NULL"} pos={pos}");
            AudioManager.Instance.PlayWeapon3D(clip, pos, _activeProfile.fireVolume, pitch);
        }

        private void HandleReloadStateChanged(bool reloading)
        {
            if (_activeProfile == null) return;
            if (!AudioManager.HasInstance) return;

            Vector3 pos = GetMuzzlePosition();

            if (reloading && _activeProfile.reloadStartClip != null)
            {
                AudioManager.Instance.PlayWeapon3D(_activeProfile.reloadStartClip, pos);
            }
            else if (!reloading && _activeProfile.reloadEndClip != null)
            {
                AudioManager.Instance.PlayWeapon3D(_activeProfile.reloadEndClip, pos);
            }
        }

        private void HandleWeaponChanged(WeaponSlotType? prev, WeaponSlotType? next)
        {
            // Resolve profile from active weapon instance when slot changes.
            // Model may not be ready yet — HandleModelChanged also updates muzzle + profile.
            if (_weaponSystem == null) return;
            var active = _weaponSystem.GetActiveWeapon();
            var def = active != null ? ItemDatabase.GetDefinition(active.DefinitionID) : null;
            if (def is WeaponDefinition wd)
                _activeProfile = GetProfile(wd.WeaponClass);

            // Play draw sound if switching to a weapon (not holstering)
            if (next.HasValue && !AudioManager.HasInstance) return;
            if (next.HasValue && _activeProfile?.drawClip != null)
                AudioManager.Instance.PlayWeapon3D(_activeProfile.drawClip, GetMuzzlePosition());
        }

        private void HandleWeaponDepleted(WeaponSlotType slot)
        {
            if (_activeProfile == null || _activeProfile.emptyClip == null) return;
            if (!AudioManager.HasInstance) return;

            Log($"WeaponDepleted slot={slot}");
            AudioManager.Instance.PlayWeapon3D(_activeProfile.emptyClip, GetMuzzlePosition());
        }

        private void HandleHitscanResult(WeaponSlotType slot, Vector3 origin, Vector3 endpoint)
        {
            if (!AudioManager.HasInstance) return;

            // Use per-weapon override if set, otherwise fall back to AudioLibrary.bulletImpact.
            AudioClip impactClip = _activeProfile?.bulletImpactOverride;
            if (impactClip != null)
                AudioManager.Instance.PlayImpact3D(impactClip, endpoint);
            else
                AudioManager.Instance.PlayBulletImpact(endpoint);

            Log($"BulletImpact at {endpoint} clip={(impactClip != null ? impactClip.name : "library default")}");
        }

        // ── Animation Event hooks (called directly from Animator clips) ────────
        //
        // HOW TO USE:
        //   In the Reload animation clip → Animation Events tab:
        //     • Frame where mag DROPS      → Function: "OnAnimEventReloadStart"
        //     • Frame where mag SNAPS IN   → Function: "OnAnimEventReloadInsert"
        //     • Frame where slide RACKS    → Function: "OnAnimEventReloadEnd"
        //
        //   The Animator must be on the same GameObject or a child/parent
        //   reachable by SendMessage (Unity default for Animation Events).
        //   Because WeaponAudioController is on the player root and the
        //   animator may be on a child, add [AnimEventRelay] (see note below)
        //   or move all anim clips to call SendMessageUpwards.
        //
        //   RECOMMENDED: Set "Send Message" receiver to "Fire And Forget" on
        //   the Animation Event so missing receivers don't spam warnings.
        //
        // NOTE ON TIMER vs ANIMATION EVENT:
        //   HandleReloadStateChanged(bool) is still the fallback (fires when
        //   WeaponSystem coroutine starts/ends). If animation events are set up,
        //   OnAnimEventReloadInsert/End fire at the exact frame — more accurate.
        //   Both paths play the same clips; whichever fires last wins (no harm).

        /// <summary>
        /// Optional AnimEvent — play reloadStartClip at the exact mag-drop frame.
        /// Leave unused if HandleReloadStateChanged(true) timing is already good.
        /// </summary>
        public void OnAnimEventReloadStart()
        {
            if (_activeProfile?.reloadStartClip == null) return;
            if (!AudioManager.HasInstance) return;
            Log("AnimEvent: ReloadStart");
            AudioManager.Instance.PlayWeapon3D(_activeProfile.reloadStartClip, GetMuzzlePosition());
        }

        /// <summary>
        /// AnimEvent — play reloadEndClip at the frame the new mag snaps in
        /// (or the bolt/slide racks). This is more accurate than the coroutine
        /// timer used by HandleReloadStateChanged(false).
        /// </summary>
        public void OnAnimEventReloadInsert()
        {
            if (_activeProfile == null) return;
            if (!AudioManager.HasInstance) return;

            var clip = _activeProfile.reloadTacticalClip != null
                ? _activeProfile.reloadTacticalClip
                : _activeProfile.reloadEndClip;

            if (clip == null) return;
            Log($"AnimEvent: ReloadInsert clip={clip.name}");
            AudioManager.Instance.PlayWeapon3D(clip, GetMuzzlePosition());
        }

        public void OnAnimEventReloadEnd()
        {
            if (_activeProfile?.reloadEndClip == null) return;
            if (!AudioManager.HasInstance) return;
            Log("AnimEvent: ReloadEnd");
            AudioManager.Instance.PlayWeapon3D(_activeProfile.reloadEndClip, GetMuzzlePosition());
        }

        // OnWeaponModelChanged fires with WeaponBase (null = holstered)
        private void HandleModelChanged(WeaponBase weaponBase)
        {
            if (weaponBase == null) return;

            // Update muzzle position from weapon model's FirePoint
            muzzlePoint = weaponBase.FirePoint;

            // Re-resolve profile (in case HandleWeaponChanged fired before model was ready)
            if (_weaponSystem == null) return;
            var active = _weaponSystem.GetActiveWeapon();
            var def = active != null ? ItemDatabase.GetDefinition(active.DefinitionID) : null;
            if (def is WeaponDefinition wd)
            {
                _activeProfile = GetProfile(wd.WeaponClass);
                Log($"WeaponModel changed: class={wd.WeaponClass} profile={_activeProfile?.name ?? "NULL"} muzzle={muzzlePoint?.name}");
            }
        }

        // ── Model binding (adds WeaponAnimEventRelay to model root) ───────────────────

        private void BindModel(GameObject modelRoot)
        {
            // WeaponAudioController lives on the player prefab root.
            // Reload animation events fire from the Animator on modelRoot (child GO).
            // Unity SendMessage only targets the GO with the Animator — NOT parents.
            // Solution: add a relay component to modelRoot that forwards the anim events up.
            if (modelRoot.GetComponent<WeaponAnimEventRelay>() == null)
            {
                var relay = modelRoot.AddComponent<WeaponAnimEventRelay>();
                relay.audioController = this;
                Log($"WeaponAnimEventRelay added to '{modelRoot.name}'");
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private Vector3 GetMuzzlePosition()
            => muzzlePoint != null ? muzzlePoint.position : transform.position;

        private WeaponAudioProfile GetProfile(WeaponClass wc)
        {
            if (profiles != null)
            {
                foreach (var entry in profiles)
                {
                    if (entry != null && entry.weaponClass == wc && entry.profile != null)
                        return entry.profile;
                }
            }
            return defaultProfile;
        }

        // ── Nested types ───────────────────────────────────────────────────────

        [System.Serializable]
        public class WeaponAudioProfileEntry
        {
            public WeaponClass          weaponClass;
            public WeaponAudioProfile   profile;
        }
    }

    /// <summary>
    /// Added at runtime to the model root by <see cref="WeaponAudioController.BindModel"/>.
    /// Receives reload Animation Events from the Animator on the model GO and forwards
    /// them to WeaponAudioController on the player prefab root.
    /// Unity SendMessage doesn't propagate upward — this relay bridges the gap without
    /// modifying any package scripts.
    /// </summary>
    internal sealed class WeaponAnimEventRelay : MonoBehaviour
    {
        internal WeaponAudioController audioController;

        // These method names must exactly match the Animation Event "Function" field
        // set in the Reload animation clip(s).
        public void OnAnimEventReloadStart()  => audioController?.OnAnimEventReloadStart();
        public void OnAnimEventReloadInsert() => audioController?.OnAnimEventReloadInsert();
        public void OnAnimEventReloadEnd()    => audioController?.OnAnimEventReloadEnd();
    }
}
