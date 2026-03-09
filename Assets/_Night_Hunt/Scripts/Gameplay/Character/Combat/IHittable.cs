namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Implemented by any object that can receive damage (players, destructibles).
    /// Concrete implementations route damage through the appropriate server-authoritative system.
    /// </summary>
    public interface IHittable
    {
        /// <summary>
        /// Request damage application.
        /// Owner-client call → internally sends ServerRpc for authoritative processing.
        /// </summary>
        void RequestDamage(DamageInfo info);
    }
}
