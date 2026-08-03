namespace ReMouse.Core.Input;

public sealed class RadialMenuSession
{
    private readonly RadialMenuLayout _layout;
    private bool _isOpen;
    private ScreenPoint _center;
    private RadialMenuHit _currentHit;

    public RadialMenuSession(RadialMenuLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    public bool IsOpen => _isOpen;

    public RadialMenuHit CurrentHit =>
        _isOpen
            ? _currentHit
            : throw new InvalidOperationException("The radial menu session is not open.");

    public void Open(ScreenPoint center)
    {
        if (_isOpen)
        {
            throw new InvalidOperationException("The radial menu session is already open.");
        }

        _center = center;
        _currentHit = new RadialMenuHit(true, null, 0, 0);
        _isOpen = true;
    }

    public RadialMenuHit Update(ScreenPoint cursor)
    {
        EnsureOpen();
        _currentHit = RadialMenuHitTester.HitTest(_layout, _center, cursor);
        return _currentHit;
    }

    public RadialMenuItem? Commit(ScreenPoint cursor)
    {
        var hit = Update(cursor);
        _isOpen = false;
        return hit.SlotIndex is { } index ? _layout.Items[index] : null;
    }

    public void Cancel()
    {
        EnsureOpen();
        _isOpen = false;
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("The radial menu session is not open.");
        }
    }
}
