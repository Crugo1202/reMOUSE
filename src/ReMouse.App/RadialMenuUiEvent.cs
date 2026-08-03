using ReMouse.Core.Input;

namespace ReMouse.App;

public abstract record RadialMenuUiEvent
{
    private RadialMenuUiEvent()
    {
    }

    public sealed record Open(ScreenPoint Center) : RadialMenuUiEvent;

    public sealed record Preview(RadialMenuHit Hit) : RadialMenuUiEvent;

    public sealed record Commit(RadialMenuItem? Item) : RadialMenuUiEvent;

    public sealed record Cancel : RadialMenuUiEvent;
}
