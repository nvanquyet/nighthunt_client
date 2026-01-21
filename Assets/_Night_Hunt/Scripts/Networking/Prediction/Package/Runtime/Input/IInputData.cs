namespace NightHunt.Networking.Prediction.Input
{
    /// <summary>
    /// Base interface for prediction input payloads.
    /// </summary>
    public interface IInputData
    {
        bool HasChanged(IInputData other);
        void Reset();
    }
}

