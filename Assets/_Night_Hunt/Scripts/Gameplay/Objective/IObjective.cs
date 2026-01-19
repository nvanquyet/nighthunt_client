namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Base interface for objectives
    /// </summary>
    public interface IObjective
    {
        string ObjectiveId { get; }
        string ObjectiveName { get; }
        bool IsCompleted { get; }
        float Progress { get; } // 0-1

        void OnStart();
        void OnUpdate();
        void OnComplete();
        void OnFail();
    }
}

