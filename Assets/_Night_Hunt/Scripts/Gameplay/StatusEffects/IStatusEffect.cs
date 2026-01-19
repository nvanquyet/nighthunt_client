namespace NightHunt.Gameplay.StatusEffects
{
    /// <summary>
    /// Interface for status effects
    /// </summary>
    public interface IStatusEffect
    {
        string StatusId { get; }
        string Description { get; }
        float Duration { get; }
        bool IsExpired { get; }
        
        void OnApply();
        void OnUpdate(float deltaTime);
        void OnRemove();
    }
}

