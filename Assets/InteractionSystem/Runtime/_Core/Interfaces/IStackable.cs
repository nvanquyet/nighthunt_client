namespace NightHunt.InteractionSystem.Core
{
    public interface IStackable
    {
        int CurrentStack { get; }
        int MaxStack { get; }
        bool CanStack(ItemInstance other);
        void AddToStack(int amount);
        void RemoveFromStack(int amount);
    }
}