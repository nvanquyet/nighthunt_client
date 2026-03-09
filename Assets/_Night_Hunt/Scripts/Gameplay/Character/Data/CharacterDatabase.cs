using NightHunt.Core;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Data
{
    /// <summary>
    /// Ordered registry of all playable CharacterDefinitions.
    /// Inherits singleton pattern from <see cref="ScriptableObjectSingleton{T}"/>.
    ///
    /// HOW THE INDEX WORKS:
    ///   The network transmits an integer (CharacterModelIndex) which is the
    ///   position in _entries[].  Entry 0 = default (Soldier_White).
    ///   NEVER reorder existing entries once a live server is running —
    ///   it would cause mismatched skins for connected sessions.
    ///   To retire a skin, replace ModelPrefab with a fallback instead of removing.
    ///
    /// INSPECTOR SETUP:
    ///   1. Right-click in Project → Create → NightHunt → Character → Character Database
    ///   2. Save as "Assets/_Night_Hunt/Data/CharacterDatabase.asset" (one per project).
    ///   3. Drag CharacterDefinition assets into _entries IN ORDER.
    ///      Index 0 → Soldier_White (default)
    ///   4. Assign this asset to every PlayerModelLoader in the project and to the
    ///      character select screen UI.  Optionally place in Resources/ for fallback load.
    ///
    /// USAGE:
    ///   CharacterDefinition def = CharacterDatabase.Instance.GetByIndex(2);
    ///   if (def != null) Instantiate(def.ModelPrefab, modelParent);
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterDatabase",
        menuName  = "NightHunt/Character/Character Database")]
    public sealed class CharacterDatabase : ScriptableObjectSingleton<CharacterDatabase>
    {
        // ── Data ──────────────────────────────────────────────────────────────

        [Tooltip("Ordered list of character skins.\n" +
                 "Index 0 = default. Do NOT reorder once a server session has started.")]
        [SerializeField] private CharacterDefinition[] _entries = System.Array.Empty<CharacterDefinition>();

        // Key = CharacterDefinition.CharacterId (the string the backend uses), Value = array index
        private System.Collections.Generic.Dictionary<string, int> _idToIndex;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void OnSingletonEnabled() => RebuildIndex();

        private void RebuildIndex()
        {
            _idToIndex = new System.Collections.Generic.Dictionary<string, int>(
                System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] == null) continue;
                string id = _entries[i].CharacterId;
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[CharacterDatabase] Entry [{i}] has no CharacterId — " +
                                     "it cannot be resolved from a backend string ID.");
                    continue;
                }
                if (_idToIndex.ContainsKey(id))
                    Debug.LogWarning($"[CharacterDatabase] Duplicate CharacterId '{id}' at index [{i}]. " +
                                     "Only the first occurrence will be used.");
                else
                    _idToIndex[id] = i;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Total number of character definitions registered.</summary>
        public int Count => _entries?.Length ?? 0;

        /// <summary>
        /// Returns the CharacterDefinition at the given index, or null if out of range.
        /// Clamps to index 0 if a negative index is passed (never throws).
        /// </summary>
        public CharacterDefinition GetByIndex(int index)
        {
            if (_entries == null || _entries.Length == 0)
            {
                Debug.LogWarning("[CharacterDatabase] _entries is empty. " +
                                 "Drag CharacterDefinition assets into the database asset.");
                return null;
            }

            if (index < 0)
            {
                Debug.LogWarning($"[CharacterDatabase] Negative index {index} — clamping to 0.");
                index = 0;
            }

            if (index >= _entries.Length)
            {
                Debug.LogWarning($"[CharacterDatabase] Index {index} out of range (Count={_entries.Length}). " +
                                 $"Falling back to index 0.");
                index = 0;
            }

            return _entries[index];
        }

        /// <summary>
        /// Resolves a backend string ID (e.g. "character_02") to the array index used over the network.
        /// Returns -1 if the ID is not found — caller should treat -1 as "use default (0)".
        /// Case-insensitive.
        /// </summary>
        public int GetIndexById(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return -1;
            if (_idToIndex == null) RebuildIndex();
            return _idToIndex.TryGetValue(characterId, out int index) ? index : -1;
        }

        /// <summary>
        /// Resolves a backend string ID directly to its CharacterDefinition.
        /// Returns null if not found.
        /// </summary>
        public CharacterDefinition GetById(string characterId)
        {
            int index = GetIndexById(characterId);
            return index >= 0 ? GetByIndex(index) : null;
        }

        /// <summary>
        /// Returns a read-only view of all entries (for character select UI).
        /// </summary>
        public System.ReadOnlySpan<CharacterDefinition> GetAll()
            => new System.ReadOnlySpan<CharacterDefinition>(_entries);

        /// <summary>
        /// Returns all unlocked definitions (for filtering on character select screen).
        /// </summary>
        public CharacterDefinition[] GetAllUnlocked()
        {
            var result = new System.Collections.Generic.List<CharacterDefinition>();
            foreach (var entry in _entries)
                if (entry != null && entry.IsUnlocked)
                    result.Add(entry);
            return result.ToArray();
        }

        // ── Validation (Editor) ───────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildIndex(); // keep lookup dict fresh while editing in Editor
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] == null)
                {
                    Debug.LogWarning($"[CharacterDatabase] Entry [{i}] is null. " +
                                     "Assign a CharacterDefinition asset.");
                    continue;
                }
                if (_entries[i].ModelPrefab == null)
                {
                    Debug.LogWarning($"[CharacterDatabase] Entry [{i}] '{_entries[i].name}' — " +
                                     "ModelPrefab is not assigned.");
                }
            }
        }
#endif
    }
}
