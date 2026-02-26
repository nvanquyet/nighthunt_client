namespace NightHunt.GameplaySystems.World
{
    /// <summary>
    /// Loại object sẽ spawn tại WorldItemSpawnPoint.
    ///   Item      → WorldItem (item rơi đất, scatter từ SpawnTable)
    ///   Container → WorldContainer (thùng / crate / rương / chest)
    /// </summary>
    public enum WorldSpawnType
    {
        /// <summary>Item rơi trên đất (WorldItem) — scatter từ SpawnTable</summary>
        Item,

        /// <summary>Thùng chứa / Crate / Rương / Chest (WorldContainer)</summary>
        Container,
    }
}
