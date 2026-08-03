using ReMouse.Core.Input;

namespace ReMouse.App;

/// <summary>
/// UI-thread messages emitted by the low-level pixel-inspector bridge.
/// </summary>
public abstract record PixelInspectorUiEvent
{
    private PixelInspectorUiEvent()
    {
    }

    public sealed record Open(PixelInspectorSnapshot Snapshot) : PixelInspectorUiEvent;

    public sealed record Update(PixelInspectorSnapshot Snapshot) : PixelInspectorUiEvent;

    public sealed record SelectionCompleted(PixelInspectorSnapshot Snapshot) : PixelInspectorUiEvent;

    public sealed record Dismiss : PixelInspectorUiEvent;
}
