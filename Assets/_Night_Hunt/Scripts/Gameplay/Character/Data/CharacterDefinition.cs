using UnityEngine;

namespace NightHunt.Gameplay.Character.Data
{
    /// <summary>
    /// Data definition for a single playable character skin.
    ///
    /// HOW IT FITS IN THE SYSTEM:
    ///   CharacterDatabase (ScriptableObject) holds an ordered array of these.
    ///   The array INDEX is what gets transmitted over the network via
    ///   PlayerModelLoader._modelIndex SyncVar — so order in the database
    ///   MUST NOT change once a server session has started.
    ///
    /// INSPECTOR SETUP:
    ///   1. Right-click in Project → Create → NightHunt → Character → Character Definition
    ///   2. Assign ModelPrefab → drag Soldier_White.prefab (or variant mesh prefab).
    ///      The prefab must have PrActorUtils, PrCharacterIK, PrCharacterRagdoll on its root.
    ///   3. Fill in DisplayName, Icon, and Thumbnail for the character select screen.
    ///   4. Drag this asset into CharacterDatabase._entries at the matching index.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Character_",
        menuName  = "NightHunt/Character/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique string ID. Used only for debugging — the network transmits the array index.")]
        public string CharacterId;

        [Tooltip("Display name shown on character select screen.")]
        public string DisplayName;

        [TextArea(2, 4)]
        [Tooltip("Short lore/description shown in character select.")]
        public string Description;

        [Header("Visuals")]
        [Tooltip("Full-body portrait or thumbnail for character select UI.")]
        public Sprite Thumbnail;

        [Tooltip("Small icon for in-game HUD or kill-feed.")]
        public Sprite Icon;

        [Header("Prefab")]
        [Tooltip("The mesh prefab to instantiate under the 'Model' child of PlayerPrefab.\n" +
                 "Must contain PrActorUtils + PrCharacterIK + PrCharacterRagdoll on its root.")]
        public GameObject ModelPrefab;

        [Header("Availability")]
        [Tooltip("Set false to hide this skin from the character select screen (e.g. unreleased skin).")]
        public bool IsUnlocked = true;
    }
}
