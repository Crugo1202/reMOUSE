namespace ReMouse.Core.Input;

public readonly record struct InputKeyStroke
{
    public InputKeyStroke(ushort virtualKey, bool isDown)
    {
        if (virtualKey == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(virtualKey),
                "A key stroke must identify a non-zero virtual key.");
        }

        VirtualKey = virtualKey;
        IsDown = isDown;
    }

    public ushort VirtualKey { get; }

    public bool IsDown { get; }

    public static InputKeyStroke Down(ushort virtualKey) => new(virtualKey, true);

    public static InputKeyStroke Up(ushort virtualKey) => new(virtualKey, false);
}
