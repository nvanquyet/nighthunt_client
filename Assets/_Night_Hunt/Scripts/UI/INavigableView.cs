using System.Threading.Tasks;

namespace NightHunt.UI
{
    /// <summary>
    /// Immutable navigation payload passed through the code-first home UI flow.
    /// </summary>
    public readonly struct NavigationContext
    {
        public NavigationContext(PanelType from, PanelType to, bool forceInstant, bool bypassCanLeave = false)
            : this(from, to, forceInstant, bypassCanLeave, null, null)
        {
        }

        public NavigationContext(
            PanelType from,
            PanelType to,
            bool forceInstant,
            bool bypassCanLeave,
            string reason,
            object payload = null)
        {
            From = from;
            To = to;
            ForceInstant = forceInstant;
            BypassCanLeave = bypassCanLeave;
            Reason = reason ?? string.Empty;
            Payload = payload;
        }

        public PanelType From { get; }
        public PanelType To { get; }
        public bool ForceInstant { get; }
        public bool BypassCanLeave { get; }
        public string Reason { get; }
        public object Payload { get; }
        public bool IsRefresh => From == To;
    }

    /// <summary>
    /// Implement on panels that need deterministic navigation lifecycle callbacks.
    /// UINavigator owns the order: CanLeave -> OnHideAsync -> activate route root
    /// -> OnShowAsync -> visible shell animation.
    /// </summary>
    public interface INavigableView
    {
        bool CanLeave(NavigationContext context);
        Task OnShowAsync(NavigationContext context);
        Task OnHideAsync(NavigationContext context);
    }
}
