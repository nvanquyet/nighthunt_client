namespace NightHunt.Networking
{
    /// <summary>
    /// Distinguishes how a match is hosted:
    ///   Custom_Relay  – Host player + Mini Relay server (friend lobbies)
    ///   Ranked_DS     – Dedicated Server (ranked matchmaking)
    /// </summary>
    public enum GameMode
    {
        None,
        Custom_Relay,
        Ranked_DS
    }
}
