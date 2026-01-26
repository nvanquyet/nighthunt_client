using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NightHunt.InteractionSystem.Loot.Definitions
{
    /// <summary>
    /// Database for resolving LootItemDefinition by id at runtime (client + server).
    /// Place an instance under Resources/ with name: LootItemDefinitionDatabase
    /// </summary>
    [CreateAssetMenu(fileName = "LootItemDefinitionDatabase", menuName = "NightHunt/InteractionSystem/Loot/LootItemDefinitionDatabase")]
    public class LootItemDefinitionDatabase : ScriptableObject
    {
        [SerializeField] private LootItemDefinition[] definitions = new LootItemDefinition[0];

        private Dictionary<string, LootItemDefinition> _byId;

        public LootItemDefinition GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            BuildCacheIfNeeded();
            _byId.TryGetValue(id, out var def);
            return def;
        }

        private void BuildCacheIfNeeded()
        {
            if (_byId != null)
                return;

            _byId = new Dictionary<string, LootItemDefinition>();
            if (definitions == null)
                return;

            foreach (var def in definitions)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.DefinitionId))
                    continue;

                _byId[def.DefinitionId] = def;
            }
        }

        /// <summary>
        /// Get all definitions (for searching by item ID).
        /// </summary>
        public IEnumerable<LootItemDefinition> GetAllDefinitions()
        {
            BuildCacheIfNeeded();
            return _byId.Values;
        }

        public static LootItemDefinitionDatabase Load()
        {
            return Resources.Load<LootItemDefinitionDatabase>("LootItemDefinitionDatabase");
        }
    }
}

