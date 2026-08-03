using ReMouse.Core.Input;

namespace ReMouse.App;

public interface IPixelInspectorOverlayView
{
    void Show(PixelInspectorSnapshot snapshot);

    void Update(PixelInspectorSnapshot snapshot);

    void Dismiss();
}
