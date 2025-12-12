using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Data;
using UnityEngine;

namespace NightHunt.Core.Config
{
    /// <summary>
    /// Async configuration loader with caching
    /// Parses JSON config into strongly-typed data structures
    /// </summary>
    public class ConfigLoader
    {
        private const string CONFIG_PATH = "Configs/";
        private Dictionary<string, GameData> cache = new Dictionary<string, GameData>();

        public async Task<GameData> LoadAsync(string configName)
        {
            if (cache.TryGetValue(configName, out var cached))
            {
                return cached;
            }

            var textAsset = await LoadTextAssetAsync(configName);
            if (textAsset == null)
            {
                Debug.LogError($"[ConfigLoader] Failed to load {configName}");
                return null;
            }

            var gameData = JsonUtility.FromJson<GameData>(textAsset.text);
            cache[configName] = gameData;
            
            return gameData;
        }

        private async Task<TextAsset> LoadTextAssetAsync(string name)
        {
            var request = Resources.LoadAsync<TextAsset>(CONFIG_PATH + name);
            
            while (!request.isDone)
            {
                await Task.Yield();
            }
            
            return request.asset as TextAsset;
        }
    }
    
    
    
}