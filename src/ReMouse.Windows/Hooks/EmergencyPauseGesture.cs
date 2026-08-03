namespace ReMouse.Windows.Hooks;

/// <summary>
/// The emergency pause gesture deliberately uses a three-key chord so an
/// ordinary Escape press remains available to the active application.
/// </summary>
internal static class EmergencyPauseGesture
{
    internal const uint ActivationKey = 0x7B; // F12

    internal static bool IsTriggered(
        uint virtualKey,
        bool controlDown,
        bool altDown) =>
        virtualKey == ActivationKey && controlDown && altDown;
}
