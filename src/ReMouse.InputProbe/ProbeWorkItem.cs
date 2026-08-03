using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.InputProbe;

internal readonly record struct ProbeWorkItem(
    ProbeEvent? InputEvent,
    ReMouseSettings? SettingsToApply,
    TaskCompletionSource<bool>? Applied,
    InputEffect? EffectToApply)
{
    public static ProbeWorkItem FromInput(ProbeEvent inputEvent) =>
        new(inputEvent, null, null, null);

    public static ProbeWorkItem FromSettings(
        ReMouseSettings settings,
        TaskCompletionSource<bool> applied) =>
        new(null, settings, applied, null);

    public static ProbeWorkItem FromEffect(InputEffect effect) =>
        new(null, null, null, effect);
}
