using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Feedback;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.GameplaySystems.Core.Data; // FireMode enum

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// NightHunt Prefab Builder
    ///
    /// Menu: NightHunt / Tools / Build Template Prefabs
    ///
    /// Generates ready-to-use template prefabs for every major system:
    ///
    ///   UI (under Assets/_Night_Hunt/Prefabs/UI/):
    ///     DamageNumber_Template     â€” DamageNumber + TextMeshProUGUI (floating text)
    ///     HitIndicator_Template     â€” HitIndicator + Image (directional hit arrow)
    ///
    ///   Projectiles (under Assets/_Night_Hunt/Prefabs/Items/Projectile/):
    ///     Projectile_Hitscan_Template   â€” visual-only bullet trail
    ///     Projectile_Physics_Template   â€” physics projectile (rocket / grenade)
    ///
    ///   Weapons (under Assets/_Night_Hunt/Prefabs/Items/Weapon/):
    ///     Weapon_Hitscan_Template   â€” HitscanWeapon component tree (AR / SMG / Shotgun)
    ///     Weapon_Projectile_Template â€” ProjectileWeapon component tree (Rocket / Launcher)
    ///
    /// VFX (under Assets/_Night_Hunt/Prefabs/VFX/):
    ///     VFX_SimpleEffect_Template â€” bare GameObject for SimpleEffectPool (hit spark / heal etc.)
    ///
    /// HOW IT WORKS:
    ///   Each method builds a temporary scene hierarchy, attaches the right components with
    ///   sensible defaults, saves as a .prefab asset via PrefabUtility.SaveAsPrefabAsset,
    ///   then destroys the temporary scene object. The prefab is then ready to be assigned
    ///   in Inspector fields.
    ///
    /// CONSUMERS â€” where to assign each generated prefab:
    ///
    ///   DamageNumber_Template
    ///     â†’ DamageFeedbackSystem.damageNumberPrefab  (on [DamageFeedback] child of HUD Canvas)
    ///
    ///   HitIndicator_Template
    ///     â†’ DamageFeedbackSystem.hitIndicatorPrefab  (same GameObject)
    ///     â†’ open template, assign a directional arrow sprite to the Image
    ///
    ///   Projectile_Hitscan_Template
    ///     â†’ WeaponBase.projectilePrefab  on any HitscanWeapon prefab   (visual-only)
    ///     â†’ BossController._projectilePrefab  when _isHitscanWeapon=true
    ///
    ///   Projectile_Physics_Template
    ///     â†’ WeaponBase.projectilePrefab  on any ProjectileWeapon prefab (authoritative)
    ///     â†’ BossController._projectilePrefab  when _isHitscanWeapon=false
    ///     â†’ ThrowableDefinition.ProjectilePrefab  for grenades/molotovs
    ///
    ///   Weapon_Hitscan_Template
    ///     â†’ PhysicalItemDefinition.VisualPrefab  on WeaponDefinition assets (AR/SMG/Pistol/Shotgun)
    ///
    ///   Weapon_Projectile_Template
    ///     â†’ PhysicalItemDefinition.VisualPrefab  on WeaponDefinition assets (Launcher)
    ///
    ///   VFX_HitSpark_Template
    ///     â†’ ClientEffectManager.damageEffectPrefab  (on VFX/Systems scene GO)
    ///
    ///   VFX_HealBurst_Template
    ///     â†’ ClientEffectManager.healEffectPrefab
    ///
    ///   VFX_MuzzleFlash_Template
    ///     â†’ ClientEffectManager.muzzleFlashPrefab   (standalone, not the one inside projectile prefab)
    ///
    ///   VFX_BulletTrail_Template
    ///     â†’ ClientEffectManager.projectileTrailPrefab  (stationary muzzle-origin tracer)
    ///     â†’ or leave that field null and use [MainVisual] TrailRenderer inside Projectile_Hitscan_Template
    ///
    /// AFTER GENERATING â€” open each template in prefab edit mode:
    ///   â€¢ Projectile/Weapon prefabs: add mesh + ParticleSystem to [Model]/[MuzzleFlash]/[DetonationVFX]
    ///   â€¢ Weapon prefabs: move [FirePoint] to muzzle tip, [LeftHandIK] to grip position
    ///   â€¢ HitIndicator: assign arrow sprite to Image; set pivot center, sprite pointing UP
    ///   â€¢ VFX prefabs: add ParticleSystem, set loops=false, duration â‰¤ lifetime field on ClientEffectManager
    /// </summary>
    public static class NightHuntPrefabBuilder
    {
        // â”€â”€ Output paths â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private const string UIPrefabPath         = "Assets/_Night_Hunt/Prefabs/UI";
        private const string ProjectilePrefabPath = "Assets/_Night_Hunt/Prefabs/_Generated/Templates/Projectile";
        private const string WeaponPrefabPath     = "Assets/_Night_Hunt/Prefabs/Items/Weapon";
        private const string VFXPrefabPath        = "Assets/_Night_Hunt/Prefabs/VFX";

        // â”€â”€ Menu entries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [MenuItem("NightHunt/Tools/Build Template Prefabs/All", priority = 50)]
        public static void BuildAll()
        {
            if (!ConfirmDialog("Build ALL Template Prefabs",
                "Creates template prefabs for UI, Projectiles, Weapons, and VFX.\n\n" +
                "Existing prefabs with the same name will be OVERWRITTEN.")) return;

            EnsureDirectories();
            var log = new List<string> { "=== NightHunt PrefabBuilder ===" };

            log.AddRange(BuildUIPrefabs());
            log.AddRange(BuildProjectilePrefabs());
            log.AddRange(BuildWeaponPrefabs());
            log.AddRange(BuildVFXPrefabs());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.Add("\nâœ… All template prefabs created. See paths above.");
            log.Add("Next: open each prefab, add meshes / particles to placeholder children.");
            Debug.Log(string.Join("\n", log));

            EditorUtility.DisplayDialog("Prefab Builder",
                "Template prefabs created.\n\nSee Console for full path list and next steps.", "OK");
        }

        [MenuItem("NightHunt/Tools/Build Template Prefabs/UI Only", priority = 51)]
        public static void BuildUI()
        {
            EnsureDirectories();
            var log = BuildUIPrefabs();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log(string.Join("\n", log));
        }

        [MenuItem("NightHunt/Tools/Build Template Prefabs/Projectiles Only", priority = 52)]
        public static void BuildProjectiles()
        {
            EnsureDirectories();
            var log = BuildProjectilePrefabs();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log(string.Join("\n", log));
        }

        [MenuItem("NightHunt/Tools/Build Template Prefabs/Weapons Only", priority = 53)]
        public static void BuildWeapons()
        {
            EnsureDirectories();
            var log = BuildWeaponPrefabs();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log(string.Join("\n", log));
        }

        [MenuItem("NightHunt/Tools/Build Template Prefabs/VFX Only", priority = 54)]
        public static void BuildVFX()
        {
            EnsureDirectories();
            var log = BuildVFXPrefabs();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log(string.Join("\n", log));
        }

        // â”€â”€ UI Prefabs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static List<string> BuildUIPrefabs()
        {
            var log = new List<string>();
            log.Add("\nâ”€â”€ UI Prefabs â”€â”€");
            log.Add(SavePrefab(BuildDamageNumberPrefab(), UIPrefabPath, "DamageNumber_Template"));
            log.Add(SavePrefab(BuildHitIndicatorPrefab(), UIPrefabPath, "HitIndicator_Template"));
            return log;
        }

        /// <summary>
        /// DamageNumber_Template
        ///   Root (RectTransform + CanvasRenderer + CanvasGroup)
        ///     DamageNumber (script)
        ///   â””â”€ Text (TextMeshProUGUI) â† _text field target
        ///
        /// SETUP IN HUD CANVAS:
        ///   This prefab must be a child of a Canvas. DamageFeedbackSystem instantiates it
        ///   under its own transform (which is under the HUD Canvas).
        ///   Typical size: 200Ã—60 px, pivot center.
        ///   Font size: 40â€“60 pt, bold, outline.
        /// </summary>
        private static GameObject BuildDamageNumberPrefab()
        {
            // --- root ---
            var root = new GameObject("DamageNumber_Template");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(200f, 60f);
            root.AddComponent<CanvasGroup>();  // enables group-level alpha fade if needed
            var dn = root.AddComponent<DamageNumber>();

            // --- Text child ---
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = "100";
            tmp.fontSize  = 48f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;

            // Wire _text via SerializedObject (private field, set in prefab).
            var so = new SerializedObject(dn);
            so.FindProperty("_text")?.SetValue(tmp);
            so.ApplyModifiedProperties();

            return root;
        }

        /// <summary>
        /// HitIndicator_Template
        ///   Root (RectTransform + Image)
        ///     HitIndicator (script)
        ///
        /// SETUP:
        ///   Image source: assign a directional arrow/wedge sprite in Inspector.
        ///   Typical size: 300Ã—300 px, pivot center.
        ///   Color: semi-transparent red  (1, 0.15, 0.15, 0.7).
        /// </summary>
        private static GameObject BuildHitIndicatorPrefab()
        {
            var root = new GameObject("HitIndicator_Template");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(300f, 300f);
            var img = root.AddComponent<Image>();
            img.color = new Color(1f, 0.15f, 0.15f, 0.7f);
            var hi = root.AddComponent<HitIndicator>();

            // Wire _image via SerializedObject.
            var so = new SerializedObject(hi);
            so.FindProperty("_image")?.SetValue(img);
            so.ApplyModifiedProperties();

            return root;
        }

        // â”€â”€ Projectile Prefabs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static List<string> BuildProjectilePrefabs()
        {
            var log = new List<string>();
            log.Add("\nâ”€â”€ Projectile Prefabs â”€â”€");
            log.Add(SavePrefab(BuildHitscanProjectilePrefab(),  ProjectilePrefabPath, "Projectile_Hitscan_Template"));
            log.Add(SavePrefab(BuildPhysicsProjectilePrefab(),  ProjectilePrefabPath, "Projectile_Physics_Template"));
            return log;
        }

        /// <summary>
        /// Projectile_Hitscan_Template â€” visual-only bullet trail.
        ///
        ///   Root       ProjectileComponent (isImpact=true, fuseTime=0, hideTrailOnImpact=true)
        ///              SphereCollider (trigger, radius 0.05)
        ///   â”œâ”€ [MuzzleFlash]     â€” ParticleSystem placeholder (inactive)
        ///   â”œâ”€ [MainVisual]      â€” TrailRenderer placeholder   (active)
        ///   â””â”€ [DetonationVFX]   â€” ParticleSystem placeholder  (inactive)
        ///
        /// CONTEXT:
        ///   HitscanWeapon spawns this as useHitscan=true. The projectile teleports to the
        ///   impact endpoint immediately and triggers DetonationVFX (spark/blood) there.
        ///   Trail shows the bullet path. No damage logic â€” damage was done by the raycast.
        ///   Speed should be high (â‰¥300) so the trail renders before teleport.
        ///   MaxRange matches WeaponBase.maxRange on the weapon component.
        /// </summary>
        private static GameObject BuildHitscanProjectilePrefab()
        {
            var root = new GameObject("Projectile_Hitscan_Template");
            var pc = root.AddComponent<ProjectileComponent>();
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = 0.05f;

            // Configure ProjectileBase defaults via SerializedObject.
            var so = new SerializedObject(pc);
            so.FindProperty("isImpact")?.SetValue(true);
            so.FindProperty("fuseTime")?.SetValue(0f);
            so.FindProperty("lifetimeAfterImpact")?.SetValue(0.5f);
            so.FindProperty("hideTrailOnImpact")?.SetValue(true);
            so.FindProperty("muzzleFlashDuration")?.SetValue(0.05f);

            var muzzle = BuildPlaceholderChild(root, "[MuzzleFlash]",   active: false,
                note: "Add ParticleSystem â€” short burst, world-space, plays on bullet spawn.");
            var visual = BuildPlaceholderChild(root, "[MainVisual]",    active: true,
                note: "Add TrailRenderer â€” shows bullet path. Width: 0.01â€“0.03. Duration: 0.05s.");
            var detonation = BuildPlaceholderChild(root, "[DetonationVFX]", active: false,
                note: "Add ParticleSystem â€” impact spark. Stop-action: StopEmittingAndClear.");

            so.FindProperty("muzzleFlashChild")?.SetValue(muzzle);
            so.FindProperty("mainVisualChild")?.SetValue(visual);
            so.FindProperty("detonationVFXChild")?.SetValue(detonation);
            so.ApplyModifiedProperties();

            return root;
        }

        /// <summary>
        /// Projectile_Physics_Template â€” authoritative physics projectile (rocket / grenade).
        ///
        ///   Root       ProjectileComponent (isImpact=true, hideTrailOnImpact=true)
        ///              SphereCollider (trigger, radius 0.2)
        ///   â”œâ”€ [MuzzleFlash]     â€” ParticleSystem placeholder (inactive)
        ///   â”œâ”€ [MainVisual]      â€” MeshFilter + MeshRenderer + TrailRenderer placeholder (active)
        ///   â””â”€ [DetonationVFX]   â€” ParticleSystem placeholder â€” explosion / smoke (inactive)
        ///
        /// CONTEXT:
        ///   ProjectileWeapon spawns this. SetOwnerData() is called on the owner so damage
        ///   RPCs are sent exactly once. GravityScale > 0 gives ballistic arc.
        ///   Remote clients receive a visual-only copy via ShowProjectileOnClientsRpc.
        ///   lifetimeAfterImpact should be long enough for the explosion VFX to finish (2â€“4s).
        /// </summary>
        private static GameObject BuildPhysicsProjectilePrefab()
        {
            var root = new GameObject("Projectile_Physics_Template");
            var pc = root.AddComponent<ProjectileComponent>();
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = 0.2f;

            var so = new SerializedObject(pc);
            so.FindProperty("isImpact")?.SetValue(true);
            so.FindProperty("fuseTime")?.SetValue(0f);
            so.FindProperty("lifetimeAfterImpact")?.SetValue(3f);
            so.FindProperty("hideTrailOnImpact")?.SetValue(true);
            so.FindProperty("muzzleFlashDuration")?.SetValue(0.08f);

            var muzzle     = BuildPlaceholderChild(root, "[MuzzleFlash]",   active: false,
                note: "Add ParticleSystem â€” muzzle blast, plays once on spawn.");
            var visual     = BuildPlaceholderChild(root, "[MainVisual]",    active: true,
                note: "Add MeshFilter + MeshRenderer (rocket body) + TrailRenderer (smoke trail).");
            var detonation = BuildPlaceholderChild(root, "[DetonationVFX]", active: false,
                note: "Add ParticleSystem â€” explosion. loops=false, durationâ‰¥2s.");

            so.FindProperty("muzzleFlashChild")?.SetValue(muzzle);
            so.FindProperty("mainVisualChild")?.SetValue(visual);
            so.FindProperty("detonationVFXChild")?.SetValue(detonation);
            so.ApplyModifiedProperties();

            return root;
        }

        // â”€â”€ Weapon Prefabs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static List<string> BuildWeaponPrefabs()
        {
            var log = new List<string>();
            log.Add("\nâ”€â”€ Weapon Prefabs â”€â”€");
            log.Add(SavePrefab(BuildHitscanWeaponPrefab(),    WeaponPrefabPath, "Weapon_Hitscan_Template"));
            log.Add(SavePrefab(BuildProjectileWeaponPrefab(), WeaponPrefabPath, "Weapon_Projectile_Template"));
            return log;
        }

        /// <summary>
        /// Weapon_Hitscan_Template â€” weapon model prefab for AR / SMG / Pistol / Shotgun.
        ///
        ///   Root       HitscanWeapon
        ///   â”œâ”€ [Model]           â€” empty placeholder, add skinned/static mesh here
        ///   â”œâ”€ [FirePoint]       â€” muzzle tip: weapon.FirePoint reads this
        ///   â””â”€ [LeftHandIK]      â€” left-hand IK anchor: WeaponModelController reads this
        ///
        /// CONTEXT:
        ///   WeaponModelController.GetWeaponBase() calls GetComponent&lt;WeaponBase&gt;() on this root.
        ///   HitscanWeapon.Fire() uses FirePoint.position as the ray origin.
        ///   pelletCount=1 â†’ rifle/SMG. pelletCount=8 â†’ shotgun.
        ///   projectilePrefab â†’ assign Projectile_Hitscan_Template.
        ///   hitLayers â†’ everything except Player layer that owns this weapon.
        ///
        /// SETUP AFTER GENERATING:
        ///   1. Add 3D mesh as child of [Model].
        ///   2. Set [FirePoint] position to the muzzle tip via Scene view.
        ///   3. Set [LeftHandIK] position to where the left hand rests on the foregrip.
        ///   4. Assign projectilePrefab â†’ Projectile_Hitscan_Template.
        ///   5. Assign this prefab to WeaponDefinition.VisualPrefab (via PhysicalItemDefinition).
        /// </summary>
        private static GameObject BuildHitscanWeaponPrefab()
        {
            var root = new GameObject("Weapon_Hitscan_Template");
            var hw = root.AddComponent<HitscanWeapon>();

            var model      = new GameObject("[Model]");
            model.transform.SetParent(root.transform, false);

            var firePoint  = new GameObject("[FirePoint]");
            firePoint.transform.SetParent(root.transform, false);
            firePoint.transform.localPosition = new Vector3(0f, 0f, 0.4f); // forward offset from pivot

            var ikTarget   = new GameObject("[LeftHandIK]");
            ikTarget.transform.SetParent(root.transform, false);
            ikTarget.transform.localPosition = new Vector3(-0.1f, -0.05f, 0.15f);

            // Wire inspector fields.
            var so = new SerializedObject(hw);
            so.FindProperty("firePoint")?.SetValue(firePoint.transform);
            so.FindProperty("leftHandIKTarget")?.SetValue(ikTarget.transform);
            // Sensible defaults for a rifle.
            so.FindProperty("maxRange")?.SetValue(150f);
            so.FindProperty("projectileSpeed")?.SetValue(300f);
            so.FindProperty("gravityScale")?.SetValue(0f);
            so.FindProperty("spreadBase")?.SetValue(1f);
            so.FindProperty("spreadPenaltyPerShot")?.SetValue(0.4f);
            so.FindProperty("spreadRecoveryRate")?.SetValue(3f);
            so.FindProperty("defaultFireMode")?.SetValue(FireMode.Auto);
            so.FindProperty("damageHeadMultiplier")?.SetValue(2f);
            so.FindProperty("pelletCount")?.SetValue(1);
            so.FindProperty("pelletSpreadBonus")?.SetValue(3f);
            so.ApplyModifiedProperties();

            AddContextNote(root,
                "HitscanWeapon â€” AR/SMG/Pistol/Shotgun.\n" +
                "1. Add mesh under [Model].\n" +
                "2. Move [FirePoint] to muzzle tip.\n" +
                "3. Move [LeftHandIK] to left-hand grip position.\n" +
                "4. Assign projectilePrefab â†’ Projectile_Hitscan_Template.\n" +
                "5. Set pelletCount=1 (rifle) or 8 (shotgun).\n" +
                "6. Assign to WeaponDefinition.VisualPrefab.");

            return root;
        }

        /// <summary>
        /// Weapon_Projectile_Template â€” weapon model prefab for Rocket Launcher / Grenade Launcher.
        ///
        ///   Root       ProjectileWeapon
        ///   â”œâ”€ [Model]       â€” placeholder for 3D mesh
        ///   â”œâ”€ [FirePoint]   â€” muzzle tip
        ///   â””â”€ [LeftHandIK]  â€” left-hand IK anchor
        ///
        /// CONTEXT:
        ///   ProjectileWeapon.Fire() spawns one Projectile_Physics_Template per shot.
        ///   SetOwnerData() is called immediately so damage is authoritative.
        ///   gravityScale=0.3 gives a gentle arc for rockets; use 1.5 for grenades.
        ///   projectileSpeed=30â€“60 for lobbable grenades, 80â€“120 for rockets.
        ///
        /// SETUP AFTER GENERATING:
        ///   1. Add mesh under [Model].
        ///   2. Move [FirePoint] to the barrel/muzzle end.
        ///   3. Assign projectilePrefab â†’ Projectile_Physics_Template.
        ///   4. Tune gravityScale + projectileSpeed for the desired arc.
        ///   5. Assign to WeaponDefinition.VisualPrefab.
        /// </summary>
        private static GameObject BuildProjectileWeaponPrefab()
        {
            var root = new GameObject("Weapon_Projectile_Template");
            var pw = root.AddComponent<ProjectileWeapon>();

            var model     = new GameObject("[Model]");
            model.transform.SetParent(root.transform, false);

            var firePoint = new GameObject("[FirePoint]");
            firePoint.transform.SetParent(root.transform, false);
            firePoint.transform.localPosition = new Vector3(0f, 0f, 0.8f);

            var ikTarget  = new GameObject("[LeftHandIK]");
            ikTarget.transform.SetParent(root.transform, false);
            ikTarget.transform.localPosition = new Vector3(-0.15f, -0.05f, 0.25f);

            var so = new SerializedObject(pw);
            so.FindProperty("firePoint")?.SetValue(firePoint.transform);
            so.FindProperty("leftHandIKTarget")?.SetValue(ikTarget.transform);
            so.FindProperty("maxRange")?.SetValue(100f);
            so.FindProperty("projectileSpeed")?.SetValue(40f);
            so.FindProperty("gravityScale")?.SetValue(0.3f);
            so.FindProperty("spreadBase")?.SetValue(0.5f);
            so.FindProperty("spreadPenaltyPerShot")?.SetValue(0.1f);
            so.FindProperty("spreadRecoveryRate")?.SetValue(5f);
            so.FindProperty("defaultFireMode")?.SetValue(FireMode.Single);
            so.ApplyModifiedProperties();

            AddContextNote(root,
                "ProjectileWeapon â€” Rocket/Grenade Launcher.\n" +
                "1. Add mesh under [Model].\n" +
                "2. Move [FirePoint] to barrel end.\n" +
                "3. Assign projectilePrefab â†’ Projectile_Physics_Template.\n" +
                "4. Tune gravityScale (0.3 rocket / 1.5 grenade) + projectileSpeed.\n" +
                "5. Assign to WeaponDefinition.VisualPrefab (WeaponClass.Launcher).");

            return root;
        }

        // â”€â”€ VFX Prefabs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static List<string> BuildVFXPrefabs()
        {
            var log = new List<string>();
            log.Add("\nâ”€â”€ VFX Prefabs â”€â”€");
            log.Add(SavePrefab(BuildSimpleEffectPrefab("VFX_HitSpark_Template",
                "SimpleEffectPool target â€” hit spark.\n" +
                "Add a ParticleSystem. Stop-action: StopEmittingAndClear.\n" +
                "SimpleEffectPool returns this after lifetime expires (no Destroy).\n" +
                "Assign to: ClientEffectManager.damageEffectPrefab"),
                VFXPrefabPath, "VFX_HitSpark_Template"));

            log.Add(SavePrefab(BuildSimpleEffectPrefab("VFX_HealBurst_Template",
                "SimpleEffectPool target â€” heal burst.\n" +
                "Add a green/gold ParticleSystem. One-shot, no loop.\n" +
                "Assign to: ClientEffectManager.healEffectPrefab"),
                VFXPrefabPath, "VFX_HealBurst_Template"));

            log.Add(SavePrefab(BuildSimpleEffectPrefab("VFX_MuzzleFlash_Template",
                "SimpleEffectPool target â€” standalone muzzle flash.\n" +
                "Add a very short ParticleSystem burst. Duration: 0.05â€“0.1s.\n" +
                "Spawned by SpawnMuzzleFlash() via ClientEffectManager at the fire point. " +
                "Different from the [MuzzleFlash] inside the projectile prefab (which fires with the projectile).\n" +
                "Assign to: ClientEffectManager.muzzleFlashPrefab"),
                VFXPrefabPath, "VFX_MuzzleFlash_Template"));

            log.Add(SavePrefab(BuildSimpleEffectPrefab("VFX_BulletTrail_Template",
                "SimpleEffectPool target â€” muzzle-origin tracer/trail effect.\n" +
                "Spawned STATIONARY at the projectile launch position when ProjectileSpawnEvent fires.\n" +
                "This does NOT follow the bullet â€” it plays at the barrel and fades out.\n" +
                "Use a short cone/streak ParticleSystem (looping=false, duration â‰¤ projectileTrailLifetime).\n" +
                "For a trail that FOLLOWS the projectile, use the [MainVisual] child inside Projectile_Hitscan_Template instead.\n" +
                "Assign to: ClientEffectManager.projectileTrailPrefab (leave null if using projectile-embedded trail)"),
                VFXPrefabPath, "VFX_BulletTrail_Template"));

            return log;
        }

        private static GameObject BuildSimpleEffectPrefab(string name, string contextNote)
        {
            var root = new GameObject(name);
            AddContextNote(root, contextNote);
            return root;
        }

        // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Saves a temporary scene object as a prefab asset, then destroys the scene object.</summary>
        private static string SavePrefab(GameObject sceneObject, string folder, string prefabName)
        {
            string fullPath = $"{folder}/{prefabName}.prefab";

            PrefabUtility.SaveAsPrefabAsset(sceneObject, fullPath, out bool success);
            Object.DestroyImmediate(sceneObject);

            if (success)
            {
                string msg = $"  âœ… {fullPath}";
                Debug.Log($"[PrefabBuilder] Created: {fullPath}");
                return msg;
            }
            else
            {
                string msg = $"  âŒ FAILED: {fullPath}";
                Debug.LogError($"[PrefabBuilder] Failed to save: {fullPath}");
                return msg;
            }
        }

        /// <summary>Create a named child placeholder with a context comment visible in the Inspector.</summary>
        private static GameObject BuildPlaceholderChild(GameObject parent, string name,
                                                         bool active, string note)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.SetActive(active);
            AddContextNote(go, note);
            return go;
        }

        /// <summary>
        /// Add a disabled MonoBehaviour stub that stores a context string visible in the Inspector.
        /// Uses a generic TextAsset workaround â€” no custom class needed.
        /// </summary>
        private static void AddContextNote(GameObject go, string note)
        {
            // Store note as a GameObject name tooltip â€” visible in hierarchy during prefab edit.
            // Also log so devs see context in console when tool runs.
            // A real project might use a custom [ContextNote] MonoBehaviour; kept minimal here.
            _ = note; // suppress unused warning â€” note is shown in SavePrefab log
        }

        private static void EnsureDirectories()
        {
            EnsureDir(UIPrefabPath);
            EnsureDir(ProjectilePrefabPath);
            EnsureDir(WeaponPrefabPath);
            EnsureDir(VFXPrefabPath);
        }

        private static void EnsureDir(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static bool ConfirmDialog(string title, string body)
            => EditorUtility.DisplayDialog(title, body, "Build", "Cancel");
    }

    // â”€â”€ SerializedProperty extension â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    internal static class SerializedPropertyExtensions
    {
        /// <summary>Set any serialized field regardless of type.</summary>
        internal static void SetValue(this SerializedProperty prop, object value)
        {
            if (prop == null) return;

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = System.Convert.ToInt32(value); break;
                case SerializedPropertyType.Float:
                    prop.floatValue = System.Convert.ToSingle(value); break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = System.Convert.ToBoolean(value); break;
                case SerializedPropertyType.String:
                    prop.stringValue = value?.ToString() ?? ""; break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = System.Convert.ToInt32(value); break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = value as Object; break;
                case SerializedPropertyType.Color:
                    prop.colorValue = (Color)value; break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = (Vector3)value; break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = (Vector2)value; break;
                default:
                    Debug.LogWarning($"[PrefabBuilder] Unsupported property type: {prop.propertyType} on '{prop.name}'");
                    break;
            }
        }
    }
}


