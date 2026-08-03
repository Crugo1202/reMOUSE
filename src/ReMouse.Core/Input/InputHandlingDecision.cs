namespace ReMouse.Core.Input;

/// <summary>
/// Describes whether the original mouse event should continue through the hook
/// and which platform-neutral effects should be emitted for it.
/// </summary>
public sealed class InputHandlingDecision
{
    public InputHandlingDecision(bool suppressOriginal, IReadOnlyList<InputEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        SuppressOriginal = suppressOriginal;
        Effects = Array.AsReadOnly(effects.ToArray());
    }

    public bool SuppressOriginal { get; }

    public IReadOnlyList<InputEffect> Effects { get; }

    public static InputHandlingDecision PassThrough() =>
        new(false, Array.Empty<InputEffect>());

    public static InputHandlingDecision PassThrough(params InputEffect[] effects) =>
        new(false, effects);

    public static InputHandlingDecision Suppress(params InputEffect[] effects) =>
        new(true, effects);
}
