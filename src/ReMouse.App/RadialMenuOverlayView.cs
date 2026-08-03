using ReMouse.Core.Input;

namespace ReMouse.App;

public interface IRadialMenuOverlayView
{
    void Show(RadialMenuLayout layout, ScreenPoint center);

    void Update(RadialMenuHit hit);

    void Dismiss();
}
