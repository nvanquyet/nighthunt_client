namespace NightHunt.Gameplay.Inventory.Logic.Services
{
    /// <summary>
    /// Interface for item usage service
    /// Handles item usage logic (consumables, equipment, throwables, etc.)
    /// </summary>
    public interface IItemUsageService
    {
        // Usage operations
        bool CanUseItem(string itemId);
        bool StartUseItem(string itemId);
        bool CancelUseItem(string itemId);
        bool IsUsingItem();
        string GetCurrentUsingItemId();
        float GetUseProgress(); // 0-1

        // Item type specific operations
        bool UseConsumable(string itemId);
        bool EquipItem(string itemId);
        bool PrepareThrowItem(string itemId);
        bool TriggerEventItem(string itemId);
        bool TriggerQuestItem(string itemId);
    }
}
