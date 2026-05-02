
namespace NightHunt.UI
{
    /// <summary>
    /// Implement on any panel MonoBehaviour that needs to react to
    /// UINavigator show/hide transitions.
    ///
    /// UINavigator uses CanvasGroup for all transitions — it never calls
    /// SetActive, so Unity's OnEnable/OnDisable do NOT fire on navigation.
    /// Use these callbacks instead.
    ///
    ///   OnShow() — called right before the panel fades in.
    ///   OnHide() — called right before the panel fades out.
    /// </summary>
    public interface INavigableView
    {
        void OnShow();
        void OnHide();
    }
}
