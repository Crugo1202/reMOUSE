namespace ReMouse.InputProbe;

internal static class KeyboardKeyName
{
    private const uint VkBrowserBack = 0xA6;
    private const uint VkBrowserForward = 0xA7;

    public static string Get(uint virtualKey)
    {
        return virtualKey switch
        {
            VkBrowserBack => "BrowserBack",
            VkBrowserForward => "BrowserForward",
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x10 => "Shift",
            0x11 => "Ctrl",
            0x12 => "Alt",
            0x1B => "Escape",
            0x20 => "Space",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x5B => "LeftWin",
            0x5C => "RightWin",
            >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
            >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
            _ => $"VK_0x{virtualKey:X2}"
        };
    }
}
