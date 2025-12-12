using System.Collections.Generic;
using NightHunt.Data.Configs;

namespace NightHunt.Data
{
    /// <summary>
    /// Database - Quick lookup by ID
    /// </summary>
    public class GameDatabase
    {
        private Dictionary<string, WeaponConfig> weapons = new Dictionary<string, WeaponConfig>();
        private Dictionary<string, ItemConfig> items = new Dictionary<string, ItemConfig>();
        private Dictionary<string, StatusEffectConfig> statusEffects = new Dictionary<string, StatusEffectConfig>();

        public void Initialize(GameData data)
        {
            foreach (var w in data.WeaponConfig)
                weapons[w.WeaponId] = w;
            
            foreach (var i in data.ItemConfig)
                items[i.ItemId] = i;
            
            foreach (var s in data.StatusEffectConfig)
                statusEffects[s.StatusId] = s;
        }

        public WeaponConfig GetWeapon(string id) => weapons.TryGetValue(id, out var w) ? w : null;
        public ItemConfig GetItem(string id) => items.TryGetValue(id, out var i) ? i : null;
        public StatusEffectConfig GetStatus(string id) => statusEffects.TryGetValue(id, out var s) ? s : null;
    }
}