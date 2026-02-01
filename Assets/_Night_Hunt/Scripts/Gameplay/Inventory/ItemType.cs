namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Item type classification
    /// </summary>
    public enum ItemType
    {
        Equipment,      // Item trang bị (armor, weapon attachments)
        Consumable,     // Item tiêu thụ (bình máu, thức ăn) - có thời gian sử dụng
        Throwable,      // Item ném (lựu đạn, bom)
        Event,          // Item sự kiện
        Quest,          // Item nhiệm vụ
        Resource,       // Tài nguyên (có thể stack)
        Misc            // Khác
    }
}
