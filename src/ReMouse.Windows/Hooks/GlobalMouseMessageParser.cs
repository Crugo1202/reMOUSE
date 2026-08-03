namespace ReMouse.Windows.Hooks;

internal static class GlobalMouseMessageParser
{
    internal const uint WmMouseMove = 0x0200;
    internal const uint WmLButtonDown = 0x0201;
    internal const uint WmLButtonUp = 0x0202;
    internal const uint WmRButtonDown = 0x0204;
    internal const uint WmRButtonUp = 0x0205;
    internal const uint WmMButtonDown = 0x0207;
    internal const uint WmMButtonUp = 0x0208;
    internal const uint WmXButtonDown = 0x020B;
    internal const uint WmXButtonUp = 0x020C;
    internal const uint LlmhfInjected = 0x00000001;

    internal static bool TryParse(
        uint message,
        NativeMethods.MsllHookStruct hookData,
        out GlobalMouseEvent mouseEvent)
    {
        var isInjected = (hookData.Flags & LlmhfInjected) != 0;
        var point = hookData.Point;

        if (message == WmMouseMove)
        {
            mouseEvent = new GlobalMouseEvent(
                GlobalMouseEventKind.Move,
                null,
                isDown: false,
                point.X,
                point.Y,
                isInjected,
                hookData.Time);
            return true;
        }

        var isDown = message switch
        {
            WmLButtonDown or WmRButtonDown or WmMButtonDown or WmXButtonDown => true,
            WmLButtonUp or WmRButtonUp or WmMButtonUp or WmXButtonUp => false,
            _ => (bool?)null
        };

        if (isDown is null)
        {
            mouseEvent = default;
            return false;
        }

        var button = message switch
        {
            WmLButtonDown or WmLButtonUp => GlobalMouseButton.Left,
            WmRButtonDown or WmRButtonUp => GlobalMouseButton.Right,
            WmMButtonDown or WmMButtonUp => GlobalMouseButton.Middle,
            WmXButtonDown or WmXButtonUp => GetXButton(hookData.MouseData),
            _ => (GlobalMouseButton?)null
        };

        if (button is null)
        {
            mouseEvent = default;
            return false;
        }

        mouseEvent = new GlobalMouseEvent(
            GlobalMouseEventKind.Button,
            button,
            isDown.Value,
            point.X,
            point.Y,
            isInjected,
            hookData.Time);
        return true;
    }

    private static GlobalMouseButton? GetXButton(uint mouseData)
    {
        return (ushort)(mouseData >> 16) switch
        {
            0x0001 => GlobalMouseButton.XButton1,
            0x0002 => GlobalMouseButton.XButton2,
            _ => null
        };
    }
}
