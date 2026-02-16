using UnityEngine;
using GameplaySystems.Inventory;
using GameplaySystems.Core;
using GameplaySystems.Core.Data;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Stat;
using UnityEditor;

namespace GameplaySystems.Tests
{
    /// <summary>
    /// Runtime debug console for testing GameplaySystems
    /// Add to Player prefab for quick testing
    /// Toggle with F1 key
    /// </summary>
    public class GameplayDebugConsole : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        [SerializeField] private bool _startVisible = false;
        
        [Header("References (Auto-Assigned)")]
        [SerializeField] private PlayerStatSystem _statSystem;
        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private EquipmentSystem _equipmentSystem;
        [SerializeField] private WeaponSystem _weaponSystem;
        [SerializeField] private QuickSlotSystem _quickSlotSystem;
        [SerializeField] private AttachmentSystem _attachmentSystem;
        
        private bool _showConsole;
        private Vector2 _scrollPosition;
        private string _commandInput = "";
        
        private void Awake()
        {
            if (_statSystem == null) _statSystem = GetComponent<PlayerStatSystem>();
            if (_inventorySystem == null) _inventorySystem = GetComponent<InventorySystem>();
            if (_equipmentSystem == null) _equipmentSystem = GetComponent<EquipmentSystem>();
            if (_weaponSystem == null) _weaponSystem = GetComponent<WeaponSystem>();
            if (_quickSlotSystem == null) _quickSlotSystem = GetComponent<QuickSlotSystem>();
            if (_attachmentSystem == null) _attachmentSystem = GetComponent<AttachmentSystem>();
            
            _showConsole = _startVisible;
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _showConsole = !_showConsole;
            }
        }
        
        private void OnGUI()
        {
            if (!_showConsole)
                return;
            
            float width = 500;
            float height = 700;
            float x = Screen.width - width - 10;
            float y = 10;
            
            GUILayout.BeginArea(new Rect(x, y, width, height));
            
            // Background
            GUI.Box(new Rect(0, 0, width, height), "");
            
            GUILayout.BeginVertical();
            
            // Header
            GUILayout.Label("🛠️ GAMEPLAY DEBUG CONSOLE", EditorStyles.boldLabel);
            GUILayout.Label($"Press {_toggleKey} to toggle", EditorStyles.miniLabel);
            GUILayout.Space(10);
            
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(height - 150));
            
            DrawInventoryCommands();
            GUILayout.Space(10);
            DrawEquipmentCommands();
            GUILayout.Space(10);
            DrawWeaponCommands();
            GUILayout.Space(10);
            DrawStatCommands();
            GUILayout.Space(10);
            DrawQuickCommands();
            
            GUILayout.EndScrollView();
            
            // Command input
            GUILayout.Space(10);
            GUILayout.Label("Quick Command:", EditorStyles.miniLabel);
            GUILayout.BeginHorizontal();
            _commandInput = GUILayout.TextField(_commandInput, GUILayout.Width(350));
            if (GUILayout.Button("Execute", GUILayout.Width(140)))
            {
                ExecuteCommand(_commandInput);
                _commandInput = "";
            }
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #region Draw Commands
        
        private void DrawInventoryCommands()
        {
            GUILayout.Label("📦 INVENTORY", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ AK-47", GUILayout.Width(120)))
                ExecuteCommand("add weapon_ak47 1");
            if (GUILayout.Button("+ Vest", GUILayout.Width(120)))
                ExecuteCommand("add armor_vest 1");
            if (GUILayout.Button("+ Medkit x5", GUILayout.Width(120)))
                ExecuteCommand("add consumable_medkit 5");
            if (GUILayout.Button("+ Scope", GUILayout.Width(120)))
                ExecuteCommand("add attachment_reddot 1");
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All", GUILayout.Width(120)))
                ExecuteCommand("clear");
            if (GUILayout.Button("Log Items", GUILayout.Width(120)))
                ExecuteCommand("log inventory");
            GUILayout.EndHorizontal();
        }
        
        private void DrawEquipmentCommands()
        {
            GUILayout.Label("👔 EQUIPMENT", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Equip Vest", GUILayout.Width(120)))
                ExecuteCommand("equip armor_vest");
            if (GUILayout.Button("Equip Helmet", GUILayout.Width(120)))
                ExecuteCommand("equip armor_helmet");
            if (GUILayout.Button("Equip Backpack", GUILayout.Width(120)))
                ExecuteCommand("equip armor_backpack");
            if (GUILayout.Button("Unequip All", GUILayout.Width(120)))
                ExecuteCommand("unequip all");
            GUILayout.EndHorizontal();
        }
        
        private void DrawWeaponCommands()
        {
            GUILayout.Label("🔫 WEAPONS", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Equip AK-47", GUILayout.Width(120)))
                ExecuteCommand("equip weapon_ak47");
            if (GUILayout.Button("Select Primary", GUILayout.Width(120)))
                ExecuteCommand("select primary");
            if (GUILayout.Button("Reload", GUILayout.Width(120)))
                ExecuteCommand("reload primary");
            if (GUILayout.Button("Holster", GUILayout.Width(120)))
                ExecuteCommand("holster");
            GUILayout.EndHorizontal();
        }
        
        private void DrawStatCommands()
        {
            GUILayout.Label("📊 STATS", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Log Stats", GUILayout.Width(120)))
                ExecuteCommand("log stats");
            if (GUILayout.Button("Log Weight", GUILayout.Width(120)))
                ExecuteCommand("log weight");
            if (GUILayout.Button("Log Modifiers", GUILayout.Width(120)))
                ExecuteCommand("log modifiers");
            GUILayout.EndHorizontal();
        }
        
        private void DrawQuickCommands()
        {
            GUILayout.Label("⚡ QUICK TESTS", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Full Loadout", GUILayout.Width(120)))
                ExecuteCommand("scenario loadout");
            if (GUILayout.Button("Overweight Test", GUILayout.Width(120)))
                ExecuteCommand("scenario overweight");
            if (GUILayout.Button("Run All Tests", GUILayout.Width(120)))
                ExecuteCommand("test all");
            GUILayout.EndHorizontal();
        }
        
        #endregion
        
        #region Command Execution
        
        private void ExecuteCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return;
            
            string[] parts = cmd.ToLower().Trim().Split(' ');
            string action = parts[0];
            
            Debug.Log($"<color=cyan>[Console] {cmd}</color>");
            
            try
            {
                switch (action)
                {
                    case "add":
                        if (parts.Length >= 2)
                        {
                            string itemID = parts[1];
                            int quantity = parts.Length >= 3 ? int.Parse(parts[2]) : 1;
                            _inventorySystem?.AddItem(itemID, quantity);
                            Debug.Log($"Added {quantity}x {itemID}");
                        }
                        break;
                    
                    case "remove":
                        if (parts.Length >= 2)
                        {
                            string itemID = parts[1];
                            int quantity = parts.Length >= 3 ? int.Parse(parts[2]) : 1;
                            _inventorySystem?.RemoveItemByDefinition(itemID, quantity);
                            Debug.Log($"Removed {quantity}x {itemID}");
                        }
                        break;
                    
                    case "clear":
                        _inventorySystem?.ClearInventory();
                        Debug.Log("Cleared inventory");
                        break;
                    
                    case "equip":
                        if (parts.Length >= 2)
                        {
                            string itemID = parts[1];
                            var items = _inventorySystem?.GetItemsByDefinition(itemID);
                            if (items != null && items.Count > 0)
                            {
                                var item = items[0];
                                var itemDef = ItemDatabase.GetDefinition(itemID);
                                
                                if (itemDef is GameplaySystems.Core.Data.WeaponDefinition)
                                {
                                    _weaponSystem?.EquipWeapon(item.InstanceID);
                                }
                                else
                                {
                                    _equipmentSystem?.EquipItem(item.InstanceID);
                                }
                                Debug.Log($"Equipped {itemID}");
                            }
                        }
                        break;
                    
                    case "unequip":
                        if (parts.Length >= 2 && parts[1] == "all")
                        {
                            foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
                            {
                                if (_equipmentSystem?.IsSlotOccupied(slot) == true)
                                {
                                    _equipmentSystem.UnequipItem(slot);
                                }
                            }
                            Debug.Log("Unequipped all equipment");
                        }
                        break;
                    
                    case "select":
                        if (parts.Length >= 2)
                        {
                            if (parts[1] == "primary")
                                _weaponSystem?.SelectWeapon(WeaponSlotType.Primary);
                            else if (parts[1] == "secondary")
                                _weaponSystem?.SelectWeapon(WeaponSlotType.Secondary);
                            else if (parts[1] == "melee")
                                _weaponSystem?.SelectWeapon(WeaponSlotType.Melee);
                        }
                        break;
                    
                    case "holster":
                        _weaponSystem?.HolsterWeapon();
                        Debug.Log("Holstered weapon");
                        break;
                    
                    case "reload":
                        if (parts.Length >= 2)
                        {
                            if (parts[1] == "primary")
                                _weaponSystem?.Reload(WeaponSlotType.Primary);
                            else if (parts[1] == "secondary")
                                _weaponSystem?.Reload(WeaponSlotType.Secondary);
                        }
                        break;
                    
                    case "log":
                        if (parts.Length >= 2)
                        {
                            if (parts[1] == "inventory")
                                _inventorySystem?.LogInventoryState();
                            else if (parts[1] == "stats")
                                _statSystem?.GetType().GetMethod("LogAllStats", 
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                    ?.Invoke(_statSystem, null);
                            else if (parts[1] == "weight")
                            {
                                var info = _inventorySystem?.GetWeightInfo();
                                Debug.Log($"Weight: {info?.current:F1} / {info?.capacity:F1} ({info?.percent:P0})");
                            }
                            else if (parts[1] == "modifiers")
                                _statSystem?.GetType().GetMethod("LogAllModifiers", 
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                    ?.Invoke(_statSystem, null);
                        }
                        break;
                    
                    case "scenario":
                        if (parts.Length >= 2)
                        {
                            if (parts[1] == "loadout")
                                ScenarioFullLoadout();
                            else if (parts[1] == "overweight")
                                ScenarioOverweight();
                        }
                        break;
                    
                    case "test":
                        if (parts.Length >= 2 && parts[1] == "all")
                        {
                            RunAllTests();
                        }
                        break;
                    
                    default:
                        Debug.LogWarning($"Unknown command: {action}");
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Command error: {e.Message}");
            }
        }
        
        #endregion
        
        #region Scenarios
        
        private void ScenarioFullLoadout()
        {
            _inventorySystem?.ClearInventory();
            
            // Weapons
            _inventorySystem?.AddItem("weapon_ak47", 1);
            var weapon = _inventorySystem?.GetItemsByDefinition("weapon_ak47")?[0];
            if (weapon != null)
                _weaponSystem?.EquipWeapon(weapon.InstanceID);
            
            // Equipment
            _inventorySystem?.AddItem("armor_vest", 1);
            _inventorySystem?.AddItem("armor_helmet", 1);
            _inventorySystem?.AddItem("armor_backpack", 1);
            
            var vest = _inventorySystem?.GetItemsByDefinition("armor_vest")?[0];
            var helmet = _inventorySystem?.GetItemsByDefinition("armor_helmet")?[0];
            var backpack = _inventorySystem?.GetItemsByDefinition("armor_backpack")?[0];
            
            if (vest != null) _equipmentSystem?.EquipItem(vest.InstanceID);
            if (helmet != null) _equipmentSystem?.EquipItem(helmet.InstanceID);
            if (backpack != null) _equipmentSystem?.EquipItem(backpack.InstanceID);
            
            // Consumables
            _inventorySystem?.AddItem("consumable_medkit", 5);
            
            // Attachments
            _inventorySystem?.AddItem("attachment_reddot", 1);
            
            Debug.Log("<color=green>✓ Full loadout applied!</color>");
        }
        
        private void ScenarioOverweight()
        {
            _inventorySystem?.ClearInventory();
            
            // Add many heavy items
            for (int i = 0; i < 20; i++)
            {
                _inventorySystem?.AddItem("weapon_ak47", 1);
            }
            
            var info = _inventorySystem?.GetWeightInfo();
            Debug.Log($"<color=yellow>⚠️ Overweight! {info?.current:F1}/{info?.capacity:F1} ({info?.percent:P0})</color>");
        }
        
        private void RunAllTests()
        {
            var invTest = GetComponent<InventorySystemTest>();
            if (invTest != null)
            {
                invTest.RunAllTests();
            }
            
            var equipTest = GetComponent<EquipmentSystemTest>();
            if (equipTest != null)
            {
                equipTest.RunAllTests();
            }
            
            var itemStatTest = GetComponent<ItemStatSystemTest>();
            if (itemStatTest != null)
            {
                itemStatTest.RunAllTests();
            }
            
            Debug.Log("<color=green>✓ All tests executed! Check console for results.</color>");
        }
        
        #endregion
    }
}