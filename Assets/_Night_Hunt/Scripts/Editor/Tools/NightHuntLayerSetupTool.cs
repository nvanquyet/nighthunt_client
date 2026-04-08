#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NightHunt.Editor.Tools
{
    // ──────────────────────────────────────────────────────────────────────────
    //  NightHuntLayerSetupTool
    //  Menu: NightHunt ▸ Tools ▸ Layer & Tag Setup
    //
    //  ┌─────────────────────────────────────────────────────────────────────┐
    //  │  What this tool does                                                │
    //  ├─────────────────────────────────────────────────────────────────────┤
    //  │  1. Audits ProjectSettings/TagManager.asset for required layers     │
    //  │     and tags, showing OK / MISSING / CONFLICT states.              │
    //  │  2. Applies the FULL canonical layer + tag table in one click.      │
    //  │  3. Writes the COMPLETE physics collision matrix for all custom     │
    //  │     layers — replaces the minimal DemoSceneGenerator rules.        │
    //  │  4. Documents FOG-OF-WAR, MINIMAP, and SEE-THROUGH SHADER layer    │
    //  │     integration rules with in-editor guidance.                      │
    //  └─────────────────────────────────────────────────────────────────────┘
    //
    //  CANONICAL LAYER TABLE
    //  ─────────────────────
    //  Slot  Name             Purpose
    //   0    Default          Unity reserved
    //   1    TransparentFX    Unity reserved
    //   2    Ignore Raycast   Unity reserved
    //   3    (empty)          Unity reserved — leave empty
    //   4    Water            Unity reserved
    //   5    UI               Unity reserved
    //   6    Player           CharacterController physics capsule
    //   7    PlayerHitBox     Per-bone hitbox colliders for damage detection
    //   8    Projectile       Weapon bullets / rockets (ProjectileComponent)
    //   9    Character        Player root — FOW team logic, NavMesh reference
    //  10    Interactable     Pickup triggers, door activators, interactive props
    //  11    Zone             Trigger volumes: lockdown, ZoneBuff, safe-zones
    //  12    Throwable        Grenade / throwable physics bodies (ProjectileNetworked)
    //  13    DeadCharacter    Ragdoll bodies of dead players
    //  14    PlayerCharacter  Player model root (mesh parent, IK rig)
    //  15    MapObstacle      Map-level physics obstacles (covers, crates, barriers)
    //  16-19 (reserved)       Available for future use
    //  20    Items            Dropped world-item 3-D model / physics collider
    //  21    Minimap          Minimap markers (minimap camera only)
    //  22    MapStatic        Static architecture always rendered inside/outside FOW
    //  23    Wall             Map walls — blocks both physics and line-of-sight
    //  24    Ground           Terrain / floor — aim raycast, nav-mesh reference
    //  25    SeeThrough       See-through shader replacement visual meshes
    //  26-31 (reserved)       Available for future use
    //
    //  COLLISION MATRIX RULES  (✓ = collide, ✗ = ignore/pass-through)
    //  See BuildCollisionMatrix() for the full 32×32 specification.
    //
    //  FOW RULES
    //  ─────────
    //  • Layers Wall / Ground / MapStatic / MapObstacle → no FogOfWarHider
    //    → always rendered; FOW render-feature darkens them in unexplored fog.
    //  • Enemy players / NPCs → FogOfWarHider(HiderDisableRenderers)
    //    → completely hidden outside local player's vision cone.
    //  • Same-team players   → FogTeamVisibilityBinder removes the hider
    //    → always visible regardless of FOW.
    //  • Dropped Items       → designer opt-in. Add FogOfWarHider to hide in fog
    //    (typical Battle-Royale: loot is always visible).
    //
    //  MINIMAP CAMERA CULLING MASK
    //  ───────────────────────────
    //  Set MinimapCameraController._minimapCamera.cullingMask to:
    //    Minimap | Ground | Wall | MapStatic | MapObstacle
    //  Do NOT include Player, Projectile, Items etc. (clutters the minimap).
    //  MinimapMarkerController on each player prefab self-assigns the Minimap layer.
    //
    //  SEE-THROUGH SHADER RULES
    //  ────────────────────────
    //  • "Wall"      layer → physics collider that blocks movement (always present).
    //  • "SeeThrough" layer → the replacement *visual* mesh (no physics).
    //     When the camera–player ray passes through a "SeeThrough" mesh, the
    //     STS GlobalShaderReplacement swaps its material to the cutout/transparent version.
    //  • Multi-level floors (player can stand below another floor):
    //     Collider  →  "Ground" or "MapObstacle" (walkable surface)
    //     Visual    →  "SeeThrough" (the ceiling mesh that becomes transparent from below)
    //     STS detects if the player is below the floor mesh and applies cut-out.
    //  • Pure ground/floor the player walks ON → "Ground" only; do NOT add SeeThrough.
    //  • The SeeThroughShaderPlayer component needs "Player" as its detection layer.
    // ──────────────────────────────────────────────────────────────────────────

    public sealed class NightHuntLayerSetupTool : EditorWindow
    {
        // ── Canonical table ────────────────────────────────────────────────
        // (slot, name, description)
        private static readonly (int slot, string name, string desc)[] LayerTable =
        {
            ( 6, "Player",          "Player root + model root + CharacterController capsule"),
            ( 7, "PlayerHitBox",    "Per-bone hitbox colliders — damage detection only"),
            ( 8, "Projectile",      "Weapon bullets / rockets (ProjectileComponent)"),
            (10, "Interactable",    "Pickup triggers, door/switch activators"),
            (11, "Zone",            "Trigger volumes: lockdown, buffs, safe-zones"),
            (12, "Throwable",       "Grenade / throwable physics bodies"),
            (13, "DeadCharacter",   "Ragdoll dead-player bodies"),
            (15, "MapObstacle",     "Map-level physics obstacles (covers, crates)"),
            (20, "Items",           "Dropped world-item 3-D model / physics"),
            (21, "Minimap",         "Minimap markers — minimap camera only"),
            (22, "MapStatic",       "Static architecture — always visible in/out FOW"),
            (23, "Wall",            "Map walls — blocks physics + line-of-sight"),
            (24, "Ground",          "Terrain / floor — aim raycast, nav reference"),
            (25, "SeeThrough",      "See-through shader replacement visual meshes"),
        };

        // Required tags (excluding Unity built-ins)
        private static readonly (string name, string desc)[] TagTable =
        {
            ("Enemy",       "Generic enemy entity (NPC, boss, turret)"),
            ("Pickup",      "Dropped loot / world-spawn item"),
            ("Destroyable", "Destructible prop"),
            ("Usable",      "Map interactable (door, switch)"),
            ("Beacon",      "Deployed RespawnBeacon"),
            ("Vehicle",     "Vehicle entity"),
            ("AIPlayer",    "AI-controlled player-like unit"),
            ("AINeutral",   "AI neutral / wildlife unit"),
        };

        // ── GUI state ──────────────────────────────────────────────────────
        private Vector2 _scroll;
        private string  _log = "";
        private bool    _showFOWNotes;
        private bool    _showSTSNotes;
        private bool    _showMatrixTable;
        private bool    _showLayerAudit = true;
        private bool    _showTagAudit   = true;

        // ── Styles ─────────────────────────────────────────────────────────
        private GUIStyle _okStyle;
        private GUIStyle _warnStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _boxStyle;
        private bool     _stylesInit;

        // ── Open ───────────────────────────────────────────────────────────
        [MenuItem("NightHunt/Tools/Layer & Tag Setup", priority = 10)]
        public static void Open() =>
            GetWindow<NightHuntLayerSetupTool>("Layer & Tag Setup").Show();

        // ── GUI ────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            InitStyles();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("NightHunt — Layer, Tag & Collision Matrix Setup",
                                       _headerStyle);
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(
                "Audits and applies the canonical layer/tag table + full physics collision matrix.\n" +
                "Safe to run multiple times — existing correct entries are kept unchanged.",
                MessageType.None);
            EditorGUILayout.Space(6);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawLayerAudit();
            EditorGUILayout.Space(4);
            DrawTagAudit();
            EditorGUILayout.Space(4);
            DrawActionButtons();
            EditorGUILayout.Space(4);
            DrawCollisionMatrixPreview();
            EditorGUILayout.Space(4);
            DrawFOWNotes();
            EditorGUILayout.Space(4);
            DrawSTSNotes();
            EditorGUILayout.Space(4);
            DrawMinimapNotes();
            EditorGUILayout.Space(4);
            DrawLog();

            EditorGUILayout.EndScrollView();
        }

        // ── Layer audit ────────────────────────────────────────────────────
        private void DrawLayerAudit()
        {
            _showLayerAudit = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showLayerAudit, "  Layer Audit");

            if (_showLayerAudit)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                var (layers, tm) = OpenTagManager();

                foreach (var (slot, name, desc) in LayerTable)
                {
                    string current = GetLayerName(layers, slot);
                    string status;
                    GUIStyle style;

                    if (current == name)
                    { status = "✔ OK";      style = _okStyle; }
                    else if (string.IsNullOrEmpty(current))
                    { status = "⊕ MISSING"; style = _warnStyle; }
                    else
                    { status = $"⚠ CONFLICT  (slot has \"{current}\")"; style = _errorStyle; }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($" [{slot:D2}] {name}", GUILayout.Width(190));
                    EditorGUILayout.LabelField(status, style, GUILayout.Width(240));
                    EditorGUILayout.LabelField(desc, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                tm.Dispose();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ── Tag audit ──────────────────────────────────────────────────────
        private void DrawTagAudit()
        {
            _showTagAudit = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showTagAudit, "  Tag Audit");

            if (_showTagAudit)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                var allTags = new HashSet<string>(InternalEditorUtility.tags);

                foreach (var (name, desc) in TagTable)
                {
                    bool exists = allTags.Contains(name);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"  {name}", GUILayout.Width(140));
                    EditorGUILayout.LabelField(
                        exists ? "✔ OK" : "⊕ MISSING",
                        exists ? _okStyle : _warnStyle,
                        GUILayout.Width(100));
                    EditorGUILayout.LabelField(desc, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ── Action buttons ─────────────────────────────────────────────────
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("  ▶  Apply Layers + Tags + Collision Matrix",
                                 GUILayout.Height(32)))
                ApplyAll();

            GUI.backgroundColor = new Color(0.8f, 0.8f, 0.4f);
            if (GUILayout.Button("Reload Audit", GUILayout.Height(32), GUILayout.Width(120)))
                Repaint();

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Applying is non-destructive for correctly-set layers.\n" +
                "CONFLICT slots (different existing name) will be overwritten — confirm the " +
                "old SciFi-pack layers (Objects, Dynamic, Triggers, Bullets, CameraWall…) " +
                "are not used by any prefab before proceeding.",
                MessageType.Warning);
        }

        // ── Collision matrix preview ───────────────────────────────────────
        private void DrawCollisionMatrixPreview()
        {
            _showMatrixTable = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showMatrixTable, "  Collision Matrix Rules (read-only reference)");

            if (_showMatrixTable)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                EditorGUILayout.LabelField(
                    "✓ = collide (physics interaction)    ✗ = ignore (no physics contact)",
                    EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(4);

                var rules = GetCollisionRules();
                foreach (var (a, b, collide, note) in rules)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(
                        $"  {(collide ? "✓" : "✗")}",
                        collide ? _okStyle : _warnStyle,
                        GUILayout.Width(28));
                    EditorGUILayout.LabelField(
                        $"{a}  ↔  {b}",
                        GUILayout.Width(280));
                    EditorGUILayout.LabelField(note, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static List<(string a, string b, bool collide, string note)> GetCollisionRules()
        {
            return new List<(string, string, bool, string)>
            {
                // Player capsule
                ("Player",       "Player",          false, "No jitter between player capsules in top-down"),
                ("Player",       "PlayerHitBox",    false, "Hitboxes are on a different sub-layer"),
                ("Player",       "Projectile",      false, "Bullets detect via PlayerHitBox only"),
                ("Player",       "Throwable",       false, "Grenades arc over/through player capsule"),
                ("Player",       "DeadCharacter",   false, "Don't trip over ragdolls"),
                ("Player",       "Items",           false, "Items are visual; use Interactable trigger for range"),
                ("Player",       "Minimap",         false, "Minimap markers are rendering-only"),
                ("Player",       "MapStatic",       false, "Rendering-only layer"),
                ("Player",       "SeeThrough",      false, "Replacement visual mesh — no physics"),
                ("Player",       "Ground",          true,  "Stands on the floor"),
                ("Player",       "Wall",            true,  "Blocked by walls"),
                ("Player",       "Interactable",    true,  "Enter pickup range / door trigger"),
                ("Player",       "Zone",            true,  "Enter lockdown / buff zone"),
                ("Player",       "MapObstacle",     true,  "Blocked by cover / crates"),
                // PlayerHitBox — ONLY Projectile may interact
                ("PlayerHitBox", "Projectile",      true,  "Bullet hits hitbox → damage path"),
                ("PlayerHitBox", "PlayerHitBox",    false, "Own hitboxes don't block each other"),
                ("PlayerHitBox", "Ground",          false, "Hitboxes float freely"),
                ("PlayerHitBox", "Wall",            false, "No wall-push on hitboxes"),
                ("PlayerHitBox", "Interactable",    false, ""),
                ("PlayerHitBox", "Zone",            false, "Zones detect Player capsule, not hitboxes"),
                ("PlayerHitBox", "Throwable",       false, ""),
                ("PlayerHitBox", "DeadCharacter",   false, ""),
                ("PlayerHitBox", "Items",           false, ""),
                ("PlayerHitBox", "MapObstacle",     false, ""),
                ("PlayerHitBox", "SeeThrough",      false, ""),
                ("PlayerHitBox", "MapStatic",       false, ""),
                ("PlayerHitBox", "Minimap",         false, ""),
                // Projectile — stops on PlayerHitBox, Wall, Ground
                ("Projectile",   "Projectile",      false, "Bullets don't collide with each other"),
                ("Projectile",   "Wall",            true,  "Bullet embeds in wall"),
                ("Projectile",   "Ground",          true,  "Bullet hits ground (decal)"),
                ("Projectile",   "MapObstacle",     true,  "Bullet hits cover"),
                ("Projectile",   "Zone",            false, "Bullets fly through zones"),
                ("Projectile",   "Interactable",    false, "Bullets don't destroy pickups"),
                ("Projectile",   "Throwable",       false, "Bullets don't hit grenades in flight"),
                ("Projectile",   "DeadCharacter",   false, "Bullets pass through dead bodies"),
                ("Projectile",   "Items",           false, ""),
                ("Projectile",   "Minimap",         false, ""),
                ("Projectile",   "MapStatic",       false, "Bullets can pass through map-static visuals"),
                ("Projectile",   "SeeThrough",      false, "See-through mesh has no physics"),
                // Throwable — bounces on Ground, Wall, MapObstacle
                ("Throwable",    "Throwable",       false, "Grenades don't stack/collide"),
                ("Throwable",    "Ground",          true,  "Grenade bounces on floor"),
                ("Throwable",    "Wall",            true,  "Grenade bounces on wall"),
                ("Throwable",    "MapObstacle",     true,  "Grenade bounces on cover"),
                ("Throwable",    "Zone",            false, ""),
                ("Throwable",    "Interactable",    false, ""),
                ("Throwable",    "DeadCharacter",   false, ""),
                ("Throwable",    "Items",           false, ""),
                ("Throwable",    "Minimap",         false, ""),
                ("Throwable",    "MapStatic",       false, ""),
                ("Throwable",    "SeeThrough",      false, ""),
                // Zone — trigger volumes detect Player only
                ("Zone",         "Zone",            false, "Zones don't trigger each other"),
                ("Zone",         "Ground",          false, ""),
                ("Zone",         "Wall",            false, "Zone volumes are not blocked by walls"),
                ("Zone",         "Interactable",    false, ""),
                ("Zone",         "DeadCharacter",   false, ""),
                ("Zone",         "Items",           false, ""),
                ("Zone",         "Minimap",         false, ""),
                ("Zone",         "MapStatic",       false, ""),
                ("Zone",         "MapObstacle",     false, ""),
                ("Zone",         "SeeThrough",      false, ""),
                // Interactable — detects Player; rests on Ground only
                ("Interactable", "Interactable",    false, "Items don't stack on each other"),
                ("Interactable", "Ground",          true,  "Dropped item rests on floor"),
                ("Interactable", "Wall",            false, "Items clip into walls — acceptable"),
                ("Interactable", "DeadCharacter",   false, ""),
                ("Interactable", "Items",           false, ""),
                ("Interactable", "Minimap",         false, ""),
                ("Interactable", "MapStatic",       false, ""),
                ("Interactable", "MapObstacle",     false, ""),
                ("Interactable", "SeeThrough",      false, ""),
                // DeadCharacter — ragdoll rests on Ground + Wall
                ("DeadCharacter","DeadCharacter",   false, ""),
                ("DeadCharacter","Ground",          true,  "Ragdoll rests on floor"),
                ("DeadCharacter","Wall",            true,  "Ragdoll bounded by walls"),
                ("DeadCharacter","Items",           false, ""),
                ("DeadCharacter","Minimap",         false, ""),
                ("DeadCharacter","MapStatic",       false, ""),
                ("DeadCharacter","MapObstacle",     true,  "Ragdoll bounded by cover"),
                ("DeadCharacter","SeeThrough",      false, ""),
                // Rendering-only layers — no physics
                ("MapStatic",    "MapStatic",       false, "Pure rendering"),
                ("MapStatic",    "Wall",            false, ""),
                ("MapStatic",    "Ground",          false, ""),
                ("MapStatic",    "SeeThrough",      false, ""),
                ("MapStatic",    "Minimap",         false, ""),
                ("MapStatic",    "Items",           false, ""),
                ("MapStatic",    "MapObstacle",     false, ""),
                ("SeeThrough",   "SeeThrough",      false, "Visual mesh only"),
                ("SeeThrough",   "Wall",            false, "Physics layer = Wall; SeeThrough = visual mesh"),
                ("SeeThrough",   "Ground",          false, ""),
                ("SeeThrough",   "Minimap",         false, ""),
                ("SeeThrough",   "Items",           false, ""),
                ("SeeThrough",   "MapObstacle",     false, ""),
                ("Items",        "Items",           false, ""),
                ("Items",        "Wall",            false, ""),
                ("Items",        "MapStatic",       false, ""),
                ("Items",        "SeeThrough",      false, ""),
                ("Items",        "Minimap",         false, ""),
                ("Items",        "DeadCharacter",   false, ""),
                ("Items",        "MapObstacle",     false, ""),
            };
        }

        // ── FOW notes ──────────────────────────────────────────────────────
        private void DrawFOWNotes()
        {
            _showFOWNotes = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showFOWNotes, "  Fog-of-War (FOW) Rendering Integration");

            if (_showFOWNotes)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                GUIStyle small = EditorStyles.wordWrappedMiniLabel;

                EditorGUILayout.LabelField("LAYER → FOW BEHAVIOR", EditorStyles.boldLabel);
                DrawFOWRow("Player (same team)",       "No FogOfWarHider",
                    "FogTeamVisibilityBinder removes hider → ALWAYS VISIBLE");
                DrawFOWRow("Player (enemy team)",      "HiderDisableRenderers",
                    "FogTeamVisibilityBinder adds hider → HIDDEN outside vision cone");
                DrawFOWRow("Character / PlayerCharacter / PlayerHitBox",
                    "Same as Player root",
                    "On same prefab hierarchy — follow player team visibility");
                DrawFOWRow("Wall / Ground / MapStatic / MapObstacle",
                    "No hider",
                    "Always rendered. FOW render-feature darkens them in unexplored fog areas.");
                DrawFOWRow("SeeThrough",               "No hider",
                    "Rendering-only mesh. Fog darkness effect applies via FOW's render feature.");
                DrawFOWRow("Items (dropped loot)",     "Optional",
                    "Battle-Royale style: always visible (no hider).\n" +
                    "Tactical style: attach HiderDisableRenderers → hidden in fog.");
                DrawFOWRow("Throwable (enemy grenade)","HiderDisableRenderers",
                    "Attach to enemy throwables so the grenade is hidden outside vision.");
                DrawFOWRow("Deployable (beacon)",      "FogTeamVisibilityBinder",
                    "Inherits team logic — enemy beacons hidden, own team beacons visible.");
                DrawFOWRow("Zone volumes",             "No hider",
                    "Trigger colliders only; typically invisible — no visual to hide.");
                DrawFOWRow("Interactable triggers",    "No hider",
                    "Trigger colliders only; door/pickup visual governed by Items/MapStatic.");

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("IMPLEMENTATION CHECKLIST", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "1. FogTeamVisibilityBinder on every player prefab root.\n" +
                    "2. FogOfWarRevealer3D on local player (disabled on death via FogVisionBinder).\n" +
                    "3. FogOfWarWorld.instance for global config (UnknownColor, BlurStrength etc.).\n" +
                    "4. FogOfWarRenderFeature added to the URP Renderer asset.\n" +
                    "5. FogOfWarHider + HiderDisableRenderers on enemy entity prefabs.\n" +
                    "6. Item prefabs: choose always-visible or hidden per game mode.",
                    small);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawFOWRow(string layer, string component, string behavior)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("  " + layer, EditorStyles.miniLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField(component,    EditorStyles.miniLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField(behavior,     EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ── SeeThrough Shader notes ────────────────────────────────────────
        private void DrawSTSNotes()
        {
            _showSTSNotes = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showSTSNotes, "  See-Through Shader (STS) Integration");

            if (_showSTSNotes)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                GUIStyle small = EditorStyles.wordWrappedMiniLabel;

                EditorGUILayout.LabelField("ARCHITECTURE", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "The See-Through Shader (ShaderCrew STS) makes specific meshes transparent " +
                    "when a player's camera–player line-of-sight passes through them.\n\n" +
                    "KEY SEPARATION:\n" +
                    "  • 'Wall' layer      → physics collider  (blocks movement & bullets)\n" +
                    "  • 'SeeThrough' layer → visual mesh      (becomes transparent in STS)\n" +
                    "A wall object typically has TWO parts:\n" +
                    "  1. Invisible high-poly physics mesh → layer 'Wall'\n" +
                    "  2. Visible render mesh              → layer 'SeeThrough'  (+ STS material)",
                    small);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("MULTI-LEVEL FLOOR LOGIC", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Scenario: Player is standing on Level 1. Level 2 floor is above.\n" +
                    "  • Level 2 floor collider    → layer 'Ground'    (walkable surface)\n" +
                    "  • Level 2 floor visual mesh  → layer 'SeeThrough' (becomes transparent)\n" +
                    "STS detects: camera–player ray passes through the Level 2 mesh → applies cut-out.\n\n" +
                    "Scenario: Player is ON Level 2 floor (standing on top):\n" +
                    "  • The same mesh does NOT become transparent from above.\n" +
                    "  • STS only triggers when the player is BELOW the mesh.\n\n" +
                    "Pure Ground (no floor above): layer 'Ground' only; do NOT add SeeThrough.",
                    small);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("SEETHROUGHSHADER SETUP CHECKLIST", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "1. GlobalShaderReplacement.layerMasksWithReplacement = 'SeeThrough' layer mask.\n" +
                    "2. PlayersPositionManager / SeeThroughShaderPlayer — set their 'Player Layer' " +
                       "field to the 'Player' layer.\n" +
                    "3. PlayerToCameraRaycastTriggerManager raycast mask → 'SeeThrough | Wall'.\n" +
                    "4. Walls: MeshCollider on 'Wall' layer (no Renderer), " +
                       "MeshRenderer on 'SeeThrough' child with the STS material.\n" +
                    "5. Do NOT put Rigidbodies on SeeThrough meshes — physics-only on Wall layer.",
                    small);

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ── Minimap notes ──────────────────────────────────────────────────
        private void DrawMinimapNotes()
        {
            EditorGUILayout.BeginFoldoutHeaderGroup(false, "  Minimap Layer & Camera Setup");
            // Rendered inline (never collapses) — just a compact static block.
            EditorGUILayout.BeginVertical(_boxStyle);
            GUIStyle small = EditorStyles.wordWrappedMiniLabel;

            EditorGUILayout.LabelField(
                "MINIMAP CAMERA  (MinimapCameraController.cs)\n" +
                "  cullingMask = Minimap | Ground | Wall | MapStatic | MapObstacle\n" +
                "  depth       = -1  (renders before main camera)\n" +
                "  clearFlags  = Solid Color  (dark background)\n" +
                "  orthographic = true  (set automatically by script)\n\n" +
                "PLAYER MARKERS  (MinimapMarkerController.cs)\n" +
                "  • Component auto-sets gameObject.layer = 'Minimap' on Awake.\n" +
                "  • Sprite color = TeamService.GetTeamColor(teamId).\n" +
                "  • SetVisible(false) on player death / despawn.\n\n" +
                "ENEMY MARKERS  (FOW-aware)\n" +
                "  • Enemy Minimap markers should be children of the enemy player prefab.\n" +
                "  • FogTeamVisibilityBinder also controls the marker's visibility:\n" +
                "    → enemy marker hidden inside fog  (SetVisible via FogTeamVisibilityBinder).\n" +
                "  • Teammate minimap marker: always visible (even through walls).\n\n" +
                "OBJECTIVE ICONS  (optional, put on 'Minimap' layer)\n" +
                "  • Add a child GO with SpriteRenderer on 'Minimap' layer.\n" +
                "  • Use a constant world-space size (no scale from camera).",
                small);
            EditorGUILayout.EndVertical();
        }

        // ── Log ────────────────────────────────────────────────────────────
        private void DrawLog()
        {
            if (string.IsNullOrEmpty(_log)) return;

            EditorGUILayout.LabelField("Last Operation Log", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(_boxStyle);
            EditorGUILayout.LabelField(_log, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        // ── Apply logic ────────────────────────────────────────────────────
        private void ApplyAll()
        {
            var lines = new List<string> { "=== NightHunt Layer & Tag Setup ===" };

            ApplyLayers(lines);
            ApplyTags(lines);
            ApplyCollisionMatrix(lines);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _log = string.Join("\n", lines);
            Debug.Log(_log);
            Repaint();

            EditorUtility.DisplayDialog("Setup Complete",
                "Layers, tags, and physics collision matrix applied.\n\n" +
                "See Console for the full diff log.\n\n" +
                "Next steps:\n" +
                "1. Set MinimapCameraController.cullingMask (see notes below).\n" +
                "2. Verify SeeThrough shader LayerMask in GlobalShaderReplacement.\n" +
                "3. Call NightHuntLayers.ValidateAll() from a game-start script.",
                "OK");
        }

        // ── Apply Layers ───────────────────────────────────────────────────
        private static void ApplyLayers(List<string> log)
        {
            log.Add("\n[Layers]");
            var (layers, tm) = OpenTagManager();

            foreach (var (slot, name, _) in LayerTable)
            {
                string current = GetLayerName(layers, slot);

                if (current == name)
                {
                    log.Add($"  [{slot:D2}] \"{name}\"  ✔ already correct");
                }
                else if (string.IsNullOrEmpty(current))
                {
                    var e = layers.GetArrayElementAtIndex(slot);
                    e.stringValue = name;
                    log.Add($"  [{slot:D2}] \"{name}\"  ⊕ created");
                }
                else
                {
                    var e = layers.GetArrayElementAtIndex(slot);
                    log.Add($"  [{slot:D2}] \"{name}\"  ⚠ replaced \"{current}\"");
                    e.stringValue = name;
                }
            }

            tm.ApplyModifiedProperties();
            log.Add("  TagManager saved.");
        }

        // ── Apply Tags ─────────────────────────────────────────────────────
        private static void ApplyTags(List<string> log)
        {
            log.Add("\n[Tags]");
            var (tags, tm) = OpenTagManagerTags();

            var existingTags = new HashSet<string>(InternalEditorUtility.tags);

            foreach (var (name, _) in TagTable)
            {
                if (existingTags.Contains(name))
                {
                    log.Add($"  Tag \"{name}\"  ✔ already exists");
                    continue;
                }

                // Append new element to the "tags" array
                int newIdx = tags.arraySize;
                tags.InsertArrayElementAtIndex(newIdx);
                tags.GetArrayElementAtIndex(newIdx).stringValue = name;
                log.Add($"  Tag \"{name}\"  ⊕ added");
            }

            tm.ApplyModifiedProperties();
        }

        // ── Build collision matrix ─────────────────────────────────────────
        private static void ApplyCollisionMatrix(List<string> log)
        {
            log.Add("\n[Collision Matrix]");

            // Resolve all layer IDs by name (safe — names already written above)
            int lPlayer         = LayerMask.NameToLayer("Player");
            int lPlayerHitBox   = LayerMask.NameToLayer("PlayerHitBox");
            int lProjectile     = LayerMask.NameToLayer("Projectile");
            int lCharacter      = LayerMask.NameToLayer("Character");
            int lInteractable   = LayerMask.NameToLayer("Interactable");
            int lZone           = LayerMask.NameToLayer("Zone");
            int lThrowable      = LayerMask.NameToLayer("Throwable");
            int lDeadCharacter  = LayerMask.NameToLayer("DeadCharacter");
            int lPlayerChar     = LayerMask.NameToLayer("PlayerCharacter");
            int lMapObstacle    = LayerMask.NameToLayer("MapObstacle");
            int lItems          = LayerMask.NameToLayer("Items");
            int lMinimap        = LayerMask.NameToLayer("Minimap");
            int lMapStatic      = LayerMask.NameToLayer("MapStatic");
            int lWall           = LayerMask.NameToLayer("Wall");
            int lGround         = LayerMask.NameToLayer("Ground");
            int lSeeThrough     = LayerMask.NameToLayer("SeeThrough");

            // Collect custom layer ids (skip unresolved -1)
            int[] custom = { lPlayer, lPlayerHitBox, lProjectile, lCharacter,
                             lInteractable, lZone, lThrowable, lDeadCharacter,
                             lPlayerChar, lMapObstacle, lItems, lMinimap,
                             lMapStatic, lWall, lGround, lSeeThrough };

            // Step 1 — RESET: re-enable all collisions between our custom layers so
            // previous stale ignores don't persist.
            int resetCount = 0;
            foreach (int a in custom)
            {
                if (a < 0) continue;
                foreach (int b in custom)
                {
                    if (b < a) continue;   // visit each pair once
                    if (Physics.GetIgnoreLayerCollision(a, b))
                    {
                        Physics.IgnoreLayerCollision(a, b, false);
                        resetCount++;
                    }
                }
            }
            log.Add($"  Reset {resetCount} previously-ignored pair(s) to default (collide).");

            // Step 2 — APPLY specific ignores.
            // Helper: ignore(a, b) — checks both are valid before calling.
            static void Ignore(int a, int b)
            {
                if (a >= 0 && b >= 0)
                    Physics.IgnoreLayerCollision(a, b, true);
            }

            // ── Minimap — ignore against EVERYTHING (rendering only) ────────
            for (int i = 0; i < 32; i++)
                if (lMinimap >= 0)
                    Physics.IgnoreLayerCollision(lMinimap, i, true);

            // ── MapStatic — rendering layer, no physics ─────────────────────
            foreach (int x in custom)
            { Ignore(lMapStatic, x); }

            // ── SeeThrough — visual replacement mesh, no physics ───────────
            foreach (int x in custom)
            { Ignore(lSeeThrough, x); }

            // ── Character / PlayerCharacter — visual references only ────────
            foreach (int x in custom)
            { Ignore(lCharacter, x); Ignore(lPlayerChar, x); }

            // ── Player capsule (movement only) ─────────────────────────────
            // Collides: Ground, Wall, Interactable, Zone, MapObstacle
            // Ignores everything else:
            Ignore(lPlayer, lPlayer);
            Ignore(lPlayer, lPlayerHitBox);
            Ignore(lPlayer, lProjectile);
            Ignore(lPlayer, lThrowable);
            Ignore(lPlayer, lDeadCharacter);
            Ignore(lPlayer, lItems);
            // Note: Player × Ground, Wall, Interactable, Zone, MapObstacle → collide (default)

            // ── PlayerHitBox — ONLY collides with Projectile ────────────────
            Ignore(lPlayerHitBox, lPlayerHitBox);
            Ignore(lPlayerHitBox, lGround);
            Ignore(lPlayerHitBox, lWall);
            Ignore(lPlayerHitBox, lInteractable);
            Ignore(lPlayerHitBox, lZone);
            Ignore(lPlayerHitBox, lThrowable);
            Ignore(lPlayerHitBox, lDeadCharacter);
            Ignore(lPlayerHitBox, lItems);
            Ignore(lPlayerHitBox, lMapObstacle);
            // Note: PlayerHitBox × Projectile → collide (damage path)

            // ── Projectile — stops on PlayerHitBox, Ground, Wall, MapObstacle
            Ignore(lProjectile, lProjectile);
            Ignore(lProjectile, lZone);
            Ignore(lProjectile, lInteractable);
            Ignore(lProjectile, lThrowable);
            Ignore(lProjectile, lDeadCharacter);
            Ignore(lProjectile, lItems);
            // Note: Projectile × PlayerHitBox, Ground, Wall, MapObstacle → collide

            // ── Throwable — bounces on Ground, Wall, MapObstacle ───────────
            Ignore(lThrowable, lThrowable);
            Ignore(lThrowable, lZone);
            Ignore(lThrowable, lInteractable);
            Ignore(lThrowable, lDeadCharacter);
            Ignore(lThrowable, lItems);
            // Note: Throwable × Ground, Wall, MapObstacle → collide

            // ── Zone — trigger; detects Player (already collides by default)
            Ignore(lZone, lZone);
            Ignore(lZone, lGround);
            Ignore(lZone, lWall);
            Ignore(lZone, lInteractable);
            Ignore(lZone, lDeadCharacter);
            Ignore(lZone, lItems);
            Ignore(lZone, lMapObstacle);

            // ── Interactable — range trigger + rests on Ground ──────────────
            Ignore(lInteractable, lInteractable);
            Ignore(lInteractable, lWall);
            Ignore(lInteractable, lDeadCharacter);
            Ignore(lInteractable, lItems);
            Ignore(lInteractable, lMapObstacle);
            Ignore(lInteractable, lZone);  // already defined above
            // Note: Interactable × Ground, Player → collide

            // ── DeadCharacter — ragdoll, rests on Ground / Wall / MapObstacle
            Ignore(lDeadCharacter, lDeadCharacter);
            Ignore(lDeadCharacter, lProjectile);  // bullets pass through ragdolls
            Ignore(lDeadCharacter, lThrowable);
            Ignore(lDeadCharacter, lItems);
            Ignore(lDeadCharacter, lInteractable);
            Ignore(lDeadCharacter, lZone);
            // Note: DeadCharacter × Ground, Wall, MapObstacle → collide

            // ── Items — visual world model, rests on Ground ─────────────────
            Ignore(lItems, lItems);
            Ignore(lItems, lWall);
            Ignore(lItems, lDeadCharacter);
            Ignore(lItems, lMapObstacle);
            Ignore(lItems, lZone);
            Ignore(lItems, lThrowable);
            Ignore(lItems, lProjectile);

            log.Add("  Physics collision matrix applied (name-based — survives layer slot changes).");
        }

        // ── TagManager helpers ─────────────────────────────────────────────
        private static (SerializedProperty layers, SerializedObject tm)
            OpenTagManager()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "ProjectSettings/TagManager.asset");
            var tm     = new SerializedObject(asset);
            var layers = tm.FindProperty("layers");
            return (layers, tm);
        }

        private static (SerializedProperty tags, SerializedObject tm)
            OpenTagManagerTags()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "ProjectSettings/TagManager.asset");
            var tm   = new SerializedObject(asset);
            var tags = tm.FindProperty("tags");
            return (tags, tm);
        }

        private static string GetLayerName(SerializedProperty layers, int slot)
        {
            if (slot < 0 || slot >= layers.arraySize) return "(out of range)";
            return layers.GetArrayElementAtIndex(slot).stringValue ?? "";
        }

        // ── Style init ─────────────────────────────────────────────────────
        private void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _okStyle = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.2f, 0.8f, 0.2f) } };

            _warnStyle = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(1f, 0.75f, 0.1f) } };

            _errorStyle = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(1f, 0.35f, 0.25f) } };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 13 };

            _boxStyle = new GUIStyle("box")
            { padding = new RectOffset(8, 8, 6, 6), margin = new RectOffset(4, 4, 2, 2) };
        }
    }
}
#endif
