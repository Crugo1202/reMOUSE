namespace ReMouse.Core.Input;

public sealed record SideButtonBindings(
    SideButtonAction XButton1,
    SideButtonAction XButton2)
{
    public SideButtonAction For(XButtonId button) => button switch
    {
        XButtonId.XButton1 => XButton1,
        XButtonId.XButton2 => XButton2,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown XButton.")
    };

    public SideButtonBindings Swap() => new(XButton2, XButton1);

    public void Validate()
    {
        ValidateAction(XButton1, nameof(XButton1));
        ValidateAction(XButton2, nameof(XButton2));
        if (XButton1 != SideButtonAction.None && XButton1 == XButton2)
        {
            throw new ArgumentException(
                "The same side-button action cannot be assigned to both physical XButtons.",
                nameof(XButton2));
        }
    }

    private static void ValidateAction(SideButtonAction action, string parameterName)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(parameterName, action, "Unknown side-button action.");
        }
    }
}
