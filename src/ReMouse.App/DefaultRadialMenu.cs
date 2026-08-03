using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.App;

public static class DefaultRadialMenu
{
    public static RadialMenuLayout Create()
    {
        return ConfiguredRadialMenu.Create(DefaultSettings.RadialMenu);
    }
}
