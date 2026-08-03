namespace ReMouse.Core.Input;

/// <summary>
/// The raw Windows side-button identity. This is deliberately not a physical
/// position; Terra Pro currently reports its lower side button as XButton1 and
/// its upper side button as XButton2.
/// </summary>
public enum XButtonId
{
    XButton1 = 1,
    XButton2 = 2
}
