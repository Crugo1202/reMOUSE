namespace ReMouse.Core.Input;

public sealed record PixelInspectorSnapshot(
    bool IsActive,
    PixelPoint Cursor,
    PixelPoint? SelectionStart,
    PixelRectangle? Selection);

public sealed record PixelInspectorDecision(
    bool SuppressOriginal,
    PixelInspectorSnapshot Snapshot,
    bool ModeChanged,
    bool SelectionCompleted);
